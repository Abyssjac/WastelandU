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
/// A named group of blueprint entries for Inspector organization.
/// Groups are flattened at runtime ¡ª ordering is: Group0 entries, then Group1 entries, etc.
/// </summary>
[System.Serializable]
public struct BlueprintGroup
{
    [Tooltip("Display label for this group in the Inspector (e.g. 'Platforms', 'Walls', 'Furniture').")]
    public string groupName;

    [Tooltip("Entries in this group, in dependency order.")]
    public BlueprintEntry[] entries;
}

/// <summary>
/// A blueprint (prefab room) that the player can place as a single unit.
/// Contains grouped, ordered lists of buildable entries with local offsets.
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

    [Header("Blueprint Groups")]
    [Tooltip("Ordered groups of buildables in this blueprint.\n" +
             "Groups are purely for Inspector organization ¡ª at runtime they are flattened in order.\n" +
             "Must be in dependency order: platforms first, then walls, then ground, then furniture.")]
    public BlueprintGroup[] groups = new BlueprintGroup[0];

    [Header("UI Display")]
    public Sprite iconSprite;
    public string displayName;

    // ©¤©¤©¤ Runtime flattened cache ©¤©¤©¤
    private BlueprintEntry[] flattenedCache;
    private bool dirty = true;

    private void OnValidate() { dirty = true; }
    private void OnEnable() { dirty = true; }

    /// <summary>
    /// All entries flattened from groups, in order. Use this at runtime.
    /// </summary>
    public BlueprintEntry[] Entries
    {
        get
        {
            if (dirty || flattenedCache == null)
            {
                flattenedCache = FlattenGroups();
                dirty = false;
            }
            return flattenedCache;
        }
    }

    private BlueprintEntry[] FlattenGroups()
    {
        if (groups == null || groups.Length == 0)
            return System.Array.Empty<BlueprintEntry>();

        int total = 0;
        for (int g = 0; g < groups.Length; g++)
        {
            if (groups[g].entries != null)
                total += groups[g].entries.Length;
        }

        BlueprintEntry[] result = new BlueprintEntry[total];
        int idx = 0;
        for (int g = 0; g < groups.Length; g++)
        {
            if (groups[g].entries == null) continue;
            for (int i = 0; i < groups[g].entries.Length; i++)
            {
                result[idx++] = groups[g].entries[i];
            }
        }
        return result;
    }
}
