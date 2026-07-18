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

public enum Key_SellablePP
{
    None = 0,

    [InspectorName("Bookstore/Table 0")] Sellable_Bookstore_Table_0 = 1,
    [InspectorName("Artstudio/Chair 0")] Sellable_Artstudio_Chair_0 = 2,
    [InspectorName("Bar/Shelf 0")] Sellable_Bar_Shelf_0 = 3,
}
