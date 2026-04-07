using JackyUtility;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which logical layer this buildable occupies in the grid.
/// Multiple layers can coexist on the same XZ cell.
/// </summary>
public enum BuildLayer
{
    /// <summary>The world ground layer ¡ª platforms are placed here.</summary>
    World = 0,
    /// <summary>On top of a platform ¡ª rooms, stairs, etc.</summary>
    Platform = 1,
    /// <summary>Inside a room ¡ª furniture, decorations, etc.</summary>
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

/// <summary>
/// Defines a rectangular box region of footprint cells.
/// Two opposite corners of a 3D box (like a cube's diagonal) ¡ú generates all cells within.
/// </summary>
[System.Serializable]
public struct FootprintBox
{
    [Tooltip("First corner of the box (diagonal vertex A).")]
    public Vector3Int cornerA;

    [Tooltip("Second corner of the box (diagonal vertex B).")]
    public Vector3Int cornerB;

    public FootprintBox(Vector3Int cornerA, Vector3Int cornerB)
    {
        this.cornerA = cornerA;
        this.cornerB = cornerB;
    }

    /// <summary>
    /// Expand all cells this box covers into the target list.
    /// </summary>
    public void GenerateCells(List<Vector3Int> target)
    {
        int minX = Mathf.Min(cornerA.x, cornerB.x);
        int maxX = Mathf.Max(cornerA.x, cornerB.x);
        int minY = Mathf.Min(cornerA.y, cornerB.y);
        int maxY = Mathf.Max(cornerA.y, cornerB.y);
        int minZ = Mathf.Min(cornerA.z, cornerB.z);
        int maxZ = Mathf.Max(cornerA.z, cornerB.z);

        for (int y = minY; y <= maxY; y++)
            for (int z = minZ; z <= maxZ; z++)
                for (int x = minX; x <= maxX; x++)
                    target.Add(new Vector3Int(x, y, z));
    }
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

    [Header("Grid Footprint ¡ª Boxes (additive)")]
    [Tooltip("Each box generates a rectangular region of cells. All boxes are merged together.")]
    public FootprintBox[] footprintBoxes = new FootprintBox[0];

    [Header("Grid Footprint ¡ª Manual Cells (additive)")]
    [Tooltip("Individual cell offsets added on top of the box-generated cells.\n" +
             "Use this for fine-tuning or irregular shapes.")]
    public Vector3Int[] footprintCells = new Vector3Int[] { Vector3Int.zero };

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

    // ©¤©¤©¤ Cached footprint (generated on first access) ©¤©¤©¤
    private Vector3Int[] cachedFootprint;
    private bool footprintDirty = true;

    private void OnValidate()
    {
        footprintDirty = true;
    }

    private void OnEnable()
    {
        footprintDirty = true;
    }

    /// <summary>
    /// Returns the merged footprint: all boxes expanded + all manual cells, deduplicated.
    /// Cached until the SO is modified.
    /// </summary>
    public Vector3Int[] GetFootprint()
    {
        if (!footprintDirty && cachedFootprint != null)
            return cachedFootprint;

        HashSet<Vector3Int> cellSet = new HashSet<Vector3Int>();

        // Expand all boxes
        if (footprintBoxes != null)
        {
            List<Vector3Int> boxCells = new List<Vector3Int>();
            for (int i = 0; i < footprintBoxes.Length; i++)
            {
                footprintBoxes[i].GenerateCells(boxCells);
            }
            for (int i = 0; i < boxCells.Count; i++)
            {
                cellSet.Add(boxCells[i]);
            }
        }

        // Add manual cells
        if (footprintCells != null)
        {
            for (int i = 0; i < footprintCells.Length; i++)
            {
                cellSet.Add(footprintCells[i]);
            }
        }

        // Fallback: ensure at least origin
        if (cellSet.Count == 0)
            cellSet.Add(Vector3Int.zero);

        cachedFootprint = new Vector3Int[cellSet.Count];
        cellSet.CopyTo(cachedFootprint);
        footprintDirty = false;
        return cachedFootprint;
    }

    /// <summary>
    /// Returns the footprint cells rotated by 90¡ã steps around Y axis.
    /// rotationStep: 0=0¡ã, 1=90¡ã, 2=180¡ã, 3=270¡ã
    /// </summary>
    public Vector3Int[] GetRotatedFootprint(int rotationStep)
    {
        Vector3Int[] resolved = GetFootprint();

        rotationStep = ((rotationStep % 4) + 4) % 4;
        if (rotationStep == 0)
            return resolved;

        Vector3Int[] rotated = new Vector3Int[resolved.Length];
        for (int i = 0; i < resolved.Length; i++)
        {
            rotated[i] = RotateCellY(resolved[i], rotationStep);
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
    /// Rotate a single cell offset around Y axis by 90¡ã * steps.
    /// Y component is preserved (height unchanged).
    /// </summary>
    private static Vector3Int RotateCellY(Vector3Int cell, int steps)
    {
        int x = cell.x;
        int z = cell.z;

        for (int s = 0; s < steps; s++)
        {
            // 90¡ã clockwise around Y: (x, z) -> (z, -x)
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
    Build_Platform_99 = 4,

    // ---- Rooms (5x5) ----
    Build_Room_5x5 = 10,

    // ---- Furniture ----
    Build_Furniture_Table = 20,
    Build_Furniture_Chair = 21,
}