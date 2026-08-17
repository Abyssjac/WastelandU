using UnityEngine;

/// <summary>
/// Bridges NPC long-term progress to the shared game-save pipeline.
/// Attach beside <see cref="NPCManager"/> on the persistent NPC manager object.
/// </summary>
[DisallowMultipleComponent]
public class NPCSaveAdapter : MonoBehaviour, IGameSavable
{
    [SerializeField] private NPCManager npcManager;

    private bool registered;

    public string SaveKey => "npcs";
    public int SaveVersion => 1;
    public int LoadOrder => 110;

    private void OnEnable()
    {
        TryRegister();
    }

    private void Start()
    {
        TryRegister();
    }

    private void OnDisable()
    {
        if (!registered || GameSaveManager.Instance == null)
            return;

        GameSaveManager.Instance.UnregisterSavable(this);
        registered = false;
    }

    public string CaptureSaveJson()
    {
        NPCSaveData data = new NPCSaveData();
        NPCManager manager = ResolveNPCManager();
        if (manager != null)
            data.entries = manager.CaptureSaveEntries();

        return JsonUtility.ToJson(data, prettyPrint: true);
    }

    public void RestoreSaveJson(string json)
    {
        NPCManager manager = ResolveNPCManager();
        if (manager == null)
        {
            Debug.LogWarning($"[{nameof(NPCSaveAdapter)}] {nameof(NPCManager)} is unavailable during restore.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            manager.RestoreSaveEntries(null);
            return;
        }

        NPCSaveData data = JsonUtility.FromJson<NPCSaveData>(json);
        if (data == null)
        {
            Debug.LogError($"[{nameof(NPCSaveAdapter)}] Failed to deserialize NPC save data.", this);
            return;
        }

        manager.RestoreSaveEntries(data.entries);
    }

    public void OnNoSaveData()
    {
        ResolveNPCManager()?.RestoreSaveEntries(null);
    }

    private void TryRegister()
    {
        if (registered || GameSaveManager.Instance == null)
            return;

        GameSaveManager.Instance.RegisterSavable(this);
        registered = true;
    }

    private NPCManager ResolveNPCManager()
    {
        if (npcManager == null)
            npcManager = GetComponent<NPCManager>();

        if (npcManager == null)
            npcManager = NPCManager.Instance;

        return npcManager;
    }
}
