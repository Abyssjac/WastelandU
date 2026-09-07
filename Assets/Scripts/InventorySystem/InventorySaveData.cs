using System;
using System.Collections.Generic;

/// <summary>
/// Serializable storage for this project's physical inventory slots.
/// The game-side save adapter owns registration with the save pipeline;
/// this type is project-side because slots use <see cref="Key_ItemDefinitionPP"/>.
/// </summary>
[Serializable]
public class InventorySaveData
{
    public int slotCount;
    public List<InventorySlotSaveEntry> slots = new List<InventorySlotSaveEntry>();
}

/// <summary>One exact physical inventory slot in an <see cref="InventorySaveData"/> payload.</summary>
[Serializable]
public class InventorySlotSaveEntry
{
    public int slotIndex;
    public Key_ItemDefinitionPP itemKey = Key_ItemDefinitionPP.None;
    public int itemCount;
}
