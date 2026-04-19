using UnityEngine;
using JackyUtility;

/// <summary>
/// Stores UI display data for a build action category.
/// Looked up by <see cref="Key_BuildActionDisplayPP"/> via <see cref="BuildActionDisplayDatabase"/>.
/// </summary>
[CreateAssetMenu(fileName = "BuildActionDisplay_", menuName = "AllProperties/ BuildActionDisplayInfo")]
public class BuildActionDisplayInfo : EnumStringKeyedProperty<Key_BuildActionDisplayPP>
{
    [Header("UI Display")]
    public string displayName;
    [TextArea(2, 5)]
    public string description;
    public Sprite icon;
    public Color panelTintColor = Color.white;
}

/// <summary>
/// <summary>
/// Enum key for <see cref="BuildActionDisplayInfo"/> entries.
/// Used to look up UI display data for build actions.
/// </summary>
public enum Key_BuildActionDisplayPP
{
    None = 0,
    BuildDisplay_Base_Platform_Normal_0 = 1,
    BuildDisplay_Base_WallXNegPos_1Level_0 = 2,
    BuildDisplay_Base_Platform_Elevated_0 = 3,
    BuildDisplay_Base_Platform_Elevated_1 = 4,
}
