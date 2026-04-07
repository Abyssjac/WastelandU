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
/// A preset configuration of pre-placed buildables that can be loaded at game start.
/// Entries MUST be ordered by dependency: platforms first, then rooms, then furniture.
/// </summary>
[CreateAssetMenu(fileName = "BuildPreset_", menuName = "AllProperties/ BuildPreset")]
public class BuildPreset : ScriptableObject
{
    [Tooltip("Ordered list of buildables to place at game start.\n" +
             "Must be in dependency order: World-layer first, then Platform-layer, then Room-layer.")]
    public BuildPresetEntry[] entries = new BuildPresetEntry[0];
}
