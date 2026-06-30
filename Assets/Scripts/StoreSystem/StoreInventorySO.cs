using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines a single entry in a store inventory.
/// Each entry corresponds to one fixed slot in the store UI.
/// </summary>
[Serializable]
public struct StoreItemEntry
{
    [Tooltip("The buildable item this slot sells.")]
    public Key_BuildablePP itemKey;

    [Tooltip("Starting stock count for this item.")]
    public int initialStock;

    [Tooltip("When true this slot is permanently locked and cannot be purchased.")]
    public bool isLocked;
}

/// <summary>
/// ScriptableObject that describes the full inventory of a store.
/// The number of entries determines the fixed slot count shown in the UI.
///
/// Create via: Assets > Create > StoreSystem > StoreInventorySO
/// </summary>
[CreateAssetMenu(fileName = "StoreInventory_", menuName = "StoreSystem/StoreInventorySO")]
public class StoreInventorySO : ScriptableObject
{
    [Tooltip("Fixed store slots. Runtime store stock is copied from this container.")]
    [SerializeField] private StoreContainer storeContainer = new StoreContainer();

    [SerializeField, HideInInspector] private List<StoreItemEntry> entries = new List<StoreItemEntry>();

    /// <summary>Read-only view of all store entries.</summary>
    public IReadOnlyList<StoreItemEntry> Entries => entries;

    /// <summary>Total number of slots this store shows (fixed, never changes at runtime).</summary>
    public int SlotCount
    {
        get
        {
            EnsureContainer();
            return storeContainer.MaxSlots;
        }
    }

    public StoreContainer StoreContainer
    {
        get
        {
            EnsureContainer();
            return storeContainer;
        }
    }

    public StoreSlot GetSlot(int index)
    {
        EnsureContainer();
        return storeContainer.GetSlotByIndex(index);
    }

    public StoreContainer CreateRuntimeContainer()
    {
        EnsureContainer();

        StoreContainer runtime = new StoreContainer(storeContainer.MaxSlots)
        {
            UseMaxStack = storeContainer.UseMaxStack,
            MaxStackCount = storeContainer.MaxStackCount
        };

        for (int i = 0; i < storeContainer.MaxSlots; i++)
        {
            StoreSlot source = storeContainer.GetSlotByIndex(i);
            if (source == null) continue;

            runtime.TrySetSlotAtIndex(i, source.ItemEnum, Mathf.Max(0, source.initialStock), out _);

            StoreSlot target = runtime.GetSlotByIndex(i);
            if (target == null) continue;

            target.initialStock = Mathf.Max(0, source.initialStock);
            target.isLocked = source.isLocked;
        }

        return runtime;
    }

    private void OnValidate()
    {
        EnsureContainer();
    }

    private void EnsureContainer()
    {
        if (storeContainer == null)
            storeContainer = new StoreContainer();

        storeContainer.EnsureInitialized();

        if (storeContainer.MaxSlots == 0 && entries != null && entries.Count > 0)
            MigrateLegacyEntries();
    }

    private void MigrateLegacyEntries()
    {
        storeContainer = new StoreContainer(entries.Count);

        for (int i = 0; i < entries.Count; i++)
        {
            StoreItemEntry entry = entries[i];
            storeContainer.TrySetSlotAtIndex(i, entry.itemKey, Mathf.Max(0, entry.initialStock), out _);

            StoreSlot slot = storeContainer.GetSlotByIndex(i);
            if (slot == null) continue;

            slot.initialStock = Mathf.Max(0, entry.initialStock);
            slot.isLocked = entry.isLocked;
        }
    }
}
