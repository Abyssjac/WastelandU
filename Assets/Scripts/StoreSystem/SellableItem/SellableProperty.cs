using JackyUtility;
using UnityEngine;

/// <summary>
/// Stable identifiers for this project's sellable definitions.
/// Keep the numeric values stable because authored properties use them.
/// </summary>
public enum Key_SellablePP
{
    None = 0,

    [InspectorName("Bookstore/Table 0")] Sellable_Bookstore_Table_0 = 1,
    [InspectorName("Artstudio/Chair 0")] Sellable_Artstudio_Chair_0 = 2,
    [InspectorName("Bar/Shelf 0")] Sellable_Bar_Shelf_0 = 3,
    [InspectorName("Resource/Brassscrap 0")] Sellable_Resource_Brassscrap_0 = 10,
    [InspectorName("Resource/Aether 0")] Sellable_Resourcce_Aether_0 = 11,
    [InspectorName("Resource/Cloudwood 0")] Sellable_Resource_Cloudwood_0 = 12,
    [InspectorName("Resource/Test A")] Sellable_Resource_Test_A = 20,
    [InspectorName("Resource/Test B")] Sellable_Resource_Test_B = 21,
    [InspectorName("Resource/Test C")] Sellable_Resource_Test_C = 22,
}

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

