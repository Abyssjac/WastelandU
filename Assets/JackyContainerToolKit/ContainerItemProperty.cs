using UnityEngine;
using JackyUtility;

[CreateAssetMenu(fileName = "ContainerItemPP_", menuName = "AllProperties/ ContainerItemProperty")]
public class ContainerItemProperty : EnumStringKeyedProperty<Key_ContainerItemPP>, ISlotDisplayableProperty
{
    public int maxStackCount;
    public Sprite icon;

    [Header("Item Actions")]
    [Tooltip("Drag action SOs here to declare what this item can do (build, drop, use ¡­).")]
    [SerializeField] private ContainerItemAction[] actions = new ContainerItemAction[0];

    /// <summary>All actions assigned to this item.</summary>
    public ContainerItemAction[] Actions => actions;

    // ©¤©¤©¤ Action Queries ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Returns the first action of type <typeparamref name="T"/>, or null if none exists.
    /// </summary>
    public T GetAction<T>() where T : ContainerItemAction
    {
        for (int i = 0; i < actions.Length; i++)
        {
            if (actions[i] is T typed)
                return typed;
        }
        return null;
    }

    /// <summary>
    /// Returns true when the item has at least one action of type <typeparamref name="T"/>.
    /// </summary>
    public bool HasAction<T>() where T : ContainerItemAction
    {
        for (int i = 0; i < actions.Length; i++)
        {
            if (actions[i] is T)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Tries to get the first action of type <typeparamref name="T"/>.
    /// Returns true when found.
    /// </summary>
    public bool TryGetAction<T>(out T action) where T : ContainerItemAction
    {
        action = GetAction<T>();
        return action != null;
    }

    // ©¤©¤©¤ Display ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public SlotDisplayData ToSlotDisplayData(int itemCount)
    {
        return new SlotDisplayData(icon, Color.white, itemCount);
    }
}

public enum Key_ContainerItemPP
{
    None = 0,
    ContainerItem_Iron = 1,
    ContainerItem_Copper = 2,
    ContainerItem_Gold = 3,
    ContainerItem_Diamond = 4,
    ContainerItem_Wood = 5,
    ContainerItem_Stone = 6,
    ContainerItem_Food = 7,

    ContainerItem_Cube11 = 100,
    ContainerItem_Cube12 = 101,
    ContainerItem_Cube22 = 102, 

    ContainerItem_Room55 = 200,
    ContainerItem_Platform11 = 201,
    COntainerItem_FurChair11 = 202,
    ContainerItem_Platform99 = 203,

    ContainerItem_Base_Platform_Normal_0 = 204,
    ContainerItem_Base_Platform_Elevated_0 = 206,
    ContainerItem_Base_Platform_Elevated_1 = 207,


    ContainerItem_Base_Wall_XNegPos_OLayer_0 = 205,

    ContainerItem_Fur_BookStore_BookShelf_113 = 300,


    //Mihoyo Demo
    ContainerItemM_Cube11_0 = 400,
    ContainerItemM_Cube12_0 = 401,
    ContainerItemM_Cube22_0 = 402,
    ContainerItemM_CubeL_0 = 403,
}