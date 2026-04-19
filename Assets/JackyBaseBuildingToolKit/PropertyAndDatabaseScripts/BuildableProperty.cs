using JackyUtility;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which logical layer this buildable occupies in the grid.
/// Multiple layers can coexist on the same XZ cell.
/// </summary>
public enum BuildLayer
{
    BL_World = 0,
    BL_Platform = 1,
    BL_Room = 2,
    BL_Wall = 3,
    BL_Ground = 4,
    BL_EdgeAttacher = 5,
}

/// <summary>
/// Surface type that can be required or provided.
/// A buildable's OccupancyZone declares what surface it requires beneath it,
/// and a SurfaceZone declares what surface it provides to others.
/// </summary>
public enum BuildSurfaceType
{
    None = 0,
    //BST_Platform = 1,
    BST_PlatformSupporter = 2,
    //BST_Room = 3,
    BST_Wall = 4,
    BST_Ground = 5,
    BST_WallSupporter = 6,
}

/// <summary>
/// Directional facing for surface zones (e.g. which direction a wall faces).
/// Used to match wall-mounted items to the correct wall orientation.
/// </summary>
public enum SurfaceFacing
{
    None = 0,
    XPos = 1,
    XNeg = 2,
    ZPos = 3,
    ZNeg = 4,
    YPos = 5,   
    YNeg = 6,
}

/// <summary>
/// Bit-mask for selecting multiple occupancy facings at once.
/// Each set bit causes an additional <see cref="ResolvedOccupancyCell"/> to be emitted
/// during cache rebuild, sharing the same cell and layer.
/// </summary>
[System.Flags]
public enum FacingMask
{
    None     = 0,
    Cell     = 1 << 0,   // the cell itself (SurfaceFacing.None)
    XPos     = 1 << 1,
    XNeg     = 1 << 2,
    ZPos     = 1 << 3,
    ZNeg     = 1 << 4,
    YPos     = 1 << 5,
    YNeg     = 1 << 6,

    // ©¤©¤ Common presets ©¤©¤

    XWallFaces = XPos | XNeg,
    YWallFaces = YPos | YNeg,
    ZWallFaces = ZPos | ZNeg,
    AllWallFaces = XPos | XNeg | ZPos | ZNeg,
    AllFaces     = XPos | XNeg | ZPos | ZNeg | YPos | YNeg,
    Solid        = Cell | AllFaces,
}

/// <summary>
/// Defines a rectangular box region of cells via two diagonal corners.
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

// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
// Zone definitions ¡ª each zone is a region of cells with its own layer / surface config
// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

/// <summary>
/// An occupancy zone: a region of cells that this buildable physically occupies.
/// Written into <c>occupancyMap</c>, blocks other buildables on the same layer.
/// </summary>
[System.Serializable]
public struct OccupancyZone
{
    public string zoneName;  // for debugging and readability in the inspector
    [Header("Cells (additive ¡ª boxes + manual cells merged)")]
    public FootprintBox[] boxes;
    public Vector3Int[] cells;

    [Header("Layer & Requirement")]
    [Tooltip("Which grid layer these cells occupy.")]
    public BuildLayer buildLayer;

    [Tooltip("Which facings these cells occupy.\n" +
             "Cell = the cell itself (non-directional).\n" +
             "XPos/XNeg/ZPos/ZNeg/YPos/YNeg = directional faces.\n" +
             "Use Solid for full-volume objects.")]
    public FacingMask occupancyFacings;

    [Tooltip("What surface type is REQUIRED beneath these cells.\n" +
             "None = no requirement.")]
    public BuildSurfaceType requiredSurface;

    [Tooltip("Required surface facing direction.\n" +
             "None = no facing requirement (floors, platforms).\n" +
             "Set to a direction for wall-mounted items.")]
    public SurfaceFacing requiredFacing;
}

/// <summary>
/// A surface zone: a region of cells where this buildable provides a surface for others.
/// Written into <c>surfaceMap</c>, does NOT block placement ¡ª only enables it for others.
/// Can extend beyond the buildable's own occupancy footprint.
/// </summary>
[System.Serializable]
public struct SurfaceZone
{
    public string zoneName;  // for debugging and readability in the inspector
    [Header("Cells (additive ¡ª boxes + manual cells merged)")]
    public FootprintBox[] boxes;
    public Vector3Int[] cells;

    [Header("Surface")]
    [Tooltip("What surface type these cells provide.")]
    public BuildSurfaceType providedSurface;

    [Tooltip("Facing direction of this surface.\n" +
             "None = non-directional (floors, platforms).\n" +
             "Set to a direction for walls.")]
    public SurfaceFacing facing;
}

// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

/// <summary>
/// Resolved cell data for a single occupancy cell, ready for grid operations.
/// </summary>
public struct ResolvedOccupancyCell
{
    public Vector3Int Cell;
    public BuildLayer Layer;
    public SurfaceFacing OccupancyFacing;
    public BuildSurfaceType RequiredSurface;
    public SurfaceFacing RequiredFacing;
}

/// <summary>
/// Resolved cell data for a single surface cell, ready for grid operations.
/// </summary>
public struct ResolvedSurfaceCell
{
    public Vector3Int Cell;
    public BuildSurfaceType ProvidedSurface;
    public SurfaceFacing Facing;
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

    [Header("Occupancy Zones")]
    [Tooltip("Regions this buildable physically occupies. Each zone has its own layer and surface requirement.")]
    public OccupancyZone[] occupancyZones = new OccupancyZone[]
    {
        new OccupancyZone
        {
            boxes = new FootprintBox[0],
            cells = new Vector3Int[] { Vector3Int.zero },
            buildLayer = BuildLayer.BL_World,
            requiredSurface = BuildSurfaceType.None,
        }
    };

    [Header("Surface Zones")]
    [Tooltip("Regions where this buildable provides surfaces for other buildables.\n" +
             "Can extend beyond the occupancy footprint (e.g. PlatformSupporter area around a platform).")]
    public SurfaceZone[] surfaceZones = new SurfaceZone[0];

    [Header("Placement Rules")]
    public bool canRotate = true;
    public bool canMove = true;

    [Header("UI Display")]
    public Sprite iconSprite;
    public string displayName;

    // ©¤©¤©¤ Cache ©¤©¤©¤
    private ResolvedOccupancyCell[] cachedOccupancy;
    private ResolvedSurfaceCell[] cachedSurface;
    private Vector3Int[] cachedOccupancyCellsOnly;  // just the cell positions, for preview / legacy
    private bool dirty = true;

    private void OnValidate() { dirty = true; }
    private void OnEnable() { dirty = true; }

    private void RebuildCache()
    {
        if (!dirty && cachedOccupancy != null) return;

        // ©¤©¤ Occupancy ©¤©¤
        List<ResolvedOccupancyCell> occList = new List<ResolvedOccupancyCell>();
        HashSet<Vector3Int> occCellSet = new HashSet<Vector3Int>();

        if (occupancyZones != null)
        {
            List<Vector3Int> tmpCells = new List<Vector3Int>();
            for (int z = 0; z < occupancyZones.Length; z++)
            {
                var zone = occupancyZones[z];
                tmpCells.Clear();

                if (zone.boxes != null)
                    for (int b = 0; b < zone.boxes.Length; b++)
                        zone.boxes[b].GenerateCells(tmpCells);

                if (zone.cells != null)
                    for (int c = 0; c < zone.cells.Length; c++)
                        tmpCells.Add(zone.cells[c]);

                // Expand FacingMask into individual resolved cells
                SurfaceFacing[] expandedFacings = ExpandFacingMask(zone.occupancyFacings);

                for (int i = 0; i < tmpCells.Count; i++)
                {
                    for (int f = 0; f < expandedFacings.Length; f++)
                    {
                        occList.Add(new ResolvedOccupancyCell
                        {
                            Cell = tmpCells[i],
                            Layer = zone.buildLayer,
                            OccupancyFacing = expandedFacings[f],
                            RequiredSurface = zone.requiredSurface,
                            RequiredFacing = zone.requiredFacing,
                        });
                    }
                    occCellSet.Add(tmpCells[i]);
                }
            }
        }

        if (occList.Count == 0)
        {
            occList.Add(new ResolvedOccupancyCell
            {
                Cell = Vector3Int.zero,
                Layer = BuildLayer.BL_World,
                OccupancyFacing = SurfaceFacing.None,
                RequiredSurface = BuildSurfaceType.None,
            });
            occCellSet.Add(Vector3Int.zero);
        }

        // NOTE: If an OccupancyZone has occupancyFacings == FacingMask.None,
        // ExpandFacingMask returns an empty array and the zone produces zero resolved cells.
        // This is intentional ¡ª a zone with no facings occupies nothing.

        cachedOccupancy = occList.ToArray();

        // occupancy cell positions only (deduplicated, for preview)
        cachedOccupancyCellsOnly = new Vector3Int[occCellSet.Count];
        occCellSet.CopyTo(cachedOccupancyCellsOnly);

        // ©¤©¤ Surface ©¤©¤
        List<ResolvedSurfaceCell> surfList = new List<ResolvedSurfaceCell>();

        if (surfaceZones != null)
        {
            List<Vector3Int> tmpCells = new List<Vector3Int>();
            for (int z = 0; z < surfaceZones.Length; z++)
            {
                var zone = surfaceZones[z];
                tmpCells.Clear();

                if (zone.boxes != null)
                    for (int b = 0; b < zone.boxes.Length; b++)
                        zone.boxes[b].GenerateCells(tmpCells);

                if (zone.cells != null)
                    for (int c = 0; c < zone.cells.Length; c++)
                        tmpCells.Add(zone.cells[c]);

                for (int i = 0; i < tmpCells.Count; i++)
                {
                    //Debug.Log($"[BuildableProperty:{enumKey}] SurfZone[{z}] cell={tmpCells[i]} surface={zone.providedSurface} facing={zone.facing} (raw={(int)zone.facing})");
                    surfList.Add(new ResolvedSurfaceCell
                    {
                        Cell = tmpCells[i],
                        ProvidedSurface = zone.providedSurface,
                        Facing = zone.facing,
                    });
                }
            }
        }

        cachedSurface = surfList.ToArray();
        dirty = false;
    }

