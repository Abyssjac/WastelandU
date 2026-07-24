using UnityEngine;

/// <summary>
/// Bridges QuestManager runtime state to the project's IGameSavable save pipeline.
/// Attach beside QuestManager on the persistent quest system GameObject.
/// </summary>
[DisallowMultipleComponent]
public class QuestSaveAdapter : MonoBehaviour, IGameSavable
{
    [SerializeField] private QuestManager questManager;

    private bool registered;

    public string SaveKey => "quests";
    public int SaveVersion => 1;

    // Quest state must restore after inventory data. The existing build adapter uses 0.
    public int LoadOrder => 100;

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
        QuestManager manager = ResolveQuestManager();
        QuestSaveData data = new QuestSaveData();

        if (manager != null)
            data.entries = manager.CaptureSaveEntries();

        return JsonUtility.ToJson(data, prettyPrint: true);
    }

    public void RestoreSaveJson(string json)
    {
        QuestManager manager = ResolveQuestManager();
        if (manager == null)
        {
            Debug.LogWarning($"[{nameof(QuestSaveAdapter)}] QuestManager is not available during restore.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            manager.RestoreSaveEntries(null);
            return;
        }

        QuestSaveData data = JsonUtility.FromJson<QuestSaveData>(json);
        if (data == null)
        {
            Debug.LogError($"[{nameof(QuestSaveAdapter)}] Failed to deserialize quest save data.", this);
            return;
        }

        manager.RestoreSaveEntries(data.entries);
    }

    public void OnNoSaveData()
    {
        ResolveQuestManager()?.RestoreSaveEntries(null);
    }

    private void TryRegister()
    {
        if (registered || GameSaveManager.Instance == null)
            return;

        GameSaveManager.Instance.RegisterSavable(this);
        registered = true;
    }

    private QuestManager ResolveQuestManager()
    {
        if (questManager == null)
            questManager = GetComponent<QuestManager>();

        if (questManager == null)
            questManager = QuestManager.Instance;

        return questManager;
    }
}
