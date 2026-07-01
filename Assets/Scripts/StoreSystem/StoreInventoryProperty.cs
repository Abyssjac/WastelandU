using JackyUtility;
using UnityEngine;
using System;
public enum Key_StoreInventory
{
    None = 0,
    StoreInv_Test_0 = 1,
}

[CreateAssetMenu(fileName = "StoreInventoryPropertyPP_", menuName = "AllProperties/StoreInventoryProperty")]
public class StoreInventoryProperty : EnumStringKeyedProperty<Key_StoreInventory>
{
    [SerializeField] private StoreContainer storeContainer = new StoreContainer();
    public StoreContainer StoreContainer
    {
        get
        {
            EnsureContainer();
            return storeContainer;
        }
    }

    public int SlotCount
    {
        get
        {
            EnsureContainer();
            return storeContainer.MaxSlots;
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
    }
}

[Serializable]
public class StoreSlot : Slot<Key_BuildablePP>
{
    public bool isLocked;
    public int initialStock;
    public override bool IsEmpty => ItemEnum == Key_BuildablePP.None && !isLocked;
    public override bool ClearWhenCountZero => false;

}

[Serializable]
public class StoreContainer : SContainer<StoreSlot, Key_BuildablePP>
{
    public StoreContainer() : base()
    {
    }
    public StoreContainer(int slotCount) : base(slotCount)
    {
    }
}