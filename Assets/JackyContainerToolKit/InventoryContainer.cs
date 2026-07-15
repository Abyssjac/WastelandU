using System;
using JackyUtility;

[Serializable]
public class InventoryContainer : SContainer<InventorySlot, Key_ItemDefinitionPP>
{
    [NonSerialized] private ItemDefinitionDatabase itemDatabase;

    public InventoryContainer() : base()
    {
    }

    public InventoryContainer(int slotCount) : base(slotCount)
    {
    }

    public void SetItemDatabase(ItemDefinitionDatabase database)
    {
        itemDatabase = database;
    }

    protected override int GetMaxStackFor(Key_ItemDefinitionPP itemKey)
    {
        ItemDefinitionSO item = itemDatabase != null ? itemDatabase.GetByEnum(itemKey) : null;
        return item != null ? item.StackCount : 1;
    }
}
