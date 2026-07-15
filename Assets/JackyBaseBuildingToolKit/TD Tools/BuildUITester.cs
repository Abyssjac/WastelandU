using System;
using JackyUtility;
using UnityEngine;

/// <summary>
/// Temporary keyboard helper that grants ItemDefinitions to InventoryManager.
/// </summary>
public class BuildUITester : MonoBehaviour
{
    [Serializable]
    public struct ItemAddSlot
    {
        public Key_ItemDefinitionPP itemKey;
        [Min(1)] public int count;
    }

    [SerializeField] private ItemAddSlot[] addSlots = Array.Empty<ItemAddSlot>();
    [SerializeField, Min(1)] private int buildAddCount = 1;

    private ItemDefinitionDatabase itemDatabase;

    private void Start()
    {
        if (InventoryManager.Instance == null)
        {
            Debug.LogWarning("[BuildUITester] InventoryManager is not ready.");
            enabled = false;
            return;
        }

        PropertyDatabaseManager databaseManager = PropertyDatabaseManager.Instance;
        itemDatabase = databaseManager != null ? databaseManager.GetDatabase<ItemDefinitionDatabase>() : null;
        if (itemDatabase == null)
        {
            Debug.LogWarning("[BuildUITester] ItemDefinitionDatabase is not ready.");
            enabled = false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            AddAllBuildableItems();
            return;
        }

        for (int i = 0; i < addSlots.Length && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                TryAddItem(addSlots[i]);
                return;
            }
        }
    }

    private void AddAllBuildableItems()
    {
        if (itemDatabase == null || InventoryManager.Instance == null)
            return;

        foreach (ItemDefinitionSO item in itemDatabase.Entries)
        {
            if (item == null || !item.IsBuildable)
                continue;

            InventoryManager.Instance.TryAddItem(item.EnumKey, buildAddCount, out string reason);
            if (!string.IsNullOrEmpty(reason))
                Debug.LogWarning("[BuildUITester] Could not add " + item.EnumKey + ": " + reason);
        }
    }

    private void TryAddItem(ItemAddSlot slot)
    {
        if (slot.itemKey == Key_ItemDefinitionPP.None || InventoryManager.Instance == null)
            return;

        int count = Mathf.Max(1, slot.count);
        if (!InventoryManager.Instance.TryAddItem(slot.itemKey, count, out string reason))
            Debug.LogWarning("[BuildUITester] Could not add " + slot.itemKey + ": " + reason);
    }
}