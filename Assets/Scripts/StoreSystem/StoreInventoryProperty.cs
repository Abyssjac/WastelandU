using JackyUtility;
using UnityEngine;
using System;
using System.Collections.Generic;

public enum Key_StoreInventory
{
    None = 0,
    StoreInv_Test_0 = 1,
    StoreInv_Bob_0 = 2,
    StoreInv_Mira_0 = 3,
    StoreInv_Orren_0 = 4,
    StoreInv_Test_A = 5,
}

[CreateAssetMenu(fileName = "StoreInventoryPropertyPP_", menuName = "AllProperties/StoreInventoryProperty")]
public class StoreInventoryProperty : EnumStringKeyedProperty<Key_StoreInventory>
{
    public const float _minimumPriceMultiplier = 0.0001f;

    [Header("Pricing")]
    [SerializeField] private StorePriceOverrideData _priceOverrideData = new StorePriceOverrideData();

    [SerializeField] private StoreContainer storeContainer = new StoreContainer();

    public float DefaultPriceMultiplier => _priceOverrideData != null
        ? _priceOverrideData.DefaultPriceMultiplier
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
    /// Resolves one item's final transaction price for both Buy and Sell modes.
    /// An item multiplier takes priority over a fixed price; when neither is
    /// configured, the store-wide default multiplier is used instead.
    /// </summary>
    public int ResolvePrice(Key_ItemDefinitionPP itemKey, int basePrice)
    {
        EnsurePriceOverrideData();
        return _priceOverrideData.ResolvePrice(itemKey, basePrice);
    }

    /// <summary>
    /// Returns only the item's own multiplier override for UI presentation.
    /// Store-wide multipliers and fixed prices deliberately return the neutral
    /// value because they do not use the percentage badge.
    /// </summary>
    public float GetItemPriceMultiplierForDisplay(Key_ItemDefinitionPP itemKey)
    {
        EnsurePriceOverrideData();
        return _priceOverrideData.GetItemPriceMultiplierForDisplay(itemKey);
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
        }

        return runtime;
    }

    private void OnValidate()
    {
        EnsureContainer();
        EnsurePriceOverrideData();

        if (!HasUniqueItemDefinitions(out Key_ItemDefinitionPP duplicateItemKey))
            Debug.LogError($"[{nameof(StoreInventoryProperty)}] '{name}' contains duplicate item '{duplicateItemKey}'. " +
                           "A store may contain each item definition only once.", this);

        if (!_priceOverrideData.HasUniqueItemOverrides(out Key_ItemDefinitionPP invalidOverrideKey))
            Debug.LogError($"[{nameof(StoreInventoryProperty)}] '{name}' contains a missing or duplicate price override for '{invalidOverrideKey}'. " +
                           "Each price override must target one valid item definition exactly once.", this);
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

    private void EnsurePriceOverrideData()
    {
        if (_priceOverrideData == null)
            _priceOverrideData = new StorePriceOverrideData();

        _priceOverrideData.EnsureInitialized();
    }
}

[Serializable]
public class StoreSlot : Slot<Key_ItemDefinitionPP>
{
    public bool isLocked;

    public override bool IsEmpty => ItemEnum == Key_ItemDefinitionPP.None && !isLocked;
    public override bool ClearWhenCountZero => false;
}

/// <summary>
/// The complete authored pricing configuration for one store. It is shared by
/// Buy and Sell transactions; only one default multiplier exists per store.
/// </summary>
[Serializable]
public class StorePriceOverrideData
{
    [Tooltip("Multiplier used when this item has no active item-level override. Applies to both Buy and Sell.")]
    [Min(StoreInventoryProperty._minimumPriceMultiplier)]
    [SerializeField] private float _defaultPriceMultiplier = 1f;

    [SerializeField] private List<StoreItemPriceOverride> _itemPriceOverrides = new List<StoreItemPriceOverride>();

