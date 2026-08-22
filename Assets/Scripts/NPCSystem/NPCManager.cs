using System.Collections.Generic;
using System;
using JackyUtility;
using UnityEngine;

/// <summary>
/// Runtime manager for all NPCs currently registered in the loaded game world.
/// Singleton �� place on a DontDestroyOnLoad GameObject.
///
/// NPCs may be spawned dynamically or placed directly in a scene. Scene NPCs
/// register through <see cref="NPCRegister"/> while they are enabled.
/// </summary>
public class NPCManager : MonoBehaviour, IDebuggable
{
    // ���� Singleton ������������������������������������������������������������������������������������������������������������������������
    public static NPCManager Instance { get; private set; }
    // ── Nested Types ──────────────────────────────────────────────────────────
    [Serializable]
    private class InitialSpawnEntry
    {
        public Key_NPC   key;
        public Transform spawnPoint;
    }


    // ���� Inspector ��������������������������������������������������������������������������������������������������������������������������
    [Header("Spawn")]
    [Tooltip("Fallback spawn position used when SpawnNPC(key) is called without an explicit position.")]
    [SerializeField] private Transform defaultSpawnPoint;

    [Header("Initial Spawns")]
    [Tooltip("NPCs spawned automatically on Start. Entries with a null spawnPoint are skipped.")]
    [SerializeField] private List<InitialSpawnEntry> _initialSpawns = new List<InitialSpawnEntry>();

    [Header("Debug")]
    [SerializeField] private bool debugEnabled;

    // ���� IDebuggable ����������������������������������������������������������������������������������������������������������������������
    public string DebugId      => "npcmanager";
    public bool   DebugEnabled { get => debugEnabled; set => debugEnabled = value; }

    // ���� State ����������������������������������������������������������������������������������������������������������������������������������
    private readonly Dictionary<Key_NPC, GameObject> _registeredNPCs =
        new Dictionary<Key_NPC, GameObject>();

    // Long-term player progress. This is intentionally separate from
    // _registeredNPCs: a scene object can disappear while its recruitment and
    // interaction progress must survive scene transitions and save/load.
    private readonly Dictionary<Key_NPC, NPCProgressRuntimeState> _npcProgress =
        new Dictionary<Key_NPC, NPCProgressRuntimeState>();

    private NPCInteractionDatabase _interactionDatabase;

    private sealed class NPCProgressRuntimeState
    {
        public NPCStatus Status = NPCStatus.Unrecruited;
        public readonly HashSet<NPCInteractionType> UnlockedInteractions =
            new HashSet<NPCInteractionType>();
        public readonly HashSet<NPCInteractionType> LockedInteractions =
            new HashSet<NPCInteractionType>();
        public bool HasPersistentRuntimeData;
        public NPCPersistentRuntimeData PersistentRuntimeData;
    }

    /// <summary>Read-only view of all currently registered NPCs keyed by <see cref="Key_NPC"/>.</summary>
    public IReadOnlyDictionary<Key_NPC, GameObject> RegisteredNPCs => _registeredNPCs;

    /// <summary>
    /// Compatibility alias for older callers. New code should use
    /// <see cref="RegisteredNPCs"/> because scene NPCs are registered too.
    /// </summary>
    [Obsolete("Use RegisteredNPCs instead.")]
    public IReadOnlyDictionary<Key_NPC, GameObject> SpawnedNPCs => RegisteredNPCs;

    /// <summary>Fired whenever an NPC's persistent recruitment status changes.</summary>
    public event Action<Key_NPC, NPCStatus> OnNPCStatusChanged;

    /// <summary>Fired when a runtime interaction override changes.</summary>
    public event Action<Key_NPC, NPCInteractionType, bool> OnNPCInteractionAvailabilityChanged;

