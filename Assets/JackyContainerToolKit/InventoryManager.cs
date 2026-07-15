using System;
using JackyUtility;
using UnityEngine;

[DisallowMultipleComponent]
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Inventory")]
    [Min(1)]
    [SerializeField] private int initialSlotCount = 60;
    [SerializeField] private InventoryContainer inventory = new InventoryContainer();

    private ItemDefinitionDatabase itemDatabase;
    private bool missingDatabaseLogged;

    public InventoryContainer Inventory => inventory;
    public ItemDefinitionDatabase ItemDatabase => itemDatabase;
    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureInventory();
        inventory.OnContainerChanged += HandleInventoryChanged;
    }

    private void Start()
    {
        TryInitializeDatabase();
    }

    private void OnDestroy()
    {
        if (inventory != null)
            inventory.OnContainerChanged -= HandleInventoryChanged;

        if (Instance == this)
            Instance = null;
    }

    public int GetCount(Key_ItemDefinitionPP itemKey)
    {
        return inventory != null ? inventory.GetItemCountByEnum(itemKey) : 0;
    }

    public bool HasItem(Key_ItemDefinitionPP itemKey, int count)
    {
        return count > 0 && GetCount(itemKey) >= count;
    }

    public bool CanAddItem(Key_ItemDefinitionPP itemKey, int count, out string failReason)
    {
        if (!TryGetItemDefinition(itemKey, out _, out failReason))
            return false;

        return inventory.CanAddItem(itemKey, count, out failReason);
    }

    public bool TryAddItem(Key_ItemDefinitionPP itemKey, int count, out string failReason)
    {
        if (!TryGetItemDefinition(itemKey, out _, out failReason))
            return false;

        return inventory.TryAddItem(itemKey, count, out failReason);
    }

    public bool TryRemoveItem(Key_ItemDefinitionPP itemKey, int count, out string failReason)
    {
        EnsureInventory();
        return inventory.TryRemoveItem(itemKey, count, out failReason);
    }

    public bool TryGetItemDefinition(Key_ItemDefinitionPP itemKey, out ItemDefinitionSO item, out string failReason)
    {
        item = null;
        failReason = null;

        if (itemKey == Key_ItemDefinitionPP.None)
        {
            failReason = "Item key is None.";
            return false;
        }

        if (!TryInitializeDatabase())
        {
            failReason = "ItemDefinitionDatabase is not ready.";
            return false;
        }

        item = itemDatabase.GetByEnum(itemKey);
        if (item != null)
            return true;

        failReason = "No ItemDefinitionSO found for key '" + itemKey + "'.";
        return false;
    }

    private void EnsureInventory()
    {
        if (inventory == null)
            inventory = new InventoryContainer();

        inventory.SetMaxSlots(Mathf.Max(1, initialSlotCount));
        if (itemDatabase != null)
            inventory.SetItemDatabase(itemDatabase);
    }

    private bool TryInitializeDatabase()
    {
        if (itemDatabase != null)
            return true;

        PropertyDatabaseManager databaseManager = PropertyDatabaseManager.Instance;
        if (databaseManager == null)
        {
            LogMissingDatabase("PropertyDatabaseManager is not available.");
            return false;
        }

        itemDatabase = databaseManager.GetDatabase<ItemDefinitionDatabase>();
        if (itemDatabase == null)
        {
            LogMissingDatabase("ItemDefinitionDatabase is not registered.");
            return false;
        }

        inventory.SetItemDatabase(itemDatabase);
        missingDatabaseLogged = false;
        return true;
    }

    private void HandleInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    private void LogMissingDatabase(string reason)
    {
        if (missingDatabaseLogged)
            return;

        missingDatabaseLogged = true;
        Debug.LogWarning("[" + nameof(InventoryManager) + "] " + reason, this);
    }
}
