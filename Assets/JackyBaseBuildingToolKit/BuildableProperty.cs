using JackyUtility;
using UnityEngine;

/// <summary>
/// Which logical layer this buildable occupies in the grid.
/// Multiple layers can coexist on the same XZ cell.
/// </summary>
public enum BuildLayer
{
    /// <summary>The world ground layer бк platforms are placed here.</summary>
    World = 0,
    /// <summary>On top of a platform бк rooms, stairs, etc.</summary>
    Platform = 1,
    /// <summary>Inside a room бк furniture, decorations, etc.</summary>
    Room = 2,
}

/// <summary>
/// What surface type a buildable provides to the layer above it.
/// </summary>
public enum BuildSurfaceType
{
    /// <summary>This buildable does not provide any surface for others.</summary>
    None = 0,
    /// <summary>Provides a platform surface (rooms / stairs can be placed on it).</summary>
    Platform = 1,
    /// <summary>Provides a room surface (furniture can be placed inside it).</summary>
    Room = 2,
}

[CreateAssetMenu(fileName = "BuildablePP_", menuName = "AllProperties/ BuildableProperty")]
public class BuildableProperty : ScriptableObject, IEnumStringKeyedEntry<Key_BuildablePP>
{
    [Header("Keys")]
    [SerializeField] private Key_BuildablePP enumKey;
    [SerializeField] private string stringKey;

    public Key_BuildablePP EnumKey => enumKey;
    public string StringKey => stringKey;

    [Header("Visuals")]
    public GameObject prefab;
    public GameObject previewPrefab;

    [Header("Grid Footprint")]
    [Tooltip("Each entry is one occupied cell offset relative to the anchor (0,0,0). " +
             "e.g. a 2x1x3 table: (0,0,0),(1,0,0),(0,0,1),(1,0,1),(0,0,2),(1,0,2)")]
    public Vector3Int[] footprint = new Vector3Int[] { Vector3Int.zero };

    [Header("Layer & Surface")]
    [Tooltip("Which grid layer this buildable occupies.")]
    public BuildLayer buildLayer = BuildLayer.World;

    [Tooltip("What surface type is REQUIRED beneath this buildable.\n" +
             "None = no requirement (e.g. platforms placed on bare ground).\n" +
             "Platform = needs a platform beneath (rooms, stairs).\n" +
             "Room = needs a room beneath (furniture).")]
    public BuildSurfaceType requiredSurface = BuildSurfaceType.None;

    [Tooltip("What surface type this buildable PROVIDES to things on top of it.\n" +
             "None = nothing can be placed on it.\n" +
             "Platform = rooms/stairs can be placed on it.\n" +
             "Room = furniture can be placed inside it.")]
    public BuildSurfaceType providedSurface = BuildSurfaceType.None;

    [Header("Placement Rules")]
    public bool canRotate = true;
    public bool canMove = true;

    [Header("UI Display")]
    public Sprite iconSprite;
    public string displayName;

    /// <summary>
    /// Returns the footprint cells rotated by 90бу steps around Y axis.
    /// rotationStep: 0=0бу, 1=90бу, 2=180бу, 3=270бу
    /// </summary>
    public Vector3Int[] GetRotatedFootprint(int rotationStep)
    {
        rotationStep = ((rotationStep % 4) + 4) % 4;
        if (rotationStep == 0)
            return footprint;

        Vector3Int[] rotated = new Vector3Int[footprint.Length];
        for (int i = 0; i < footprint.Length; i++)
        {
            rotated[i] = RotateCellY(footprint[i], rotationStep);
        }
        return rotated;
    }

    /// <summary>
    /// Returns the Y-axis rotation in degrees for a given rotation step.
    /// </summary>
    public float GetRotationDegrees(int rotationStep)
    {
        return ((rotationStep % 4 + 4) % 4) * 90f;
    }

    /// <summary>
    /// Rotate a single cell offset around Y axis by 90бу * steps.
    /// Y component is preserved (height unchanged).
    /// </summary>
    private static Vector3Int RotateCellY(Vector3Int cell, int steps)
    {
        int x = cell.x;
        int z = cell.z;

        for (int s = 0; s < steps; s++)
        {
            // 90бу clockwise around Y: (x, z) -> (z, -x)
            int newX = z;
            int newZ = -x;
            x = newX;
            z = newZ;
        }

        return new Vector3Int(x, cell.y, z);
    }
}

public enum Key_BuildablePP
{
    None = 0,

    // ---- Platforms ----
    Build_Platform_11 = 1,
    Build_Platform_12 = 2,
    Build_Platform_22 = 3,

    // ---- Rooms (5x5) ----
    Build_Room_5x5 = 10,

    // ---- Furniture ----
    Build_Furniture_Table = 20,
    Build_Furniture_Chair = 21,
}