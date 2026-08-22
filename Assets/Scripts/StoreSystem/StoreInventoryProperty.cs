using JackyUtility;
using UnityEngine;
using System;
using System.Collections.Generic;

public enum Key_StoreInventory
{
    None = 0,
    StoreInv_Test_0 = 1,
    StoreInv_Bob_0 = 2,
}

[CreateAssetMenu(fileName = "StoreInventoryPropertyPP_", menuName = "AllProperties/StoreInventoryProperty")]
public class StoreInventoryProperty : EnumStringKeyedProperty<Key_StoreInventory>
{
    private const float _minimumPriceMultiplier = 0.0001f;

    [Header("Pricing")]
    [Tooltip("Fallback multiplier used when a slot has no active price override.")]
    [Min(_minimumPriceMultiplier)]
    [SerializeField] private float _defaultPriceMultiplier = 1f;

    [SerializeField] private StoreContainer storeContainer = new StoreContainer();

    // Legacy StoreInventory assets created before this field existed may deserialize it as 0.
    // Treat that as the authored default without mutating the asset during a read.
    public float DefaultPriceMultiplier => _defaultPriceMultiplier > 0f
        ? Mathf.Max(_minimumPriceMultiplier, _defaultPriceMultiplier)
        : 1f;

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

    /// <summary>
    /// Resolves one offer's final purchase price. A non-default slot multiplier
    /// replaces the store default multiplier; otherwise a configured fixed price
    /// is used; otherwise the store default multiplier is applied to the base price.
    /// The final result is always at least one currency unit.
    /// </summary>
    public int ResolvePrice(StoreSlot slot, int basePrice)
    {
        int sanitizedBasePrice = Mathf.Max(0, basePrice);

        if (slot != null && slot.PriceMultiplier != 1f)
            return ResolveMultipliedPrice(sanitizedBasePrice, slot.PriceMultiplier);

        if (slot != null && slot.FixedPrice >= 1)
            return slot.FixedPrice;

        return ResolveMultipliedPrice(sanitizedBasePrice, DefaultPriceMultiplier);
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
            if (source == null)
                continue;

            runtime.TrySetSlotAtIndex(i, source.ItemEnum, Mathf.Max(0, source.ItemCount), out _);

            StoreSlot target = runtime.GetSlotByIndex(i);
            target.isLocked = source.isLocked;
            target.CopyPriceOverrideFrom(source);
        }

        return runtime;
    }

    private void OnValidate()
    {
        EnsureContainer();

        if (!HasUniqueItemDefinitions(out Key_ItemDefinitionPP duplicateItemKey))
            Debug.LogError($"[{nameof(StoreInventoryProperty)}] '{name}' contains duplicate item '{duplicateItemKey}'. " +
                           "A store may contain each item definition only once.", this);
    }

    /// <summary>
    /// Store persistence uses the ItemDefinition key as the identity for one
    /// stock entry, so a configured store must not list the same item twice.
    /// </summary>
    public bool HasUniqueItemDefinitions(out Key_ItemDefinitionPP duplicateItemKey)
    {
        EnsureContainer();

        var seen = new HashSet<Key_ItemDefinitionPP>();
        for (int i = 0; i < storeContainer.MaxSlots; i++)
        {
            StoreSlot slot = storeContainer.GetSlotByIndex(i);
            if (slot == null || slot.ItemEnum == Key_ItemDefinitionPP.None)
                continue;

            if (!seen.Add(slot.ItemEnum))
            {
                duplicateItemKey = slot.ItemEnum;
                return false;
            }
        }

        duplicateItemKey = Key_ItemDefinitionPP.None;
        return true;
    }

    private void EnsureContainer()
    {
        if (storeContainer == null)
            storeContainer = new StoreContainer();

        storeContainer.EnsureInitialized();
    }

    private static int ResolveMultipliedPrice(int basePrice, float multiplier)
    {
        int roundedPrice = Mathf.RoundToInt(basePrice * Mathf.Max(_minimumPriceMultiplier, multiplier));
        return Mathf.Max(1, roundedPrice);
    }
}

[Serializable]
public class StoreSlot : Slot<Key_ItemDefinitionPP>
{
    [Header("Price Override")]
    [Tooltip("Exact final price. Set to -1 when this slot does not use a fixed price.")]
    [Min(-1)]
    [SerializeField] private int _fixedPrice = -1;

    [Tooltip("Final price multiplier. A value other than 1 replaces the store default multiplier and takes priority over Fixed Price.")]
    [Min(0.0001f)]
    [SerializeField] private float _priceMultiplier = 1f;

    public bool isLocked;

    public int FixedPrice => _fixedPrice >= 1 ? _fixedPrice : -1;
    // Legacy slots created before this field existed may deserialize it as 0.
    // A multiplier is required to be positive, so zero safely means "no override" here.
    public float PriceMultiplier => _priceMultiplier > 0f
        ? Mathf.Max(0.0001f, _priceMultiplier)
        : 1f;

    public override bool IsEmpty => ItemEnum == Key_ItemDefinitionPP.None && !isLocked;
    public override bool ClearWhenCountZero => false;

    public void CopyPriceOverrideFrom(StoreSlot source)
    {
        if (source == null)
            return;

        _fixedPrice = source._fixedPrice;
        _priceMultiplier = source._priceMultiplier;
    }
}

[Serializable]
public class StoreContainer : SContainer<StoreSlot, Key_ItemDefinitionPP>
{
    public StoreContainer() : base()
    {
    }

    public StoreContainer(int slotCount) : base(slotCount)
    {
    }
}