    // ©¤©¤©¤ Public API ©¤©¤©¤

    /// <summary>
    /// All resolved occupancy cells (with per-cell layer and required surface), unrotated.
    /// </summary>
    public ResolvedOccupancyCell[] GetOccupancyCells()
    {
        RebuildCache();
        return cachedOccupancy;
    }

    /// <summary>
    /// All resolved surface cells (with per-cell provided surface), unrotated.
    /// </summary>
    public ResolvedSurfaceCell[] GetSurfaceCells()
    {
        RebuildCache();
        return cachedSurface;
    }

    /// <summary>
    /// Occupancy cells rotated by rotation step. Each cell's Layer and RequiredSurface preserved.
    /// </summary>
    public ResolvedOccupancyCell[] GetRotatedOccupancyCells(int rotationStep)
    {
        ResolvedOccupancyCell[] src = GetOccupancyCells();
        rotationStep = ((rotationStep % 4) + 4) % 4;
        if (rotationStep == 0) return src;

        ResolvedOccupancyCell[] result = new ResolvedOccupancyCell[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            result[i] = new ResolvedOccupancyCell
            {
                Cell = RotateCellY(src[i].Cell, rotationStep),
                Layer = src[i].Layer,
                OccupancyFacing = RotateFacing(src[i].OccupancyFacing, rotationStep),
                RequiredSurface = src[i].RequiredSurface,
                RequiredFacing = RotateFacing(src[i].RequiredFacing, rotationStep),
            };
        }
        return result;
    }

    /// <summary>
    /// Surface cells rotated by rotation step. Each cell's ProvidedSurface preserved.
    /// </summary>
    public ResolvedSurfaceCell[] GetRotatedSurfaceCells(int rotationStep)
    {
        ResolvedSurfaceCell[] src = GetSurfaceCells();
        rotationStep = ((rotationStep % 4) + 4) % 4;
        if (rotationStep == 0) return src;

        ResolvedSurfaceCell[] result = new ResolvedSurfaceCell[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            result[i] = new ResolvedSurfaceCell
            {
                Cell = RotateCellY(src[i].Cell, rotationStep),
                ProvidedSurface = src[i].ProvidedSurface,
                Facing = RotateFacing(src[i].Facing, rotationStep),
            };
        }
        return result;
    }

    /// <summary>
    /// Just the occupancy cell positions (deduplicated), for preview highlighting.
    /// </summary>
    public Vector3Int[] GetFootprint()
    {
        RebuildCache();
        return cachedOccupancyCellsOnly;
    }

    /// <summary>
    /// Rotated occupancy cell positions for preview highlighting.
    /// </summary>
    public Vector3Int[] GetRotatedFootprint(int rotationStep)
    {
        Vector3Int[] src = GetFootprint();
        rotationStep = ((rotationStep % 4) + 4) % 4;
        if (rotationStep == 0) return src;

        Vector3Int[] result = new Vector3Int[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            result[i] = RotateCellY(src[i], rotationStep);
        }
        return result;
    }

    public float GetRotationDegrees(int rotationStep)
    {
        return ((rotationStep % 4 + 4) % 4) * 90f;
    }

    // ©¤©¤©¤ FacingMask Expansion ©¤©¤©¤

    private static readonly FacingMask[] s_allBits =
    {
        FacingMask.Cell,
        FacingMask.XPos,
        FacingMask.XNeg,
        FacingMask.ZPos,
        FacingMask.ZNeg,
        FacingMask.YPos,
        FacingMask.YNeg,
    };

