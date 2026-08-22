using System;
using System.Collections.Generic;

/// <summary>Top-level data for all persistent store runtime state.</summary>
[Serializable]
public class StoreSaveData
{
    public List<StoreSaveEntry> entries = new List<StoreSaveEntry>();
}

/// <summary>Persistent runtime state for one StoreInventoryProperty.</summary>
[Serializable]
public class StoreSaveEntry
{
    public Key_StoreInventory storeKey = Key_StoreInventory.None;
    public List<StoreItemSaveEntry> items = new List<StoreItemSaveEntry>();
}

/// <summary>
/// Persistent mutable state for one store item. ItemDefinition key is used
/// because it is the actual identity authored in StoreSlot.
/// </summary>
[Serializable]
public class StoreItemSaveEntry
{
    public Key_ItemDefinitionPP itemKey = Key_ItemDefinitionPP.None;
    public int remainingCount;
    public bool isLocked;
}
