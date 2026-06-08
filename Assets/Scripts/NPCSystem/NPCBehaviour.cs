using JackyUtility;
using UnityEngine;

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

    [Header("Daily Interaction")]
    [Tooltip("How much DailyInteractionAffinity increases per player interaction.")]
    [SerializeField] private float interactionGainPerClick = 10f;

    [Tooltip("If the player skips this many days without interacting, affinity starts to decay.")]
    [SerializeField] private int decayThresholdDays = 3;

    [Tooltip("How much DailyInteractionAffinity decreases each day after the decay threshold is exceeded.")]
    [SerializeField] private float decayAmountPerDay = 5f;

    [Tooltip("DailyInteractionAffinity will never decay below this floor.")]
    [SerializeField] private float decayFloor = 0f;

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
        _runtimeData = new NPCRuntimeData(0f,50f,0f);
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

        // Subscribe to new-day event for decay + reset
        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnNewDayStarted += HandleNewDayStarted;

        // Calculate once on start (in case rooms already exist from a preset)
        RecalculateEnvironmentAffinity();
    }

    private void OnDestroy()
    {
        if (GridRoomManager.Instance != null)
            GridRoomManager.Instance.OnRoomFurnitureChanged -= HandleRoomFurnitureChanged;

        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnNewDayStarted -= HandleNewDayStarted;
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
    // Daily Interaction
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by the UI when the player clicks the daily-interact button.
    /// Increases <see cref="NPCRuntimeData.DailyInteractionAffinity"/> and records the current day.
    /// </summary>
    public void AddDailyInteractionAffinity()
    {
        int today = DayNightManager.Instance != null ? DayNightManager.Instance.CurrentDay : 0;
        _runtimeData.DailyInteractionAffinity =
            Mathf.Min(maxOtherAffinityRuntime, _runtimeData.DailyInteractionAffinity + interactionGainPerClick);
        _runtimeData.LastInteractionDay = today;
        _runtimeData.InteractedToday = true;
        Debug.Log($"[NPCBehaviour] {npcKey} interaction: +{interactionGainPerClick}, " +
                  $"total={_runtimeData.DailyInteractionAffinity}, day={today}");
    }

    private void HandleNewDayStarted(int newDay)
    {
        _runtimeData.InteractedToday = false;

        // Decay check
        if (_runtimeData.LastInteractionDay >= 0)
        {
            int gap = newDay - _runtimeData.LastInteractionDay;
            if (gap > decayThresholdDays)
            {
                _runtimeData.DailyInteractionAffinity =
                    Mathf.Max(decayFloor, _runtimeData.DailyInteractionAffinity - decayAmountPerDay);
                Debug.Log($"[NPCBehaviour] {npcKey} affinity decayed by {decayAmountPerDay} " +
                          $"(gap={gap} days), new value={_runtimeData.DailyInteractionAffinity}");
            }
        }
    }

    // Cached max used by AddDailyInteractionAffinity — mirrors maxOtherAffinity in the UI
    // kept here so NPCBehaviour can clamp independently of any UI instance.
    [SerializeField] private float maxOtherAffinityRuntime = 100f;

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