    // ���� Lifecycle ��������������������������������������������������������������������������������������������������������������������������
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        RegisterDebugCommands();
    }

    private void Start()
    {
        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnNewDayStarted += HandleNewDay;
        else
            Debug.LogWarning("[NPCManager] DayNightManager instance not found �� daily income will not be collected.");

        SpawnInitialNPCs();
    }

    private void OnDestroy()
    {
        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnNewDayStarted -= HandleNewDay;
    }

    private void SpawnInitialNPCs()
    {
        for (int i = 0; i < _initialSpawns.Count; i++)
        {
            var entry = _initialSpawns[i];
            if (entry.spawnPoint == null)
            {
                Debug.LogWarning($"[NPCManager] InitialSpawns[{i}] (key: {entry.key}): spawnPoint is null �� skipped.");
                continue;
            }
            SpawnNPC(entry.key, entry.spawnPoint.position, entry.spawnPoint.rotation);
        }
    }

    // ���� Debug Commands ����������������������������������������������������������������������������������������

    private void RegisterDebugCommands()
    {
        if (DebugConsoleManager.Instance == null) return;

        // Usage: npcmgr-spawn <StringKey>   e.g. "npcmgr-spawn artist"
        DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
            "npcmgr-spawn",
            "Spawn an NPC by its string key at the default spawn point. Usage: npcmgr-spawn <StringKey>",
            args =>
            {
                if (args.Length == 0)
                {
                    Debug.LogWarning("[NPCManager] npcmgr-spawn requires a StringKey argument. " +
                                     $"Available: {string.Join(", ", Enum.GetNames(typeof(Key_NPC)))}");
                    return;
                }

                string key = args[0];
                NPCProperty property = ResolvePropertyByString(key);

                if (property == null)
                {
                    Debug.LogWarning($"[NPCManager] npcmgr-spawn: No NPCProperty found for string key '{key}'.");
                    return;
                }

                GameObject result = SpawnNPC(property.EnumKey);
                if (result != null)
                    Debug.Log($"[NPCManager] npcmgr-spawn: Spawned '{property.EnumKey}' successfully.");
            }
        ));
    }

    // ���� Public API ������������������������������������������������������������������������������������������������������������������������

    /// <summary>
    /// Spawns the NPC at the <see cref="defaultSpawnPoint"/> position set in the Inspector.
    /// Falls back to <see cref="Vector3.zero"/> if no spawn point is assigned.
    /// </summary>
    public GameObject SpawnNPC(Key_NPC key)
    {
        Vector3 pos = defaultSpawnPoint != null ? defaultSpawnPoint.position : Vector3.zero;
        return SpawnNPC(key, pos);
    }

    /// <summary>Spawns the NPC at an explicit world position with identity rotation.</summary>
    public GameObject SpawnNPC(Key_NPC key, Vector3 position)
        => SpawnNPC(key, position, Quaternion.identity);

    /// <summary>Spawns the NPC at an explicit world position and rotation.</summary>
    public GameObject SpawnNPC(Key_NPC key, Vector3 position, Quaternion rotation)
    {
        if (key == Key_NPC.None)
        {
            Debug.LogWarning("[NPCManager] Cannot spawn NPC with key 'None'.");
            return null;
        }

        if (IsRegistered(key))
        {
            Debug.LogWarning($"[NPCManager] NPC '{key}' is already spawned. Call DespawnNPC first.");
            return null;
        }

        NPCProperty property = ResolveProperty(key);
        if (property == null)
            return null;

        if (property.prefab == null)
        {
            Debug.LogError($"[NPCManager] NPCProperty for '{key}' has no prefab assigned.");
            return null;
        }

        //// Authorise the injection guard in NPCBehaviour *before* Instantiate.
        //NPCBehaviour.AllowNextSpawn(key);

        GameObject go = Instantiate(property.prefab, position, rotation);
        go.name = $"NPC_{key}";

        NPCBehaviour behaviour = go.GetComponent<NPCBehaviour>();
        if (behaviour == null || behaviour.NpcKey != key)
        {
            Debug.LogError($"[{nameof(NPCManager)}] Prefab '{property.prefab.name}' for '{key}' must contain an {nameof(NPCBehaviour)} with the same NPC key.", this);
            if (behaviour != null)
                UnregisterNPC(behaviour);
            Destroy(go);
            return null;
        }

        RegisterNPC(behaviour);

        if (debugEnabled)
            Debug.Log($"[NPCManager] Spawned '{key}' at {position}. Total registered: {_registeredNPCs.Count}");

        return go;
    }

    /// <summary>
    /// Destroys the spawned NPC and removes it from tracking.
    /// Returns <c>true</c> if successfully despawned.
    /// </summary>
    public bool DespawnNPC(Key_NPC key)
    {
        if (!_registeredNPCs.TryGetValue(key, out GameObject go))
        {
            Debug.LogWarning($"[NPCManager] Cannot despawn '{key}': not currently spawned.");
            return false;
        }

        CapturePersistentRuntimeData(key, go);
        _registeredNPCs.Remove(key);

        if (go != null)
            Destroy(go);

        if (debugEnabled)
            Debug.Log($"[NPCManager] Despawned '{key}'. Total registered: {_registeredNPCs.Count}");

        return true;
    }

    /// <summary>Registers a scene or dynamically spawned NPC under its authored key.</summary>
    public bool RegisterNPC(NPCBehaviour behaviour)
    {
        if (behaviour == null)
            return false;

        return RegisterNPC(behaviour.NpcKey, behaviour.gameObject);
    }

    /// <summary>
    /// Registers an NPC GameObject. Re-registering the same object is safe;
    /// a different object with the same NPC key is rejected.
    /// </summary>
    public bool RegisterNPC(Key_NPC key, GameObject npcGameObject)
    {
        if (key == Key_NPC.None || npcGameObject == null)
        {
            Debug.LogWarning($"[{nameof(NPCManager)}] Cannot register an NPC without a valid key and GameObject.", this);
            return false;
        }

        NPCBehaviour behaviour = npcGameObject.GetComponent<NPCBehaviour>();
        if (behaviour == null || behaviour.NpcKey != key)
        {
            Debug.LogError($"[{nameof(NPCManager)}] '{npcGameObject.name}' cannot register as '{key}' because its {nameof(NPCBehaviour)} is missing or uses a different key.", this);
            return false;
        }

        if (_registeredNPCs.TryGetValue(key, out GameObject existing))
        {
            if (existing == npcGameObject)
            {
                ApplyPersistentRuntimeData(key, npcGameObject);
                return true;
            }

            if (existing == null)
            {
                _registeredNPCs.Remove(key);
            }
            else
            {
                Debug.LogError($"[{nameof(NPCManager)}] Cannot register '{key}' on '{npcGameObject.name}': it is already registered by '{existing.name}'.", this);
                return false;
            }
        }

        _registeredNPCs.Add(key, npcGameObject);
        ApplyPersistentRuntimeData(key, npcGameObject);
        return true;
    }

    /// <summary>Captures data from and removes a registered NPC if it is the current owner of the key.</summary>
    public bool UnregisterNPC(NPCBehaviour behaviour)
    {
        if (behaviour == null)
            return false;

        return UnregisterNPC(behaviour.NpcKey, behaviour.gameObject);
    }

    public bool UnregisterNPC(Key_NPC key, GameObject npcGameObject)
    {
        if (key == Key_NPC.None
            || npcGameObject == null
            || !_registeredNPCs.TryGetValue(key, out GameObject registeredObject)
            || registeredObject != npcGameObject)
        {
            return false;
        }

        CapturePersistentRuntimeData(key, npcGameObject);
        _registeredNPCs.Remove(key);
        return true;
    }

    /// <summary>Returns whether the given NPC key is currently registered in a loaded scene.</summary>
    public bool IsRegistered(Key_NPC key) => _registeredNPCs.ContainsKey(key);

    /// <summary>
    /// Returns the registered GameObject for the given key, or <c>null</c> if it is not loaded.
    /// </summary>
    public GameObject GetRegisteredNPC(Key_NPC key)
    {
        _registeredNPCs.TryGetValue(key, out GameObject go);
        return go;
    }

    [Obsolete("Use IsRegistered instead.")]
    public bool IsSpawned(Key_NPC key) => IsRegistered(key);

    [Obsolete("Use GetRegisteredNPC instead.")]
    public GameObject GetSpawnedNPC(Key_NPC key) => GetRegisteredNPC(key);

    // ���� NPC Progress / Interaction Availability ������������������������������������������������������������������������������������

    /// <summary>Returns long-term status, independent of whether the NPC is currently spawned.</summary>
    public NPCStatus GetNPCStatus(Key_NPC key)
    {
        if (key == Key_NPC.None)
            return NPCStatus.Unrecruited;

        return _npcProgress.TryGetValue(key, out NPCProgressRuntimeState state)
            ? state.Status
            : NPCStatus.Unrecruited;
    }

    public bool IsNPCRecruited(Key_NPC key) => GetNPCStatus(key) == NPCStatus.Recruited;

    /// <summary>
    /// Sets persistent NPC status. Other systems, including Yarn commands and
    /// quest events, should use this instead of coupling recruitment to spawn state.
    /// </summary>
    public bool SetNPCStatus(Key_NPC key, NPCStatus status)
    {
        if (key == Key_NPC.None)
        {
            Debug.LogWarning($"[{nameof(NPCManager)}] Cannot set status for NPC key None.", this);
            return false;
        }

        NPCProgressRuntimeState state = GetOrCreateProgressState(key);
        if (state.Status == status)
            return false;

        state.Status = status;
        OnNPCStatusChanged?.Invoke(key, status);
        return true;
    }

    /// <summary>
    /// Makes an authored interaction visible regardless of its authored default.
    /// NPC Panel still requires the NPC to be recruited.
    /// </summary>
    public bool UnlockInteraction(Key_NPC key, NPCInteractionType interactionType)
    {
        if (!CanOverrideInteraction(key, interactionType))
            return false;

        NPCProgressRuntimeState state = GetOrCreateProgressState(key);
        bool changed = state.LockedInteractions.Remove(interactionType);
        changed |= state.UnlockedInteractions.Add(interactionType);

        if (changed)
            OnNPCInteractionAvailabilityChanged?.Invoke(key, interactionType, IsInteractionAvailable(key, interactionType));

        return changed;
    }

    /// <summary>
    /// Hides an authored interaction until it is unlocked again. This is a
    /// persistent override and does not mutate any ScriptableObject.
    /// </summary>
    public bool LockInteraction(Key_NPC key, NPCInteractionType interactionType)
    {
        if (!CanOverrideInteraction(key, interactionType))
            return false;

        NPCProgressRuntimeState state = GetOrCreateProgressState(key);
        bool changed = state.UnlockedInteractions.Remove(interactionType);
        changed |= state.LockedInteractions.Add(interactionType);

        if (changed)
            OnNPCInteractionAvailabilityChanged?.Invoke(key, interactionType, false);

        return changed;
    }

    /// <summary>Returns the static interaction configuration for this NPC, if registered.</summary>
    public NPCInteractionProperty GetInteractionProperty(Key_NPC key)
    {
        if (key == Key_NPC.None)
            return null;

        NPCInteractionDatabase database = GetNPCInteractionDatabase();
        return database != null ? database.GetByEnum(key) : null;
    }

    /// <summary>
    /// Determines whether the interaction should be shown in the NPC option menu.
    /// Hidden interactions are never exposed as disabled menu entries.
    /// </summary>
    public bool IsInteractionAvailable(Key_NPC key, NPCInteractionType interactionType)
    {
        if (!IsInteractionAuthored(key, interactionType))
            return false;

        if (interactionType == NPCInteractionType.OpenNPCPanel && !IsNPCRecruited(key))
            return false;

        if (_npcProgress.TryGetValue(key, out NPCProgressRuntimeState state))
        {
            if (state.LockedInteractions.Contains(interactionType))
                return false;

            if (state.UnlockedInteractions.Contains(interactionType))
                return true;
        }

        return IsInteractionInitiallyUnlocked(key, interactionType);
    }

    /// <summary>Builds the current visible menu actions in their fixed first-version order.</summary>
    public List<NPCInteractionType> GetAvailableInteractions(Key_NPC key)
    {
        var result = new List<NPCInteractionType>(3);

        if (IsInteractionAvailable(key, NPCInteractionType.Talk))
            result.Add(NPCInteractionType.Talk);
        if (IsInteractionAvailable(key, NPCInteractionType.OpenStore))
            result.Add(NPCInteractionType.OpenStore);
        if (IsInteractionAvailable(key, NPCInteractionType.OpenNPCPanel))
            result.Add(NPCInteractionType.OpenNPCPanel);

        return result;
    }

    /// <summary>
    /// Allocation-free availability query intended for hot paths such as
    /// <see cref="BaseInteractable.CanInteract"/>.
    /// </summary>
    public bool HasAnyAvailableInteraction(Key_NPC key)
    {
        return IsInteractionAvailable(key, NPCInteractionType.Talk)
            || IsInteractionAvailable(key, NPCInteractionType.OpenStore)
            || IsInteractionAvailable(key, NPCInteractionType.OpenNPCPanel);
    }

    /// <summary>Captures only persistent NPC progress; never scene GameObject references.</summary>
    public List<NPCSaveEntry> CaptureSaveEntries()
    {
        CaptureAllRegisteredPersistentRuntimeData();

        var entries = new List<NPCSaveEntry>(_npcProgress.Count);

        foreach (var pair in _npcProgress)
        {
            NPCProgressRuntimeState state = pair.Value;
            if (state == null)
                continue;

            var entry = new NPCSaveEntry
            {
                npcKey = pair.Key,
                npcStatus = state.Status
            };

            entry.unlockedInteractions.AddRange(state.UnlockedInteractions);
            entry.lockedInteractions.AddRange(state.LockedInteractions);
            entry.unlockedInteractions.Sort();
            entry.lockedInteractions.Sort();

            entry.hasRuntimeData = state.HasPersistentRuntimeData;
            if (state.HasPersistentRuntimeData)
                entry.runtimeData = CopyPersistentRuntimeData(state.PersistentRuntimeData);

            entries.Add(entry);
        }

        entries.Sort((left, right) => left.npcKey.CompareTo(right.npcKey));
        return entries;
    }

    /// <summary>Restores persistent progress while leaving spawned scene objects untouched.</summary>
    public void RestoreSaveEntries(List<NPCSaveEntry> entries)
    {
        _npcProgress.Clear();

        if (entries == null)
        {
            RestoreRoomAssignmentsFromProgress();
            return;
        }

        foreach (NPCSaveEntry entry in entries)
        {
            if (entry == null || entry.npcKey == Key_NPC.None)
                continue;

            NPCProgressRuntimeState state = GetOrCreateProgressState(entry.npcKey);
            state.Status = entry.npcStatus;

            AddValidInteractionOverrides(state.UnlockedInteractions, entry.unlockedInteractions);
            AddValidInteractionOverrides(state.LockedInteractions, entry.lockedInteractions);

            if (entry.hasRuntimeData && entry.runtimeData != null)
            {
                state.PersistentRuntimeData = CopyPersistentRuntimeData(entry.runtimeData);
                state.HasPersistentRuntimeData = true;
            }

            // A saved locked state takes precedence if an old save contains both.
            state.UnlockedInteractions.ExceptWith(state.LockedInteractions);
        }

        ApplyPersistentRuntimeDataToRegisteredNPCs();
        RestoreRoomAssignmentsFromProgress();
    }

    // ���� Private helpers ��������������������������������������������������������������������������������������������������������������

    private NPCProperty ResolveProperty(Key_NPC key)
    {
        var db = GetNPCDatabase();
        if (db == null) return null;

        var property = db.GetByEnum(key);
        if (property == null)
            Debug.LogError($"[NPCManager] No NPCProperty found for key '{key}' in NPCDatabase.");

        return property;
    }

    private NPCInteractionDatabase GetNPCInteractionDatabase()
    {
        if (_interactionDatabase != null)
            return _interactionDatabase;

        var databaseManager = PropertyDatabaseManager.Instance;
        if (databaseManager != null)
            _interactionDatabase = databaseManager.GetDatabase<NPCInteractionDatabase>();

        return _interactionDatabase;
    }

    private NPCProgressRuntimeState GetOrCreateProgressState(Key_NPC key)
    {
        if (!_npcProgress.TryGetValue(key, out NPCProgressRuntimeState state))
        {
            state = new NPCProgressRuntimeState();
            _npcProgress.Add(key, state);
        }

        return state;
    }

    private bool CanOverrideInteraction(Key_NPC key, NPCInteractionType interactionType)
    {
        if (key == Key_NPC.None || interactionType == NPCInteractionType.None)
        {
            Debug.LogWarning($"[{nameof(NPCManager)}] A valid NPC key and interaction type are required.", this);
            return false;
        }

        if (!IsInteractionAuthored(key, interactionType))
        {
            Debug.LogWarning($"[{nameof(NPCManager)}] '{interactionType}' is not authored for NPC '{key}'.", this);
            return false;
        }

        return true;
    }

    private bool IsInteractionAuthored(Key_NPC key, NPCInteractionType interactionType)
    {
        NPCInteractionProperty interactionProperty = GetInteractionProperty(key);

        switch (interactionType)
        {
            case NPCInteractionType.Talk:
                return interactionProperty != null && interactionProperty.TalkEnabled;

            case NPCInteractionType.OpenStore:
                return interactionProperty != null && interactionProperty.StoreInventoryProperty != null;

            case NPCInteractionType.OpenNPCPanel:
                // NPC Panel is a universal feature for recruited NPCs, not an
                // option that must be copied into every interaction asset.
                return true;

            default:
                return false;
        }
    }

    private bool IsInteractionInitiallyUnlocked(Key_NPC key, NPCInteractionType interactionType)
    {
        switch (interactionType)
        {
            case NPCInteractionType.Talk:
            case NPCInteractionType.OpenNPCPanel:
                return true;

            case NPCInteractionType.OpenStore:
            {
                NPCInteractionProperty interactionProperty = GetInteractionProperty(key);
                return interactionProperty != null && interactionProperty.StoreInitiallyUnlocked;
            }

            default:
                return false;
        }
    }

    private static void AddValidInteractionOverrides(
        HashSet<NPCInteractionType> target,
        List<NPCInteractionType> source)
    {
        if (source == null)
            return;

        foreach (NPCInteractionType interactionType in source)
        {
            if (interactionType != NPCInteractionType.None)
                target.Add(interactionType);
        }
    }

    private void CaptureAllRegisteredPersistentRuntimeData()
    {
        foreach (var pair in _registeredNPCs)
            CapturePersistentRuntimeData(pair.Key, pair.Value);
    }

    private void CapturePersistentRuntimeData(Key_NPC key, GameObject npcGameObject)
    {
        if (key == Key_NPC.None || npcGameObject == null)
            return;

        NPCBehaviour behaviour = npcGameObject.GetComponent<NPCBehaviour>();
        if (behaviour == null)
            return;

        NPCProgressRuntimeState state = GetOrCreateProgressState(key);
        state.PersistentRuntimeData = behaviour.CapturePersistentRuntimeData();
        state.HasPersistentRuntimeData = true;
    }

    private void ApplyPersistentRuntimeDataToRegisteredNPCs()
    {
        foreach (var pair in _registeredNPCs)
            ApplyPersistentRuntimeData(pair.Key, pair.Value);
    }

    private void ApplyPersistentRuntimeData(Key_NPC key, GameObject npcGameObject)
    {
        if (npcGameObject == null
            || !_npcProgress.TryGetValue(key, out NPCProgressRuntimeState state)
            || !state.HasPersistentRuntimeData
            || state.PersistentRuntimeData == null)
            return;

        NPCBehaviour behaviour = npcGameObject.GetComponent<NPCBehaviour>();
        behaviour?.RestorePersistentRuntimeData(state.PersistentRuntimeData);
    }

    private void RestoreRoomAssignmentsFromProgress()
    {
        if (NPCRoomAssignmentManager.Instance == null)
            return;

        var assignments = new List<NPCRoomAssignmentSnapshot>();
        foreach (var pair in _npcProgress)
        {
            NPCProgressRuntimeState state = pair.Value;
            if (state == null
                || !state.HasPersistentRuntimeData
                || state.PersistentRuntimeData == null
                || !state.PersistentRuntimeData.hasRoom)
                continue;

            assignments.Add(new NPCRoomAssignmentSnapshot(
                pair.Key,
                state.PersistentRuntimeData.assignedRoomStableId));
        }

        NPCRoomAssignmentManager.Instance.RestoreAssignments(assignments);
    }

    private static NPCPersistentRuntimeData CopyPersistentRuntimeData(NPCPersistentRuntimeData source)
    {
        if (source == null)
            return new NPCPersistentRuntimeData();

        return new NPCPersistentRuntimeData
        {
            dailyInteractionAffinity = source.dailyInteractionAffinity,
            familiarityAffinity = source.familiarityAffinity,
            lastInteractionDay = source.lastInteractionDay,
            interactedToday = source.interactedToday,
            hasRoom = source.hasRoom,
            assignedRoomStableId = source.assignedRoomStableId
        };
    }

    private NPCProperty ResolvePropertyByString(string stringKey)
    {
        var db = GetNPCDatabase();
        if (db == null) return null;

        var property = db.GetByString(stringKey);
        if (property == null)
            Debug.LogWarning($"[NPCManager] No NPCProperty found for string key '{stringKey}' in NPCDatabase.");

        return property;
    }

    private NPCDatabase GetNPCDatabase()
    {
        var dbMgr = PropertyDatabaseManager.Instance;
        if (dbMgr == null)
        {
            Debug.LogError("[NPCManager] PropertyDatabaseManager instance not found.");
            return null;
        }

        var db = dbMgr.GetDatabase<NPCDatabase>();
        if (db == null)
            Debug.LogError("[NPCManager] NPCDatabase not registered in PropertyDatabaseManager.");

        return db;
    }

    // ���� Daily Income ��������������������������������������������������������������������������������������������

    private void HandleNewDay(int day)
    {
        float total = CollectDailyIncome();

        if (EconomyManager.Instance != null)
        {
            EconomyManager.Instance.AddCurrency(CurrencyType.Credits, total);

            if (debugEnabled)
                Debug.Log($"[NPCManager] Day {day} income collected: {total:F1} Credits from {_registeredNPCs.Count} NPC(s).");
        }
        else
        {
            Debug.LogWarning("[NPCManager] EconomyManager instance not found �� income was not deposited.");
        }
    }

    /// <summary>
    /// Sums the daily income from all currently spawned NPCs and returns the total.
    /// </summary>
    private float CollectDailyIncome()
    {
        float total = 0f;

        foreach (var pair in _registeredNPCs)
        {
            if (pair.Value == null) continue;

            var behaviour = pair.Value.GetComponent<NPCBehaviour>();
            if (behaviour != null)
                total += behaviour.CalculateDailyIncome();
        }

        return total;
    }
}
