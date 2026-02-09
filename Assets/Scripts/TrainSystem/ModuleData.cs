using UnityEngine;
[CreateAssetMenu(menuName = "LoadoutMVP/ModuleData", fileName = "MD_")]
public class ModuleData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [Header("Placement")]
    public ModuleSlotType slotType = ModuleSlotType.Weapon;

    [Header("Prefab")]
    public GameObject prefab; // ModuleView prefab

    [Header("UI Display")]
    public Sprite moduleIcon;
    [TextArea(1,10)]
    public string moduleDescription;
    public int buildCost;
}

public enum ModuleSlotType
{
    Weapon = 0,
    Utility = 1,
    Defense = 2
}