    private static readonly SurfaceFacing[] s_bitToFacing =
    {
        SurfaceFacing.None,   // Cell  ¡ú None (the cell itself)
        SurfaceFacing.XPos,
        SurfaceFacing.XNeg,
        SurfaceFacing.ZPos,
        SurfaceFacing.ZNeg,
        SurfaceFacing.YPos,
        SurfaceFacing.YNeg,
    };

    /// <summary>
    /// Expand a <see cref="FacingMask"/> into an array of individual <see cref="SurfaceFacing"/> values.
    /// Each set bit produces one entry.
    /// </summary>
    public static SurfaceFacing[] ExpandFacingMask(FacingMask mask)
    {
        if (mask == FacingMask.None) return System.Array.Empty<SurfaceFacing>();

        List<SurfaceFacing> result = new List<SurfaceFacing>(7);
        for (int i = 0; i < s_allBits.Length; i++)
        {
            if ((mask & s_allBits[i]) != 0)
                result.Add(s_bitToFacing[i]);
        }
        return result.ToArray();
    }

    /// <summary>
    /// Rotate a SurfaceFacing by 90-degree steps around Y axis.
    /// Matches the same rotation convention as RotateCellY.
    /// </summary>
    public static SurfaceFacing RotateFacing(SurfaceFacing facing, int steps)
    {
        if (facing == SurfaceFacing.None) return SurfaceFacing.None;
        //if (facing == SurfaceFacing.YPos || facing == SurfaceFacing.YNeg) return facing;

        steps = ((steps % 4) + 4) % 4;
        if (steps == 0) return facing;

        // Rotation order (clockwise around Y when viewed from above, same as RotateCellY):
        // (x,z) -> (z,-x) means:
        // XNeg(-1,0) -> ZPos(0,1) -> XPos(1,0) -> ZNeg(0,-1) -> XNeg
        SurfaceFacing[] cycle = { SurfaceFacing.XNeg, SurfaceFacing.ZPos, SurfaceFacing.XPos, SurfaceFacing.ZNeg };
        int idx = -1;
        for (int i = 0; i < 4; i++)
        {
            if (cycle[i] == facing) { idx = i; break; }
        }
        if (idx < 0) return facing;
        return cycle[(idx + steps) % 4];
    }

    public static Vector3Int RotateCellY(Vector3Int cell, int steps)
    {
        steps = ((steps % 4) + 4) % 4;
        int x = cell.x;
        int z = cell.z;

        for (int s = 0; s < steps; s++)
        {
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
    Build_Base_Platform_Normal_Free_0 = 1,
    Build_Base_Platform_Normal_0 = 2,
    Build_Base_Platform_Elevated_0 = 3,
    Build_Base_Platform_Elevated_1 = 4,

    // ---- Rooms (5x5) ----
    Build_Room_5x5 = 10,
    Build_Base_Wall_XNegPos_1Level_0 = 11,
    Build_Base_Wall_XPosNeg_1Level_0 = 12,
    Build_Base_Wall_ZNegPos_1Level_0 = 13,
    Build_Base_Wall_ZPosNeg_1Level_0 = 14,

    Build_Base_Slope_2Level_0 = 20,

    Build_Bookstore_Bookshelf_0 = 30,
    Build_Bookstore_Bookshelf_1 = 31,
    Build_Bookstore_Bookshelf_2 = 32,
    Build_Bookstore_Bookshelf_3 = 33,
    Build_Bookstore_Bookshelf_4 = 34,
    Build_Bookstore_Table_0 = 35,
    Build_Bookstore_Couch_0 = 36,
    Build_Bookstore_Plant_0 = 37,
    Build_Bookstore_Plant_1 = 38,
    Build_Bookstore_Stepladder_0 = 39,

    Build_Artstudio_Easel_0 = 50,
    Build_Artstudio_Easel_1 = 51,
    Build_Artstudio_Easel_2 = 52,
    Build_Artstudio_Chair_0 = 53,
    Build_Artstudio_Chair_1 = 54,
    Build_Artstudio_Plant_0 = 55,
    Build_Artstudio_Table_0 = 56,
    Build_Artstudio_Canvastack_0 = 57,

    Build_Bar_Plant_0 = 60,
    Build_Bar_Shelf_0 = 61,
    Build_Bar_Stool_0 = 62,
    Build_Bar_Table_0 = 63,
    Build_Bar_Table_1 = 64,
    Build_Bar_Carpet_0 = 65,
    Build_Bar_Cocktailneonsign_0 = 66,
    Build_Bar_Openneonsign_0 = 67,





    BuildM_Cube11_0 = 300,
    BuildM_Cube12_0 = 301,
    BuildM_Cube22_0 = 302,
}