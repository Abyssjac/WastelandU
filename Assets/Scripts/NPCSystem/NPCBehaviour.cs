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

    [Header("Economy")]
    [Tooltip("Multiplier applied to TotalAffinity to calculate daily income. Income = TotalAffinity × coefficient.")]
    [SerializeField] private float incomeCoefficient = 1f;

    [Header("Room Assignment")]
    [Tooltip("Enable once an NPC is assigned to a room.")]
    [SerializeField] private bool hasRoom;

    [Tooltip("StableId of the room this NPC lives in.\n" +
             "Use the ContextMenu 'List All Rooms' in Play Mode to find valid StableIds.")]
    [SerializeField] private Vector3Int assignedRoomStableId;

    // ── Runtime ───────────────────────────────────────────────────
    private NPCProperty    _property;
    private NPCRuntimeData _runtimeData;

    public Key_NPC        NpcKey      => npcKey;
    public NPCProperty    Property    => _property;
    public NPCRuntimeData RuntimeData => _runtimeData;
    public bool           HasRoom     => hasRoom;
    public Vector3Int     AssignedRoomStableId => assignedRoomStableId;

    // ─────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Always initialise runtime data first so Start() never sees a null reference,
        // even in the brief window between Destroy(gameObject) and the deferred destroy.
        _runtimeData = new NPCRuntimeData();
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

    /// <summary>Triggers an affinity recalculation. Can be called externally (e.g. from editor debug tools).</summary>
    public void ForceRecalculateAffinity() => RecalculateEnvironmentAffinity();

    private void RecalculateEnvironmentAffinity()
    {
        if (!hasRoom || _property == null)
        {
            _runtimeData.LivingEnvironmentAffinity = 0f;
            return;
        }

        RoomData room = GridRoomManager.Instance?.GetRoomByStableId(assignedRoomStableId);
        if (room == null)
        {
            _runtimeData.LivingEnvironmentAffinity = 0f;
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
    }

    /// <summary>
    /// Returns the currency this NPC generates at the start of each new day.
    /// Formula: TotalAffinity × incomeCoefficient.
    /// </summary>
    public float CalculateDailyIncome()
    {
        return _runtimeData.TotalAffinity * incomeCoefficient;
    }

    // ─────────────────────────────────────────────────────────────
    // Room Assignment (written by NPCRoomAssignmentManager)
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called exclusively by <see cref="NPCRoomAssignmentManager"/> to apply a room assignment.
    /// Triggers an immediate affinity recalculation.
    /// </summary>
    public void SetRoomAssignment(bool hasRoom, Vector3Int stableId)
    {
        this.hasRoom             = hasRoom;
        this.assignedRoomStableId = stableId;
        RecalculateEnvironmentAffinity();
    }

    // ─────────────────────────────────────────────────────────────
    // Editor helpers
    // ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
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
