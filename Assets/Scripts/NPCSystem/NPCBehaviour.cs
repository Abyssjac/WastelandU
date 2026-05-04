using JackyUtility;
using UnityEngine;

/// <summary>
/// Key identifying an NPC type. Should correspond to entries in <see cref="NPCDatabase"/>.
/// </summary>
public enum Key_NPC
{
    None = 0,
    Artist = 1,   // 艺术家
    Botanist = 2,   // 植物学家
    Athlete = 3,   // 运动员
}

/// <summary>
/// Attach to an NPC GameObject.
/// Resolves its <see cref="NPCProperty"/> at startup via <see cref="NPCDatabase"/>,
/// then subscribes to room furniture change events to keep living-environment affinity current.
/// </summary>
public class NPCBehaviour : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────
    [Header("Identity")]
    [SerializeField] private Key_NPC npcKey;

    [Header("Room Assignment")]
    [Tooltip("Enable once an NPC is assigned to a room.")]
    [SerializeField] private bool hasRoom;

    [Tooltip("StableId of the room this NPC lives in.\n" +
             "Use the ContextMenu 'List All Rooms' in Play Mode to find valid StableIds.")]
    [SerializeField] private Vector3Int assignedRoomStableId;

    // ── Debug display (Inspector read-only during Play Mode) ──────
    [Header("Debug — Affinity (read-only)")]
    [SerializeField] private float dbg_envAffinity;
    [SerializeField] private float dbg_totalAffinity;

    // ── Runtime ───────────────────────────────────────────────────
    private NPCProperty    _property;
    private NPCRuntimeData _runtimeData;

    public Key_NPC        NpcKey      => npcKey;
    public NPCProperty    Property    => _property;
    public NPCRuntimeData RuntimeData => _runtimeData;
    public bool           HasRoom     => hasRoom;
    public Vector3Int     AssignedRoomStableId => assignedRoomStableId;

    // ── Spawn injection guard ─────────────────────────────────────────────
    // Set by NPCManager.SpawnNPC() immediately before Instantiate.
    // Prevents NPCs from being placed in the scene without going through the manager.
    //private static Key_NPC _pendingSpawnKey  = Key_NPC.None;
    //private static bool    _spawnAuthorized  = false;

    ///// <summary>Called exclusively by <see cref="NPCManager"/> before Instantiate.</summary>
    //internal static void AllowNextSpawn(Key_NPC key)
    //{
    //    _pendingSpawnKey = key;
    //    _spawnAuthorized = true;
    //}

    // ─────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Always initialise runtime data first so Start() never sees a null reference,
        // even in the brief window between Destroy(gameObject) and the deferred destroy.
        _runtimeData = new NPCRuntimeData();

        //// Injection guard: only NPCManager is allowed to spawn NPCs.
        //// TODO: tighten to hard-fail once all spawning is code-driven.
        //if (!_spawnAuthorized || _pendingSpawnKey != npcKey)
        //{
        //    Debug.LogError($"[NPCBehaviour] Unauthorized instantiation of '{npcKey}' on '{gameObject.name}'. " +
        //                   "Use NPCManager.SpawnNPC() instead of placing NPCs directly in the scene.");
        //    Destroy(gameObject);
        //    return;
        //}

        //_spawnAuthorized = false;
        //_pendingSpawnKey = Key_NPC.None;
    }

    private void Start()
    {
        // Resolve NPCProperty from the database
        var dbMgr = PropertyDatabaseManager.Instance;
        if (dbMgr != null)
        {
            var db = dbMgr.GetDatabase<NPCDatabase>();
            if (db != null)
                _property = db.GetByEnum(npcKey);
        }

        if (_property == null)
        {
            Debug.LogWarning($"[NPCBehaviour] No NPCProperty found for key '{npcKey}' on '{gameObject.name}'. " +
                             "Make sure NPCDatabase is registered in PropertyDatabaseManager.");
            return;
        }

        // Subscribe to furniture-change events
        if (GridRoomManager.Instance != null)
            GridRoomManager.Instance.OnRoomFurnitureChanged += HandleRoomFurnitureChanged;

        // Calculate once on start (in case rooms already exist from a preset)
        RecalculateEnvironmentAffinity();
    }

    private void OnDestroy()
    {
        if (GridRoomManager.Instance != null)
            GridRoomManager.Instance.OnRoomFurnitureChanged -= HandleRoomFurnitureChanged;
    }

    // ─────────────────────────────────────────────────────────────
    // Event handler
    // ─────────────────────────────────────────────────────────────

    private void HandleRoomFurnitureChanged(RoomData room)
    {
        if (!hasRoom) return;
        if (room.StableId != assignedRoomStableId) return;
        RecalculateEnvironmentAffinity();
    }

    // ─────────────────────────────────────────────────────────────
    // Affinity calculation
    // ─────────────────────────────────────────────────────────────

    private void RecalculateEnvironmentAffinity()
    {
        if (!hasRoom || _property == null)
        {
            _runtimeData.LivingEnvironmentAffinity = 0f;
            RefreshDebugDisplay();
            return;
        }

        RoomData room = GridRoomManager.Instance?.GetRoomByStableId(assignedRoomStableId);
        if (room == null)
        {
            _runtimeData.LivingEnvironmentAffinity = 0f;
            RefreshDebugDisplay();
            return;
        }

        float total = 0f;
        TagAffinityWeight[] weights = _property.tagWeights;
        for (int i = 0; i < weights.Length; i++)
        {
            if (weights[i].tag == FurnitureTag.None) continue;
            int count = room.GetTagCount(weights[i].tag);
            total += weights[i].weight * count;
        }

        _runtimeData.LivingEnvironmentAffinity =
            Mathf.Min(_property.maxEnvAffinity, Mathf.Max(0f, total));

        RefreshDebugDisplay();
    }

    private void RefreshDebugDisplay()
    {
        dbg_envAffinity   = _runtimeData.LivingEnvironmentAffinity;
        dbg_totalAffinity = _runtimeData.TotalAffinity;
    }

    // ─────────────────────────────────────────────────────────────
    // Editor helpers
    // ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    /// <summary>
    /// Lists all currently detected rooms and their StableIds in the Console.
    /// Run this in Play Mode to find the correct value for assignedRoomStableId.
    /// </summary>
    [ContextMenu("Debug: List All Rooms And StableIds (Play Mode)")]
    private void Editor_ListAllRooms()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[NPCBehaviour] Only works in Play Mode.");
            return;
        }

        if (GridRoomManager.Instance == null)
        {
            Debug.LogWarning("[NPCBehaviour] GridRoomManager not found.");
            return;
        }

        var rooms = GridRoomManager.Instance.ActiveRooms;
        if (rooms.Count == 0)
        {
            Debug.Log("[NPCBehaviour] No rooms detected.");
            return;
        }

        Debug.Log($"[NPCBehaviour] {rooms.Count} room(s) found:");
        for (int i = 0; i < rooms.Count; i++)
            Debug.Log($"  Room {rooms[i].RoomId} | StableId: {rooms[i].StableId} | Cells: {rooms[i].CellCount}");
    }

    /// <summary>
    /// Forces a manual recalculation of environment affinity. Play Mode only.
    /// </summary>
    [ContextMenu("Debug: Force Recalculate Env Affinity (Play Mode)")]
    private void Editor_ForceRecalculate()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[NPCBehaviour] Only works in Play Mode.");
            return;
        }
        RecalculateEnvironmentAffinity();
        Debug.Log($"[NPCBehaviour] EnvAffinity = {_runtimeData.LivingEnvironmentAffinity}");
    }
#endif
}
