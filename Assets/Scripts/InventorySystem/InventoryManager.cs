using System;
using System.Collections.Generic;
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
    private InventorySaveData _pendingRestoreData;

    public InventoryContainer Inventory => inventory;
    public ItemDefinitionDatabase ItemDatabase => itemDatabase;
    /// <summary>
    /// Fired once after a successful InventoryManager add or remove operation.
    /// A positive delta means the item was gained; a negative delta means it was removed.
    /// </summary>
    public event Action<Key_ItemDefinitionPP, int> OnInventoryChanged;

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
    }

    private void Start()
    {
        if (TryInitializeDatabase())
            ApplyPendingRestoreData();
    }

    private void OnDestroy()
    {
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

        bool added = inventory.TryAddItem(itemKey, count, out failReason);
        if (added)
            RaiseInventoryChanged(itemKey, count);

        return added;
    }

    /// <summary>
    /// Adds as much of an item as the inventory can hold. Any quantity that cannot fit is returned
    /// through <paramref name="excess"/>. This is intended for systems such as quest rewards that
    /// should complete even if an unexpected inventory overflow occurs.
    /// </summary>
    public bool TryAddReturnExcess(Key_ItemDefinitionPP itemKey, int count, out int excess, out string failReason)
    {
        excess = count;

        if (!TryGetItemDefinition(itemKey, out _, out failReason))
            return false;

        if (count <= 0)
        {
            excess = 0;
            failReason = "Add count must be greater than zero.";
            return false;
        }

        EnsureInventory();
        bool anyAdded = inventory.AddItemReturnExcess(itemKey, count, out excess);
        failReason = excess > 0
            ? $"Inventory overflowed by {excess} item(s)."
            : string.Empty;

        int actualAdded = count - excess;
        if (actualAdded > 0)
            RaiseInventoryChanged(itemKey, actualAdded);

        return anyAdded;
    }

    public bool TryRemoveItem(Key_ItemDefinitionPP itemKey, int count, out string failReason)
    {
        EnsureInventory();
        bool removed = inventory.TryRemoveItem(itemKey, count, out failReason);
        if (removed)
            RaiseInventoryChanged(itemKey, -count);

        return removed;
    }

    /// <summary>
    /// Removes items from one exact physical inventory slot. This is used by
    /// Store Sell mode so the slot selected by the player is the slot that
    /// actually loses the item.
    /// </summary>
    public bool TryRemoveItemAtSlot(int slotIndex, Key_ItemDefinitionPP expectedItemKey, int count, out string failReason)
    {
        EnsureInventory();
        failReason = null;

        if (expectedItemKey == Key_ItemDefinitionPP.None)
        {
            failReason = "Expected item key is None.";
            return false;
        }

        InventorySlot slot = inventory.GetSlotByIndex(slotIndex);
        if (slot == null || slot.IsEmpty)
        {
            failReason = "Inventory slot " + slotIndex + " is empty.";
            return false;
        }

        if (slot.ItemEnum != expectedItemKey)
        {
            failReason = "Inventory slot " + slotIndex + " no longer contains " + expectedItemKey + ".";
            return false;
        }

        if (!inventory.TryRemoveCountAtIndex(slotIndex, count, out failReason))
            return false;

        RaiseInventoryChanged(expectedItemKey, -count);
        return true;
    }

    /// <summary>
    /// Captures the complete physical inventory layout. Empty entries are kept
    /// so stacks remain in the same slots after a save is restored.
    /// </summary>
    public InventorySaveData CaptureSaveData()
    {
        EnsureInventory();

        var data = new InventorySaveData
        {
            slotCount = inventory.MaxSlots,
            slots = new List<InventorySlotSaveEntry>(inventory.MaxSlots)
        };

        for (int i = 0; i < inventory.MaxSlots; i++)
        {
            InventorySlot slot = inventory.GetSlotByIndex(i);
            data.slots.Add(new InventorySlotSaveEntry
            {
                slotIndex = i,
                itemKey = slot != null ? slot.ItemEnum : Key_ItemDefinitionPP.None,
                itemCount = slot != null ? Mathf.Max(0, slot.ItemCount) : 0
            });
        }

        return data;
    }

    /// <summary>
    /// Restores the complete physical inventory layout from save data. If item
    /// definitions are not ready yet, restoration is deferred until Start.
    /// </summary>
    public void RestoreSaveData(InventorySaveData data)
    {
        if (data == null)
            return;

        if (!TryInitializeDatabase())
        {
            _pendingRestoreData = data;
            return;
        }

        ApplyRestoreData(data);
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

    private void ApplyPendingRestoreData()
    {
        if (_pendingRestoreData == null)
            return;

        InventorySaveData pendingData = _pendingRestoreData;
        _pendingRestoreData = null;
        ApplyRestoreData(pendingData);
    }

    private void ApplyRestoreData(InventorySaveData data)
    {
        if (data == null)
            return;

        EnsureInventory();
        initialSlotCount = Mathf.Max(1, data.slotCount);
        inventory.SetMaxSlots(initialSlotCount);

        for (int i = 0; i < inventory.MaxSlots; i++)
            inventory.EmptySlotAtIndex(i);

        if (data.slots == null)
            return;

        for (int i = 0; i < data.slots.Count; i++)
        {
            InventorySlotSaveEntry entry = data.slots[i];
            if (entry == null
                || entry.slotIndex < 0
                || entry.slotIndex >= inventory.MaxSlots
                || entry.itemKey == Key_ItemDefinitionPP.None
                || entry.itemCount <= 0)
            {
                continue;
            }

            if (!TryGetItemDefinition(entry.itemKey, out _, out string definitionFailReason))
            {
                Debug.LogWarning("[InventoryManager] Skipped saved item '" + entry.itemKey + "': " + definitionFailReason, this);
                continue;
            }

            if (!inventory.TrySetSlotAtIndex(entry.slotIndex, entry.itemKey, entry.itemCount, out string setFailReason))
                Debug.LogWarning("[InventoryManager] Could not restore slot " + entry.slotIndex + ": " + setFailReason, this);
        }
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

    private void RaiseInventoryChanged(Key_ItemDefinitionPP itemKey, int delta)
    {
        if (itemKey == Key_ItemDefinitionPP.None || delta == 0)
            return;

        OnInventoryChanged?.Invoke(itemKey, delta);
    }

    private void LogMissingDatabase(string reason)
    {
        if (missingDatabaseLogged)
            return;

        missingDatabaseLogged = true;
        Debug.LogWarning("[" + nameof(InventoryManager) + "] " + reason, this);
    }
}
