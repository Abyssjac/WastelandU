using UnityEngine;

/// <summary>
/// Bridges <see cref="InventoryManager"/> to the shared game-save pipeline.
/// Attach beside InventoryManager on its persistent GameObject.
/// </summary>
[DisallowMultipleComponent]
public class InventorySaveAdapter : MonoBehaviour, IGameSavable
{
    [SerializeField] private InventoryManager _inventoryManager;

    private bool _registered;

    public string SaveKey => "inventory";
    public int SaveVersion => 1;
    public int LoadOrder => 10;

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
        InventoryManager inventoryManager = ResolveInventoryManager();
        InventorySaveData data = inventoryManager != null
            ? inventoryManager.CaptureSaveData()
            : new InventorySaveData();

        return JsonUtility.ToJson(data, prettyPrint: true);
    }

    public void RestoreSaveJson(string json)
    {
        InventoryManager inventoryManager = ResolveInventoryManager();
        if (inventoryManager == null)
        {
            Debug.LogWarning($"[{nameof(InventorySaveAdapter)}] {nameof(InventoryManager)} is unavailable during restore.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(json))
            return;

        InventorySaveData data = JsonUtility.FromJson<InventorySaveData>(json);
        if (data == null)
        {
            Debug.LogError($"[{nameof(InventorySaveAdapter)}] Failed to deserialize inventory save data.", this);
            return;
        }

        inventoryManager.RestoreSaveData(data);
    }

    /// <summary>
    /// New games retain the InventoryManager's Inspector-authored initial layout.
    /// </summary>
    public void OnNoSaveData()
    {
    }

    private void TryRegister()
    {
        if (_registered || GameSaveManager.Instance == null)
            return;

        GameSaveManager.Instance.RegisterSavable(this);
        _registered = true;
    }

    private InventoryManager ResolveInventoryManager()
    {
        if (_inventoryManager == null)
            _inventoryManager = GetComponent<InventoryManager>();

        if (_inventoryManager == null)
            _inventoryManager = InventoryManager.Instance;

        return _inventoryManager;
    }
}
