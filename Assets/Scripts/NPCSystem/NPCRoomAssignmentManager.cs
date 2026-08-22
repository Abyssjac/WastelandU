using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;

/// <summary>
/// Manages NPC ? Room assignments and the interactive room-selection highlight system.
/// Singleton — attach to any scene GameObject (does NOT need DontDestroyOnLoad).
///
/// Workflow:
///   1. Call <see cref="EnterSelectRoomMode"/> to begin selecting a room for an NPC.
///   2. The player hovers and left-clicks an available room.
///   3. Call <see cref="ExitSelectRoomMode"/> (via the Confirm button) to apply the change.
/// </summary>
public class NPCRoomAssignmentManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static NPCRoomAssignmentManager Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("References")]
    [SerializeField] private BuildPositionProvider positionProvider;

    [Header("Highlight Materials")]
    [Tooltip("Color A — current / pending selected room.")]
    [SerializeField] private Material materialA;
    [Tooltip("Color B — available (empty) rooms.")]
    [SerializeField] private Material materialB;
    [Tooltip("Color C — rooms occupied by a different NPC.")]
    [SerializeField] private Material materialC;
    [Tooltip("Color D — room currently hovered by the mouse.")]
    [SerializeField] private Material materialD;

    [Header("Highlight Cube")]
    [Tooltip("Prefab used to fill each room cell. Must have a MeshRenderer.")]
    [SerializeField] private GameObject highlightCubePrefab;
    [Tooltip("Scale multiplier applied to the cell size when sizing each highlight cube.")]
    [SerializeField] [Range(0.1f, 1f)] private float cubeScaleMultiplier = 0.9f;

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Fired when selection mode is entered. Argument is the NPC being assigned.</summary>
    public event Action<Key_NPC> OnSelectionModeEntered;

    /// <summary>Fired when selection mode is exited (confirm or cancel).</summary>
    public event Action OnSelectionModeExited;

    /// <summary>Fired after a successful room assignment is written.</summary>
    public event Action<Key_NPC, Vector3Int> OnRoomAssigned;

    /// <summary>Fired after an NPC's room assignment is removed.</summary>
    public event Action<Key_NPC> OnRoomUnassigned;

    // ── Internal state ────────────────────────────────────────────────────────
    private enum SelectionState { Idle, Selecting }
    private SelectionState _state = SelectionState.Idle;

    // Who is being assigned
    private Key_NPC    _selectedNPC;

    // What room will be applied when confirmed
    private Vector3Int _pendingRoomStableId;
    private bool       _hasPendingRoom;

    // What the NPC had BEFORE entering selection (to detect changes)
    private Vector3Int _originalRoomStableId;
    private bool       _hadOriginalRoom;

    // Assignment tables (source of truth)
    private readonly Dictionary<Key_NPC,    Vector3Int> _npcToRoom = new Dictionary<Key_NPC, Vector3Int>();
    private readonly Dictionary<Vector3Int, Key_NPC>    _roomToNPC = new Dictionary<Vector3Int, Key_NPC>();

    // Highlight cube tracking
    private readonly Dictionary<Vector3Int, List<GameObject>> _roomHighlightCubes =
        new Dictionary<Vector3Int, List<GameObject>>();
    private readonly Dictionary<Vector3Int, Material> _roomBaseMaterial =
        new Dictionary<Vector3Int, Material>();

    // Hover tracking
    private Vector3Int _hoveredRoomStableId;
    private bool       _hasHoveredRoom;

    /// <summary>
    /// Rebuilds the room-assignment lookup tables from persistent NPC save data.
    /// It deliberately does not enter selection mode or create any highlights.
    /// </summary>
    public void RestoreAssignments(IReadOnlyList<NPCRoomAssignmentSnapshot> assignments)
    {
        if (_state == SelectionState.Selecting)
            CancelSelectRoomMode();

        _npcToRoom.Clear();
        _roomToNPC.Clear();

        if (assignments == null)
            return;

        foreach (NPCRoomAssignmentSnapshot assignment in assignments)
        {
            if (assignment.npcKey == Key_NPC.None)
                continue;

            if (_npcToRoom.ContainsKey(assignment.npcKey))
                continue;

            if (_roomToNPC.ContainsKey(assignment.roomStableId))
            {
                Debug.LogWarning(
                    $"[{nameof(NPCRoomAssignmentManager)}] Save data assigned room {assignment.roomStableId} to more than one NPC. " +
                    $"Keeping the first assignment and skipping '{assignment.npcKey}'.",
                    this);
                continue;
            }

            _npcToRoom.Add(assignment.npcKey, assignment.roomStableId);
            _roomToNPC.Add(assignment.roomStableId, assignment.npcKey);
            UpdateNPCBehaviourRoom(assignment.npcKey, true, assignment.roomStableId);
            OnRoomAssigned?.Invoke(assignment.npcKey, assignment.roomStableId);
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (positionProvider == null) { 
            positionProvider = BuildPositionProvider.Instance;
        }
        if (positionProvider == null)
            Debug.LogError("[NPCRoomAssignmentManager] positionProvider reference is not assigned.");
    }

    private void Update()
    {
        if (_state != SelectionState.Selecting) return;

        UpdateHoverHighlight();
        if (Input.GetMouseButtonDown(0))
            HandleRoomClick();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Enter room-selection mode for <paramref name="npcKey"/>.
    /// Spawns highlight cubes over all detected rooms and broadcasts <see cref="OnSelectionModeEntered"/>.
    /// Does nothing if already in Selecting state.
    /// </summary>
    public void EnterSelectRoomMode(Key_NPC npcKey)
    {
        Debug.Log($"Entering room selection mode for NPC: {npcKey}");
        if (_state == SelectionState.Selecting) return;
        Debug.Log($"Current state: {_state}. Proceeding to enter Selecting state.");
        if (npcKey == Key_NPC.None) return;

        _selectedNPC = npcKey;

        // Snapshot the NPC's current assignment as the starting pending value.
        _hadOriginalRoom     = _npcToRoom.TryGetValue(npcKey, out _originalRoomStableId);
        _hasPendingRoom      = _hadOriginalRoom;
        _pendingRoomStableId = _originalRoomStableId;
        _hasHoveredRoom      = false;

        _state = SelectionState.Selecting;

        SpawnHighlightCubes();
        OnSelectionModeEntered?.Invoke(_selectedNPC);
    }

    /// <summary>
    /// Exit room-selection mode, applying the pending room if it changed.
    /// Broadcasts <see cref="OnSelectionModeExited"/> after cleanup.
    /// Safe to call when already Idle.
    /// </summary>
    public void ExitSelectRoomMode()
    {
        if (_state != SelectionState.Selecting) return;

        _state = SelectionState.Idle;
        DestroyHighlightCubes();

        // Apply only if something actually changed.
        bool pendingChanged = _hasPendingRoom  != _hadOriginalRoom
                           || (_hasPendingRoom && _pendingRoomStableId != _originalRoomStableId);

        if (pendingChanged)
        {
            if (_hasPendingRoom)
                AssignRoom(_selectedNPC, _pendingRoomStableId);
            else
                UnassignNPC(_selectedNPC);
        }

        OnSelectionModeExited?.Invoke();
    }

    /// <summary>
    /// Exit room-selection mode, discarding any pending room change.
    /// Broadcasts <see cref="OnSelectionModeExited"/> after cleanup.
    /// Safe to call when already Idle.
    /// </summary>
    public void CancelSelectRoomMode()
    {
        if (_state != SelectionState.Selecting) return;

        _state = SelectionState.Idle;
        DestroyHighlightCubes();

        OnSelectionModeExited?.Invoke();
    }

    /// <summary>
    /// Directly assign <paramref name="npcKey"/> to <paramref name="roomStableId"/>,
    /// evicting any previous occupant of that room and clearing the NPC's old room.
    /// </summary>
    public void AssignRoom(Key_NPC npcKey, Vector3Int roomStableId)
    {
        // Clear NPC's current room.
        if (_npcToRoom.TryGetValue(npcKey, out Vector3Int oldRoom))
        {
            _roomToNPC.Remove(oldRoom);
            UpdateNPCBehaviourRoom(npcKey, false, Vector3Int.zero);
        }

        // Evict any NPC already in the target room.
        if (_roomToNPC.TryGetValue(roomStableId, out Key_NPC previousOccupant))
        {
            _npcToRoom.Remove(previousOccupant);
            UpdateNPCBehaviourRoom(previousOccupant, false, Vector3Int.zero);
            OnRoomUnassigned?.Invoke(previousOccupant);
        }

        _npcToRoom[npcKey]       = roomStableId;
        _roomToNPC[roomStableId] = npcKey;
        UpdateNPCBehaviourRoom(npcKey, true, roomStableId);

        OnRoomAssigned?.Invoke(npcKey, roomStableId);
    }

    /// <summary>
    /// Remove <paramref name="npcKey"/> from their assigned room without assigning a new one.
    /// No-op if the NPC has no assignment.
    /// </summary>
    public void UnassignNPC(Key_NPC npcKey)
    {
        if (!_npcToRoom.TryGetValue(npcKey, out Vector3Int room)) return;

        _npcToRoom.Remove(npcKey);
        _roomToNPC.Remove(room);
        UpdateNPCBehaviourRoom(npcKey, false, Vector3Int.zero);

        OnRoomUnassigned?.Invoke(npcKey);
    }

    /// <summary>
    /// Remove whoever is currently assigned to <paramref name="roomStableId"/>.
    /// No-op if the room is unoccupied.
    /// </summary>
    public void EmptyRoom(Vector3Int roomStableId)
    {
        if (_roomToNPC.TryGetValue(roomStableId, out Key_NPC npc))
            UnassignNPC(npc);
    }

    /// <summary>Try to get the room currently assigned to <paramref name="npcKey"/>.</summary>
    public bool TryGetAssignedRoom(Key_NPC npcKey, out Vector3Int stableId)
        => _npcToRoom.TryGetValue(npcKey, out stableId);

    /// <summary>Returns true if any NPC is currently assigned to this room.</summary>
    public bool IsRoomOccupied(Vector3Int stableId) => _roomToNPC.ContainsKey(stableId);

    // ── Private: click & hover ────────────────────────────────────────────────

    private void HandleRoomClick()
    {
        RoomData room = GetHoveredRoom();
        if (room == null) return;

        Vector3Int stableId = room.StableId;

        // Color C — occupied by a DIFFERENT NPC: ignore.
        if (_roomToNPC.TryGetValue(stableId, out Key_NPC occupant) && occupant != _selectedNPC)
            return;

        // Color A — clicking the current pending room cancels the pending assignment.
        if (_hasPendingRoom && stableId == _pendingRoomStableId)
        {
            _hasPendingRoom = false;
            _roomBaseMaterial[stableId] = materialB;
            // If hovering, keep D visible; base is already B so restore will be correct.
            if (!(_hasHoveredRoom && _hoveredRoomStableId == stableId))
                SetRoomMaterial(stableId, materialB);
            return;
        }

        // Color B — clicking an available room: make it the new pending.
        Vector3Int prevPending = _pendingRoomStableId;
        bool       hadPending  = _hasPendingRoom;

        _pendingRoomStableId = stableId;
        _hasPendingRoom      = true;

        // Restore the previously pending room to Color B.
        if (hadPending && _roomHighlightCubes.ContainsKey(prevPending))
        {
            _roomBaseMaterial[prevPending] = materialB;
            if (!(_hasHoveredRoom && _hoveredRoomStableId == prevPending))
                SetRoomMaterial(prevPending, materialB);
        }

        // Mark the newly selected room as Color A.
        _roomBaseMaterial[stableId] = materialA;
        if (!(_hasHoveredRoom && _hoveredRoomStableId == stableId))
            SetRoomMaterial(stableId, materialA);
    }

    private void UpdateHoverHighlight()
    {
        RoomData   newRoom     = GetHoveredRoom();
        Vector3Int newStableId = newRoom != null ? newRoom.StableId : Vector3Int.zero;
        bool       newHasHover = newRoom != null;

        // No change — skip.
        if (newHasHover == _hasHoveredRoom
         && (!newHasHover || newStableId == _hoveredRoomStableId))
            return;

        // Restore the previously hovered room to its base material.
        if (_hasHoveredRoom && _roomHighlightCubes.ContainsKey(_hoveredRoomStableId))
        {
            if (_roomBaseMaterial.TryGetValue(_hoveredRoomStableId, out Material baseMat))
                SetRoomMaterial(_hoveredRoomStableId, baseMat);
        }

        _hasHoveredRoom      = newHasHover;
        _hoveredRoomStableId = newStableId;

        // Apply Color D to the newly hovered room.
        if (_hasHoveredRoom && _roomHighlightCubes.ContainsKey(_hoveredRoomStableId))
            SetRoomMaterial(_hoveredRoomStableId, materialD);
    }

    private RoomData GetHoveredRoom()
    {
        if (positionProvider == null || !positionProvider.HasValidHit) return null;
        return GridRoomManager.Instance?.GetRoomAtCell(positionProvider.CurrentCell);
    }

    // ── Private: highlight cubes ──────────────────────────────────────────────

    private void SpawnHighlightCubes()
    {
        if (GridRoomManager.Instance == null) return;

        if (highlightCubePrefab == null)
        {
            Debug.LogError("[NPCRoomAssignmentManager] highlightCubePrefab is not assigned.");
            return;
        }

        Vector3 cubeScale = positionProvider != null
            ? positionProvider.CellSize * cubeScaleMultiplier
            : Vector3.one * cubeScaleMultiplier;

        foreach (RoomData room in GridRoomManager.Instance.ActiveRooms)
        {
            Material mat = DetermineBaseMaterial(room.StableId);
            _roomBaseMaterial[room.StableId] = mat;

            var cubes = new List<GameObject>();
            foreach (Vector3Int cell in room.Cells)
            {
                Vector3 worldCenter = positionProvider != null
                    ? positionProvider.CellToWorldCenter(cell)
                    : (Vector3)cell + Vector3.one * 0.5f;

                GameObject cube      = Instantiate(highlightCubePrefab, worldCenter, Quaternion.identity);
                cube.transform.localScale = cubeScale;
                SetCubeMaterial(cube, mat);
                cubes.Add(cube);
            }

            _roomHighlightCubes[room.StableId] = cubes;
        }
    }

    private void DestroyHighlightCubes()
    {
        foreach (var kvp in _roomHighlightCubes)
            foreach (GameObject go in kvp.Value)
                if (go != null) Destroy(go);

        _roomHighlightCubes.Clear();
        _roomBaseMaterial.Clear();
        _hasHoveredRoom = false;
    }

    private Material DetermineBaseMaterial(Vector3Int stableId)
    {
        // Color A — the current pending room.
        if (_hasPendingRoom && stableId == _pendingRoomStableId)
            return materialA;

        // Color C — occupied by a different NPC.
        if (_roomToNPC.TryGetValue(stableId, out Key_NPC occupant) && occupant != _selectedNPC)
            return materialC;

        // Color B — available.
        return materialB;
    }

    private void SetRoomMaterial(Vector3Int stableId, Material mat)
    {
        if (!_roomHighlightCubes.TryGetValue(stableId, out List<GameObject> cubes)) return;
        for (int i = 0; i < cubes.Count; i++)
            SetCubeMaterial(cubes[i], mat);
    }

    private static void SetCubeMaterial(GameObject cube, Material mat)
    {
        if (cube == null) return;
        MeshRenderer r = cube.GetComponent<MeshRenderer>();
        if (r != null) r.material = mat;
    }

    // ── Private: NPCBehaviour write-back ──────────────────────────────────────

    private static void UpdateNPCBehaviourRoom(Key_NPC npcKey, bool hasRoom, Vector3Int stableId)
    {
        if (NPCManager.Instance == null) return;

        GameObject go = NPCManager.Instance.GetRegisteredNPC(npcKey);
        if (go == null) return;

        NPCBehaviour behaviour = go.GetComponent<NPCBehaviour>();
        if (behaviour != null)
            behaviour.SetRoomAssignment(hasRoom, stableId);
    }
}

/// <summary>Non-serialized transfer object used while restoring saved NPC room assignments.</summary>
public readonly struct NPCRoomAssignmentSnapshot
{
    public readonly Key_NPC npcKey;
    public readonly Vector3Int roomStableId;

    public NPCRoomAssignmentSnapshot(Key_NPC npcKey, Vector3Int roomStableId)
    {
        this.npcKey = npcKey;
        this.roomStableId = roomStableId;
    }
}
