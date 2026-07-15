using UnityEngine;
using UnityEngine.Serialization;
using JackyUtility;

[CreateAssetMenu(fileName = "ItemDefinition_", menuName = "AllProperties/ItemDefinition")]
public class ItemDefinitionSO : EnumStringKeyedProperty<Key_ItemDefinitionPP>, ISlotDisplayableProperty
{
    [Min(1)]
    [FormerlySerializedAs("maxStackCount")]
    [SerializeField] private int stackCount = 99;

    [SerializeField] private Sprite icon;
    [SerializeField] private Key_BuildablePP buildableKey = Key_BuildablePP.None;

    public int StackCount => Mathf.Max(1, stackCount);
    public Sprite Icon => icon;
    public Key_BuildablePP BuildableKey => buildableKey;
    public bool IsBuildable => buildableKey != Key_BuildablePP.None;

    public SlotDisplayData ToSlotDisplayData(int itemCount)
    {
        return new SlotDisplayData(icon, Color.white, itemCount, StringKey);
    }
}

public enum Key_ItemDefinitionPP
{
    None = 0,

    [InspectorName("Base/Platform Normal Free 0")] Item_Build_Base_Platform_Normal_Free_0 = 1,
    [InspectorName("Base/Platform Normal 0")] Item_Build_Base_Platform_Normal_0 = 2,
    [InspectorName("Base/Platform Elevated 0")] Item_Build_Base_Platform_Elevated_0 = 3,
    [InspectorName("Base/Platform Elevated 1")] Item_Build_Base_Platform_Elevated_1 = 4,
    [InspectorName("Base/Platform Trans Free 0")] Item_Build_Base_Platform_Trans_Free_0 = 5,
    [InspectorName("Base/Platform Normal 10x10")] Item_Build_Base_Platform_Normal_10x10 = 6,
    [InspectorName("Base/Platform Trans 10x10")] Item_Build_Base_Platform_Trans_10x10 = 7,

    [InspectorName("Room/5x5")] Item_Build_Room_5x5 = 10,
    [InspectorName("Base/Wall XNegPos 1Level 0")] Item_Build_Base_Wall_XNegPos_1Level_0 = 11,
    [InspectorName("Base/Wall XPosNeg 1Level 0")] Item_Build_Base_Wall_XPosNeg_1Level_0 = 12,
    [InspectorName("Base/Wall ZNegPos 1Level 0")] Item_Build_Base_Wall_ZNegPos_1Level_0 = 13,
    [InspectorName("Base/Wall ZPosNeg 1Level 0")] Item_Build_Base_Wall_ZPosNeg_1Level_0 = 14,

    [InspectorName("Base/Slope 2Level 0")] Item_Build_Base_Slope_2Level_0 = 20,
    [InspectorName("Base/Door XNegPos 0")] Item_Build_Base_Door_XNegPos_0 = 25,

    [InspectorName("Bookstore/Bookshelf 0")] Item_Build_Bookstore_Bookshelf_0 = 30,
    [InspectorName("Bookstore/Bookshelf 1")] Item_Build_Bookstore_Bookshelf_1 = 31,
    [InspectorName("Bookstore/Bookshelf 2")] Item_Build_Bookstore_Bookshelf_2 = 32,
    [InspectorName("Bookstore/Bookshelf 3")] Item_Build_Bookstore_Bookshelf_3 = 33,
    [InspectorName("Bookstore/Bookshelf 4")] Item_Build_Bookstore_Bookshelf_4 = 34,
    [InspectorName("Bookstore/Table 0")] Item_Build_Bookstore_Table_0 = 35,
    [InspectorName("Bookstore/Couch 0")] Item_Build_Bookstore_Couch_0 = 36,
    [InspectorName("Bookstore/Plant 0")] Item_Build_Bookstore_Plant_0 = 37,
    [InspectorName("Bookstore/Plant 1")] Item_Build_Bookstore_Plant_1 = 38,
    [InspectorName("Bookstore/Stepladder 0")] Item_Build_Bookstore_Stepladder_0 = 39,

