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
    [Tooltip("All slots this store exposes. Order is preserved in the UI.")]
    [SerializeField] private List<StoreItemEntry> entries = new List<StoreItemEntry>();

    /// <summary>Read-only view of all store entries.</summary>
    public IReadOnlyList<StoreItemEntry> Entries => entries;

    /// <summary>Total number of slots this store shows (fixed, never changes at runtime).</summary>
    public int SlotCount => entries.Count;
}