    public float DefaultPriceMultiplier => _defaultPriceMultiplier > 0f
        ? Mathf.Max(StoreInventoryProperty._minimumPriceMultiplier, _defaultPriceMultiplier)
        : 1f;

    public void EnsureInitialized()
    {
        if (_itemPriceOverrides == null)
            _itemPriceOverrides = new List<StoreItemPriceOverride>();
    }

    public int ResolvePrice(Key_ItemDefinitionPP itemKey, int basePrice)
    {
        int sanitizedBasePrice = Mathf.Max(0, basePrice);

        if (TryGetItemPriceOverride(itemKey, out StoreItemPriceOverride itemOverride))
        {
            if (itemOverride.PriceMultiplier != 1f)
                return ResolveMultipliedPrice(sanitizedBasePrice, itemOverride.PriceMultiplier);

            if (itemOverride.FixedPrice >= 1)
                return itemOverride.FixedPrice;
        }

        return ResolveMultipliedPrice(sanitizedBasePrice, DefaultPriceMultiplier);
    }

    public float GetItemPriceMultiplierForDisplay(Key_ItemDefinitionPP itemKey)
    {
        return TryGetItemPriceOverride(itemKey, out StoreItemPriceOverride itemOverride)
            ? itemOverride.PriceMultiplier
            : 1f;
    }

    public bool HasUniqueItemOverrides(out Key_ItemDefinitionPP invalidOverrideKey)
    {
        EnsureInitialized();

        var seen = new HashSet<Key_ItemDefinitionPP>();
        for (int i = 0; i < _itemPriceOverrides.Count; i++)
        {
            StoreItemPriceOverride itemOverride = _itemPriceOverrides[i];
            if (itemOverride == null || itemOverride.ItemKey == Key_ItemDefinitionPP.None)
            {
                invalidOverrideKey = Key_ItemDefinitionPP.None;
                return false;
            }

            if (!seen.Add(itemOverride.ItemKey))
            {
                invalidOverrideKey = itemOverride.ItemKey;
                return false;
            }
        }

        invalidOverrideKey = Key_ItemDefinitionPP.None;
        return true;
    }

    private bool TryGetItemPriceOverride(Key_ItemDefinitionPP itemKey, out StoreItemPriceOverride itemOverride)
    {
        EnsureInitialized();

        for (int i = 0; i < _itemPriceOverrides.Count; i++)
        {
            StoreItemPriceOverride candidate = _itemPriceOverrides[i];
            if (candidate != null && candidate.ItemKey == itemKey)
            {
                itemOverride = candidate;
                return true;
            }
        }

        itemOverride = null;
        return false;
    }

    private static int ResolveMultipliedPrice(int basePrice, float multiplier)
    {
        int roundedPrice = Mathf.RoundToInt(basePrice * Mathf.Max(StoreInventoryProperty._minimumPriceMultiplier, multiplier));
        return Mathf.Max(1, roundedPrice);
    }
}

/// <summary>
/// Optional price override for one concrete item definition in one store.
/// Multiplier takes priority over fixed price when both are configured.
/// </summary>
[Serializable]
public class StoreItemPriceOverride
{
    [SerializeField] private Key_ItemDefinitionPP _itemKey = Key_ItemDefinitionPP.None;

    [Tooltip("Exact final price. Set to -1 when this item does not use a fixed price.")]
    [Min(-1)]
    [SerializeField] private int _fixedPrice = -1;

    [Tooltip("Final price multiplier. A value other than 1 takes priority over Fixed Price.")]
    [Min(StoreInventoryProperty._minimumPriceMultiplier)]
    [SerializeField] private float _priceMultiplier = 1f;

    public Key_ItemDefinitionPP ItemKey => _itemKey;
    public int FixedPrice => _fixedPrice >= 1 ? _fixedPrice : -1;
    public float PriceMultiplier => _priceMultiplier > 0f
        ? Mathf.Max(StoreInventoryProperty._minimumPriceMultiplier, _priceMultiplier)
        : 1f;
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