    [InspectorName("Artstudio/Easel 0")] Item_Build_Artstudio_Easel_0 = 50,
    [InspectorName("Artstudio/Easel 1")] Item_Build_Artstudio_Easel_1 = 51,
    [InspectorName("Artstudio/Easel 2")] Item_Build_Artstudio_Easel_2 = 52,
    [InspectorName("Artstudio/Chair 0")] Item_Build_Artstudio_Chair_0 = 53,
    [InspectorName("Artstudio/Chair 1")] Item_Build_Artstudio_Chair_1 = 54,
    [InspectorName("Artstudio/Plant 0")] Item_Build_Artstudio_Plant_0 = 55,
    [InspectorName("Artstudio/Table 0")] Item_Build_Artstudio_Table_0 = 56,
    [InspectorName("Artstudio/Canvastack 0")] Item_Build_Artstudio_Canvastack_0 = 57,

    [InspectorName("Bar/Plant 0")] Item_Build_Bar_Plant_0 = 60,
    [InspectorName("Bar/Shelf 0")] Item_Build_Bar_Shelf_0 = 61,
    [InspectorName("Bar/Stool 0")] Item_Build_Bar_Stool_0 = 62,
    [InspectorName("Bar/Table 0")] Item_Build_Bar_Table_0 = 63,
    [InspectorName("Bar/Table 1")] Item_Build_Bar_Table_1 = 64,
    [InspectorName("Bar/Carpet 0")] Item_Build_Bar_Carpet_0 = 65,
    [InspectorName("Bar/Cocktail Neon Sign 0")] Item_Build_Bar_Cocktailneonsign_0 = 66,
    [InspectorName("Bar/Open Neon Sign 0")] Item_Build_Bar_Openneonsign_0 = 67,

    [InspectorName("Astro/Sate Receiver 0")] Item_Build_Astro_SateReceiver_0 = 70,
    [InspectorName("Astro/Cabinet 0")] Item_Build_Astro_Cabinet_0 = 71,
    [InspectorName("Astro/Meteor 0")] Item_Build_Astro_Meteor_0 = 72,
    [InspectorName("Astro/Meteor 1")] Item_Build_Astro_Meteor_1 = 73,
    [InspectorName("Astro/Planet 0")] Item_Build_Astro_Planet_0 = 74,
    [InspectorName("Astro/Planet 1")] Item_Build_Astro_Planet_1 = 75,
    [InspectorName("Astro/Planet 2")] Item_Build_Astro_Planet_2 = 76,
    [InspectorName("Astro/Plant 0")] Item_Build_Astro_Plant_0 = 77,
    [InspectorName("Astro/Pouf Moon 0")] Item_Build_Astro_PoufMoon_0 = 78,
    [InspectorName("Astro/Pouf Star 0")] Item_Build_Astro_PoufStar_0 = 79,
    [InspectorName("Astro/Table 0")] Item_Build_Astro_Table_0 = 80,
    [InspectorName("Astro/Telescope 0")] Item_Build_Astro_Telescope_0 = 81,
    [InspectorName("Astro/Telescope 1")] Item_Build_Astro_Telescope_1 = 82,
    [InspectorName("Astro/Carpet 0")] Item_Build_Astro_Carpet_0 = 83,
    [InspectorName("Astro/Moon 0")] Item_Build_Astro_Moon_0 = 84,
    [InspectorName("Astro/Poster 0")] Item_Build_Astro_Poster_0 = 85,
    [InspectorName("Astro/Poster 1")] Item_Build_Astro_Poster_1 = 86,
    [InspectorName("Astro/Poster 2")] Item_Build_Astro_Poster_2 = 87,
    [InspectorName("Astro/Poster 3")] Item_Build_Astro_Poster_3 = 88,
    [InspectorName("Astro/Star 0")] Item_Build_Astro_Star_0 = 89,
    [InspectorName("Astro/Star 1")] Item_Build_Astro_Star_1 = 90,
    [InspectorName("Astro/Window 0")] Item_Build_Astro_Window_0 = 91,
}
