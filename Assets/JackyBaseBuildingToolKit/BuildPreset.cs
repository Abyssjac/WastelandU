using UnityEngine;

/// <summary>
/// One entry in a build preset: which buildable to place, where, and at what rotation.
/// </summary>
[System.Serializable]
public struct BuildPresetEntry
{
    [Tooltip("String key that matches a BuildableProperty in the BuildableDatabase.")]
    public Key_BuildablePP buildableEnumKey;

    [Tooltip("The grid cell to place the anchor at.")]
    public Vector3Int anchorCell;

    [Tooltip("Rotation step (0-3, each step = 90бу around Y).")]
    [Range(0, 3)]
    public int rotationStep;
}

/// <summary>
/// A blueprint reference in a build preset: places an entire blueprint at a given anchor and rotation.
/// </summary>
[System.Serializable]
public struct BuildPresetBlueprintEntry
{
    [Tooltip("Which blueprint to place (must exist in BuildBlueprintDatabase).")]
    public Key_BuildBlueprintPP blueprintEnumKey;

    [Tooltip("The grid cell to place the blueprint anchor at.")]
    public Vector3Int anchorCell;

    [Tooltip("Blueprint rotation step (0-3, each step = 90бу around Y).")]
    [Range(0, 3)]
    public int rotationStep;
}

/// <summary>
/// A named group of preset entries for Inspector organization.
/// Each group can contain individual buildable entries and/or full blueprint references.
/// The 'forced' flag determines whether placement validation is skipped.
/// </summary>
[System.Serializable]
public struct BuildPresetGroup
{
    [Tooltip("Display label for this group in the Inspector (e.g. 'Terrain', 'Base Structure', 'Furniture').")]
    public string groupName;

    [Tooltip("If true, entries in this group are force-placed into the grid regardless of validation.\n" +
             "Use for terrain/base structures that must exist at game start.\n" +
             "If false, entries are placed via normal validation (CanPlace check).")]
    public bool forced;

    [Header("Individual Buildables")]
    [Tooltip("Individual buildable entries in this group, in dependency order.")]
    public BuildPresetEntry[] entries;

    [Header("Blueprints")]
    [Tooltip("Full blueprint placements in this group.\n" +
             "Each blueprint's internal entries are expanded in dependency order.")]
    public BuildPresetBlueprintEntry[] blueprints;
}

/// <summary>
/// A preset configuration of pre-placed buildables that can be loaded at game start.
/// Groups are processed in order бк earlier groups are placed first.
/// Within each group, individual entries are placed first, then blueprints.
/// </summary>
[CreateAssetMenu(fileName = "BuildPreset_", menuName = "AllProperties/ BuildPreset")]
public class BuildPreset : ScriptableObject
{
    [Tooltip("Ordered groups of buildables/blueprints to place at game start.\n" +
             "Must be in dependency order: World-layer first, then Platform-layer, then Room-layer, then furniture.")]
    public BuildPresetGroup[] groups = new BuildPresetGroup[0];
}
