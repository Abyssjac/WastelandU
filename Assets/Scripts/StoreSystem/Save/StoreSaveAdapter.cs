using UnityEngine;

/// <summary>
/// Bridges mutable store inventory state to the shared game-save pipeline.
/// Attach beside <see cref="StoreManager"/> on the persistent StoreManager object.
/// </summary>
[DisallowMultipleComponent]
public class StoreSaveAdapter : MonoBehaviour, IGameSavable
{
    [SerializeField] private StoreManager storeManager;

    private bool _registered;

    public string SaveKey => "stores";
    public int SaveVersion => 1;
    public int LoadOrder => 105;

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
        if (!_registered || GameSaveManager.Instance == null)
            return;

        GameSaveManager.Instance.UnregisterSavable(this);
        _registered = false;
    }

    public string CaptureSaveJson()
    {
        StoreManager manager = ResolveStoreManager();
        StoreSaveData data = new StoreSaveData
        {
            entries = manager != null ? manager.CaptureSaveEntries() : new System.Collections.Generic.List<StoreSaveEntry>()
        };

        return JsonUtility.ToJson(data, prettyPrint: true);
    }

    public void RestoreSaveJson(string json)
    {
        StoreManager manager = ResolveStoreManager();
        if (manager == null)
        {
            Debug.LogWarning($"[{nameof(StoreSaveAdapter)}] {nameof(StoreManager)} is unavailable during restore.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            manager.RestoreSaveEntries(null);
            return;
        }

        StoreSaveData data = JsonUtility.FromJson<StoreSaveData>(json);
        if (data == null)
        {
            Debug.LogError($"[{nameof(StoreSaveAdapter)}] Failed to deserialize store save data.", this);
            return;
        }

        manager.RestoreSaveEntries(data.entries);
    }

    public void OnNoSaveData()
    {
        ResolveStoreManager()?.RestoreSaveEntries(null);
    }

    private void TryRegister()
    {
        if (_registered || GameSaveManager.Instance == null)
            return;

        GameSaveManager.Instance.RegisterSavable(this);
        _registered = true;
    }

    private StoreManager ResolveStoreManager()
    {
        if (storeManager == null)
            storeManager = GetComponent<StoreManager>();

        if (storeManager == null)
            storeManager = StoreManager.Instance;

        return storeManager;
    }
}
