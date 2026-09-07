using JackyUtility;
using UnityEngine;

/// <summary>
/// Defines the base store offer for an item.
/// Store-specific price overrides can be added later by StoreInventory.
/// </summary>
[CreateAssetMenu(fileName = "SellablePP_", menuName = "AllProperties/SellableProperty")]
public class SellableProperty : EnumStringKeyedProperty<Key_SellablePP>
{
    [Header("Price")]
    [Min(0)]
    [SerializeField] private int price;

    [Header("Store Detail")]
    [TextArea(2, 5)]
    [SerializeField] private string detailDescription;

    public int Price => Mathf.Max(0, price);
    public string DetailDescription => detailDescription ?? string.Empty;
}

