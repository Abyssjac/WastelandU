using System.Collections.Generic;
using System;
using JackyUtility;
using UnityEngine;

/// <summary>
/// Runtime manager for all spawned NPCs.
/// Singleton ¡ª place on a DontDestroyOnLoad GameObject.
///
/// All NPCs must be created via <see cref="SpawnNPC(Key_NPC)"/> or
/// <see cref="SpawnNPC(Key_NPC, Vector3)"/>. Direct Instantiation is
/// blocked by the injection guard in <see cref="NPCBehaviour"/>.
/// </summary>
public class NPCManager : MonoBehaviour, IDebuggable
{
    // ©¤©¤ Singleton ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    public static NPCManager Instance { get; private set; }

    // ©¤©¤ Inspector ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Header("Spawn")]
    [Tooltip("Fallback spawn position used when SpawnNPC(key) is called without an explicit position.")]
    [SerializeField] private Transform defaultSpawnPoint;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled;

    // ©¤©¤ IDebuggable ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    public string DebugId      => "npcmanager";
    public bool   DebugEnabled { get => debugEnabled; set => debugEnabled = value; }

    // ©¤©¤ State ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private readonly Dictionary<Key_NPC, GameObject> _spawnedNPCs =
        new Dictionary<Key_NPC, GameObject>();

    /// <summary>Read-only view of all currently spawned NPCs keyed by <see cref="Key_NPC"/>.</summary>
    public IReadOnlyDictionary<Key_NPC, GameObject> SpawnedNPCs => _spawnedNPCs;

    // ©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
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

    // ©¤©¤ Debug Commands ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

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

    // ©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Spawns the NPC at the <see cref="defaultSpawnPoint"/> position set in the Inspector.
    /// Falls back to <see cref="Vector3.zero"/> if no spawn point is assigned.
    /// </summary>
    public GameObject SpawnNPC(Key_NPC key)
    {
        Vector3 pos = defaultSpawnPoint != null ? defaultSpawnPoint.position : Vector3.zero;
        return SpawnNPC(key, pos);
    }

    /// <summary>Spawns the NPC at an explicit world position.</summary>
    public GameObject SpawnNPC(Key_NPC key, Vector3 position)
    {
        if (key == Key_NPC.None)
        {
            Debug.LogWarning("[NPCManager] Cannot spawn NPC with key 'None'.");
            return null;
        }

        if (_spawnedNPCs.ContainsKey(key))
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

        GameObject go = Instantiate(property.prefab, position, Quaternion.identity);
        go.name = $"NPC_{key}";

        _spawnedNPCs[key] = go;

        if (debugEnabled)
            Debug.Log($"[NPCManager] Spawned '{key}' at {position}. Total spawned: {_spawnedNPCs.Count}");

        return go;
    }

    /// <summary>
    /// Destroys the spawned NPC and removes it from tracking.
    /// Returns <c>true</c> if successfully despawned.
    /// </summary>
    public bool DespawnNPC(Key_NPC key)
    {
        if (!_spawnedNPCs.TryGetValue(key, out GameObject go))
        {
            Debug.LogWarning($"[NPCManager] Cannot despawn '{key}': not currently spawned.");
            return false;
        }

        _spawnedNPCs.Remove(key);

        if (go != null)
            Destroy(go);

        if (debugEnabled)
            Debug.Log($"[NPCManager] Despawned '{key}'. Total spawned: {_spawnedNPCs.Count}");

        return true;
    }

    /// <summary>Returns whether the given NPC key is currently spawned in the scene.</summary>
    public bool IsSpawned(Key_NPC key) => _spawnedNPCs.ContainsKey(key);

    /// <summary>
    /// Returns the spawned GameObject for the given key, or <c>null</c> if not spawned.
    /// </summary>
    public GameObject GetSpawnedNPC(Key_NPC key)
    {
        _spawnedNPCs.TryGetValue(key, out GameObject go);
        return go;
    }

    // ©¤©¤ Private helpers ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private NPCProperty ResolveProperty(Key_NPC key)
    {
        var db = GetNPCDatabase();
        if (db == null) return null;

        var property = db.GetByEnum(key);
        if (property == null)
            Debug.LogError($"[NPCManager] No NPCProperty found for key '{key}' in NPCDatabase.");

        return property;
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
}
