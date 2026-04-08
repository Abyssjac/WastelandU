using JackyUtility;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Which logical layer this buildable occupies in the grid.
/// Multiple layers can coexist on the same XZ cell.
/// </summary>
public enum BuildLayer
{
    World = 0,
    Platform = 1,
    Room = 2,
}

/// <summary>
/// Surface type that can be required or provided.
/// A buildable's OccupancyZone declares what surface it requires beneath it,
/// and a SurfaceZone declares what surface it provides to others.
/// </summary>
public enum BuildSurfaceType
{
    None = 0,
    Platform = 1,
    PlatformSupporter = 2,
    Room = 3,
    Wall = 4,
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
    [Header("Cells (additive ¡ª boxes + manual cells merged)")]
    public FootprintBox[] boxes;
    public Vector3Int[] cells;

    [Header("Layer & Requirement")]
    [Tooltip("Which grid layer these cells occupy.")]
    public BuildLayer buildLayer;

    [Tooltip("What surface type is REQUIRED beneath these cells.\n" +
             "None = no requirement.")]
    public BuildSurfaceType requiredSurface;
}

/// <summary>
/// A surface zone: a region of cells where this buildable provides a surface for others.
/// Written into <c>surfaceMap</c>, does NOT block placement ¡ª only enables it for others.
/// Can extend beyond the buildable's own occupancy footprint.
/// </summary>
[System.Serializable]
public struct SurfaceZone
{
    [Header("Cells (additive ¡ª boxes + manual cells merged)")]
    public FootprintBox[] boxes;
    public Vector3Int[] cells;

    [Header("Surface")]
    [Tooltip("What surface type these cells provide.")]
    public BuildSurfaceType providedSurface;
}

// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

/// <summary>
/// Resolved cell data for a single occupancy cell, ready for grid operations.
/// </summary>
public struct ResolvedOccupancyCell
{
    public Vector3Int Cell;
    public BuildLayer Layer;
    public BuildSurfaceType RequiredSurface;
}

/// <summary>
/// Resolved cell data for a single surface cell, ready for grid operations.
/// </summary>
public struct ResolvedSurfaceCell
{
    public Vector3Int Cell;
    public BuildSurfaceType ProvidedSurface;
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
            buildLayer = BuildLayer.World,
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

                for (int i = 0; i < tmpCells.Count; i++)
                {
                    occList.Add(new ResolvedOccupancyCell
                    {
                        Cell = tmpCells[i],
                        Layer = zone.buildLayer,
                        RequiredSurface = zone.requiredSurface,
                    });
                    occCellSet.Add(tmpCells[i]);
                }
            }
        }

        if (occList.Count == 0)
        {
            occList.Add(new ResolvedOccupancyCell
            {
                Cell = Vector3Int.zero,
                Layer = BuildLayer.World,
                RequiredSurface = BuildSurfaceType.None,
            });
            occCellSet.Add(Vector3Int.zero);
        }

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
                    surfList.Add(new ResolvedSurfaceCell
                    {
                        Cell = tmpCells[i],
                        ProvidedSurface = zone.providedSurface,
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
                RequiredSurface = src[i].RequiredSurface,
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