using UnityEngine;
using JackyUtility;

/// <summary>
/// Enum keys for all blueprint entries in <see cref="BuildBlueprintDatabase"/>.
/// Add new entries here as you create new blueprint ScriptableObjects.
/// </summary>
public enum Key_BuildBlueprintPP
{
    None = 0,

    BuildBlueprint_Room_545_0 = 1,
    BuildBlueprint_Platform_33_0 = 31,
}

/// <summary>
/// One entry in a blueprint: which buildable to place, at what local offset and rotation.
/// Coordinates are relative to the blueprint's placement anchor.
/// Entries MUST be ordered by dependency (e.g. platform ¡ú wall ¡ú ground ¡ú furniture).
/// </summary>
[System.Serializable]
public struct BlueprintEntry
{
    [Tooltip("Which buildable to place (must exist in BuildableDatabase).")]
    public Key_BuildablePP buildableEnumKey;

    [Tooltip("Local cell offset relative to the blueprint anchor.\n" +
             "When the player places the blueprint at anchor A, this entry is placed at A + localCell.")]
    public Vector3Int localCell;

    [Tooltip("Local rotation step (0-3). Combined with the blueprint's overall rotation at placement time.")]
    [Range(0, 3)]
    public int localRotationStep;
}

/// <summary>
/// A blueprint (prefab room) that the player can place as a single unit.
/// Contains an ordered list of buildable entries with local offsets.
/// Validated and placed atomically via <see cref="GridSandbox"/>:
/// either all entries succeed or none are placed.
/// </summary>
[CreateAssetMenu(fileName = "BuildBlueprintPP_", menuName = "AllProperties/ BuildBlueprintProperty")]
public class BuildBlueprintProperty : ScriptableObject, IEnumStringKeyedEntry<Key_BuildBlueprintPP>
{
    [Header("Keys")]
    [SerializeField] private Key_BuildBlueprintPP enumKey;
    [SerializeField] private string stringKey;

    public Key_BuildBlueprintPP EnumKey => enumKey;
    public string StringKey => stringKey;

    [Header("Blueprint Entries")]
    [Tooltip("Ordered list of buildables in this blueprint.\n" +
             "Must be in dependency order: platforms first, then walls, then ground, then furniture.\n" +
             "Coordinates are local offsets relative to the blueprint anchor.")]
    public BlueprintEntry[] entries = new BlueprintEntry[0];

    [Header("UI Display")]
    public Sprite iconSprite;
    public string displayName;
}
