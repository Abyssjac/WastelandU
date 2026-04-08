using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composite key for the layered occupancy map: (cell position, build layer).
/// </summary>
public struct CellLayerKey : System.IEquatable<CellLayerKey>
{
    public Vector3Int Cell;
    public BuildLayer Layer;

    public CellLayerKey(Vector3Int cell, BuildLayer layer)
    {
        Cell = cell;
        Layer = layer;
    }

    public bool Equals(CellLayerKey other)
    {
        return Cell.Equals(other.Cell) && Layer == other.Layer;
    }

    public override bool Equals(object obj)
    {
        return obj is CellLayerKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked { return Cell.GetHashCode() * 397 ^ (int)Layer; }
    }

    public override string ToString() => $"({Cell}, {Layer})";
}

/// <summary>
/// One entry in the surface map: what surface type is provided and by whom.
/// </summary>
public struct SurfaceEntry
{
    public BuildSurfaceType SurfaceType;
    public string OwnerInstanceId;

    public SurfaceEntry(BuildSurfaceType surfaceType, string ownerInstanceId)
    {
        SurfaceType = surfaceType;
        OwnerInstanceId = ownerInstanceId;
    }
}

/// <summary>
/// Pure-data 3D grid that tracks cell occupancy and surface provision across <see cref="BuildLayer"/>s.
/// </summary>
[System.Serializable]
public class BuildGrid3D
{
    // (cell, layer) -> occupant
    private Dictionary<CellLayerKey, PlacedBuildableData> occupancyMap
        = new Dictionary<CellLayerKey, PlacedBuildableData>();

    // cell -> list of (surfaceType, ownerInstanceId)
    private Dictionary<Vector3Int, List<SurfaceEntry>> surfaceMap
        = new Dictionary<Vector3Int, List<SurfaceEntry>>();

    // instanceId -> PlacedBuildableData
    private Dictionary<string, PlacedBuildableData> allPlaced
        = new Dictionary<string, PlacedBuildableData>();

    [SerializeField] private Vector3Int gridMin;
    [SerializeField] private Vector3Int gridMax;
    [SerializeField] private int placedCount;
    [SerializeField] private int occupiedCellCount;
    [SerializeField] private int surfaceCellCount;

    public Vector3Int GridMin => gridMin;
    public Vector3Int GridMax => gridMax;
    public IReadOnlyDictionary<string, PlacedBuildableData> AllPlaced => allPlaced;
    public IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> OccupancyMap => occupancyMap;
    public IReadOnlyDictionary<Vector3Int, List<SurfaceEntry>> SurfaceMap => surfaceMap;

    public BuildGrid3D() { }

    public BuildGrid3D(Vector3Int gridMin, Vector3Int gridMax)
    {
        this.gridMin = gridMin;
        this.gridMax = gridMax;
    }

    public void Initialize()
    {
        occupancyMap = new Dictionary<CellLayerKey, PlacedBuildableData>();
        surfaceMap = new Dictionary<Vector3Int, List<SurfaceEntry>>();
        allPlaced = new Dictionary<string, PlacedBuildableData>();
        SyncInspectorStats();
    }

    // --------- Query ---------

    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep)
    {
        ResolvedOccupancyCell[] occ = property.GetRotatedOccupancyCells(rotationStep);

        for (int i = 0; i < occ.Length; i++)
        {
            Vector3Int worldCell = anchor + occ[i].Cell;
            if (!IsInBounds(worldCell)) return false;

            var key = new CellLayerKey(worldCell, occ[i].Layer);
            if (occupancyMap.ContainsKey(key)) return false;

            if (occ[i].RequiredSurface != BuildSurfaceType.None)
            {
                if (!HasSurface(worldCell, occ[i].RequiredSurface))
                    return false;
            }
        }
        return true;
    }

    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep, out string failReason)
    {
        failReason = null;
        ResolvedOccupancyCell[] occ = property.GetRotatedOccupancyCells(rotationStep);

        for (int i = 0; i < occ.Length; i++)
        {
            Vector3Int worldCell = anchor + occ[i].Cell;
            if (!IsInBounds(worldCell))
            {
                failReason = $"Cell {worldCell} out of bounds (min={gridMin}, max={gridMax})";
                return false;
            }

            var key = new CellLayerKey(worldCell, occ[i].Layer);
            if (occupancyMap.ContainsKey(key))
            {
                failReason = $"Cell {key} already occupied by '{occupancyMap[key].InstanceId}'";
                return false;
            }

            if (occ[i].RequiredSurface != BuildSurfaceType.None)
            {
                if (!HasSurface(worldCell, occ[i].RequiredSurface))
                {
                    failReason = $"Cell {worldCell} missing required surface '{occ[i].RequiredSurface}'";
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Checks whether any surface entry at the given cell provides the required surface type.
    /// </summary>
    public bool HasSurface(Vector3Int cell, BuildSurfaceType required)
    {
        if (!surfaceMap.TryGetValue(cell, out List<SurfaceEntry> entries))
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].SurfaceType == required)
                return true;
        }
        return false;
    }

    public bool IsCellOccupied(Vector3Int cell, BuildLayer layer)
    {
        return occupancyMap.ContainsKey(new CellLayerKey(cell, layer));
    }

    public PlacedBuildableData GetOccupant(Vector3Int cell, BuildLayer layer)
    {
        occupancyMap.TryGetValue(new CellLayerKey(cell, layer), out PlacedBuildableData data);
        return data;
    }

    public PlacedBuildableData GetTopmostOccupant(Vector3Int cell)
    {
        for (int l = (int)BuildLayer.Room; l >= (int)BuildLayer.World; l--)
        {
            var key = new CellLayerKey(cell, (BuildLayer)l);
            if (occupancyMap.TryGetValue(key, out PlacedBuildableData data))
                return data;
        }
        return null;
    }

    /// <summary>
    /// Find a parent buildable at the given cell that provides the required surface.
    /// </summary>
    public PlacedBuildableData FindParentAt(Vector3Int cell, BuildSurfaceType requiredSurface)
    {
        if (requiredSurface == BuildSurfaceType.None) return null;

        if (!surfaceMap.TryGetValue(cell, out List<SurfaceEntry> entries))
            return null;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].SurfaceType == requiredSurface)
            {
                if (allPlaced.TryGetValue(entries[i].OwnerInstanceId, out PlacedBuildableData owner))
                    return owner;
            }
        }
        return null;
    }

    public bool IsInBounds(Vector3Int cell)
    {
        return cell.x >= gridMin.x && cell.x < gridMax.x
            && cell.y >= gridMin.y && cell.y < gridMax.y
            && cell.z >= gridMin.z && cell.z < gridMax.z;
    }

    // --------- Mutate ---------

    public bool TryPlace(PlacedBuildableData data)
    {
        if (!CanPlace(data.Property, data.AnchorCell, data.RotationStep))
            return false;

        WritePlacement(data);
        return true;
    }

    public bool TryRemove(string instanceId)
    {
        return TryRemove(instanceId, out _);
    }

    public bool TryRemove(string instanceId, out string failReason)
    {
        failReason = null;
        if (!allPlaced.TryGetValue(instanceId, out PlacedBuildableData data))
        {
            failReason = $"Instance '{instanceId}' not found.";
            return false;
        }

        if (data.ChildrenIds.Count > 0)
        {
            failReason = $"Cannot remove '{instanceId}': has {data.ChildrenIds.Count} children. Remove them first.";
            return false;
        }

        if (data.ParentId != null && allPlaced.TryGetValue(data.ParentId, out PlacedBuildableData parent))
        {
            parent.ChildrenIds.Remove(instanceId);
        }

        ErasePlacement(data);
        return true;
    }

    internal void ForceRemoveFromGrid(PlacedBuildableData data)
    {
        ErasePlacement(data);
    }

    internal void ForcePlaceIntoGrid(PlacedBuildableData data)
    {
        WritePlacement(data);
    }

    // --- Core write / erase ---

    private void WritePlacement(PlacedBuildableData data)
    {
        // Occupancy
        ResolvedOccupancyCell[] occ = data.Property.GetRotatedOccupancyCells(data.RotationStep);
        for (int i = 0; i < occ.Length; i++)
        {
            Vector3Int worldCell = data.AnchorCell + occ[i].Cell;
            occupancyMap[new CellLayerKey(worldCell, occ[i].Layer)] = data;
        }

        // Surface
        ResolvedSurfaceCell[] surf = data.Property.GetRotatedSurfaceCells(data.RotationStep);
        for (int i = 0; i < surf.Length; i++)
        {
            Vector3Int worldCell = data.AnchorCell + surf[i].Cell;
            if (!surfaceMap.TryGetValue(worldCell, out List<SurfaceEntry> list))
            {
                list = new List<SurfaceEntry>(2);
                surfaceMap[worldCell] = list;
            }
            list.Add(new SurfaceEntry(surf[i].ProvidedSurface, data.InstanceId));
        }

        allPlaced[data.InstanceId] = data;
        SyncInspectorStats();
    }

    private void ErasePlacement(PlacedBuildableData data)
    {
        // Occupancy
        ResolvedOccupancyCell[] occ = data.Property.GetRotatedOccupancyCells(data.RotationStep);
        for (int i = 0; i < occ.Length; i++)
        {
            Vector3Int worldCell = data.AnchorCell + occ[i].Cell;
            occupancyMap.Remove(new CellLayerKey(worldCell, occ[i].Layer));
        }

        // Surface
        ResolvedSurfaceCell[] surf = data.Property.GetRotatedSurfaceCells(data.RotationStep);
        for (int i = 0; i < surf.Length; i++)
        {
            Vector3Int worldCell = data.AnchorCell + surf[i].Cell;
            if (surfaceMap.TryGetValue(worldCell, out List<SurfaceEntry> list))
            {
                list.RemoveAll(e => e.OwnerInstanceId == data.InstanceId && e.SurfaceType == surf[i].ProvidedSurface);
                if (list.Count == 0)
                    surfaceMap.Remove(worldCell);
            }
        }

        allPlaced.Remove(data.InstanceId);
        SyncInspectorStats();
    }

    // --------- Move with children ---------

    public bool TryMoveWithChildren(string instanceId, Vector3Int newAnchor, int newRotationStep, out string failReason)
    {
        failReason = null;

        if (!allPlaced.TryGetValue(instanceId, out PlacedBuildableData data))
        {
            failReason = $"Instance '{instanceId}' not found.";
            return false;
        }

        Vector3Int delta = newAnchor - data.AnchorCell;

        List<PlacedBuildableData> movedSet = new List<PlacedBuildableData>();
        CollectSelfAndChildren(data, movedSet);

        Vector3Int[] oldAnchors = new Vector3Int[movedSet.Count];
        int[] oldRotations = new int[movedSet.Count];
        for (int i = 0; i < movedSet.Count; i++)
        {
            oldAnchors[i] = movedSet[i].AnchorCell;
            oldRotations[i] = movedSet[i].RotationStep;
        }

        for (int i = 0; i < movedSet.Count; i++)
            ForceRemoveFromGrid(movedSet[i]);

        data.AnchorCell = newAnchor;
        data.RotationStep = newRotationStep;
        for (int i = 1; i < movedSet.Count; i++)
        {
            movedSet[i].AnchorCell = oldAnchors[i] + delta;
        }

        if (!CanPlace(data.Property, newAnchor, newRotationStep))
        {
            failReason = $"Parent cannot be placed at {newAnchor}.";
            Rollback(movedSet, oldAnchors, oldRotations);
            return false;
        }

        ForcePlaceIntoGrid(data);

        for (int i = 1; i < movedSet.Count; i++)
        {
            var child = movedSet[i];
            if (!CanPlace(child.Property, child.AnchorCell, child.RotationStep))
            {
                failReason = $"Child '{child.InstanceId}' cannot be placed at {child.AnchorCell}.";
                ForceRemoveFromGrid(data);
                for (int j = 1; j < i; j++)
                    ForceRemoveFromGrid(movedSet[j]);
                Rollback(movedSet, oldAnchors, oldRotations);
                return false;
            }
            ForcePlaceIntoGrid(child);
        }

        SyncInspectorStats();
        return true;
    }

    private void Rollback(List<PlacedBuildableData> movedSet, Vector3Int[] oldAnchors, int[] oldRotations)
    {
        for (int i = 0; i < movedSet.Count; i++)
        {
            movedSet[i].AnchorCell = oldAnchors[i];
            movedSet[i].RotationStep = oldRotations[i];
            ForcePlaceIntoGrid(movedSet[i]);
        }
    }

    private void CollectSelfAndChildren(PlacedBuildableData root, List<PlacedBuildableData> result)
    {
        result.Add(root);
        for (int i = 0; i < root.ChildrenIds.Count; i++)
        {
            if (allPlaced.TryGetValue(root.ChildrenIds[i], out PlacedBuildableData child))
            {
                CollectSelfAndChildren(child, result);
            }
        }
    }

    // --------- Stats ---------

    private void SyncInspectorStats()
    {
        placedCount = allPlaced.Count;
        occupiedCellCount = occupancyMap.Count;
        surfaceCellCount = surfaceMap.Count;
    }

    // --------- Utility ---------

    public static Vector3Int[] GetWorldCells(Vector3Int anchor, Vector3Int[] footprintOffsets)
    {
        Vector3Int[] result = new Vector3Int[footprintOffsets.Length];
        for (int i = 0; i < footprintOffsets.Length; i++)
        {
            result[i] = anchor + footprintOffsets[i];
        }
        return result;
    }
}


/// <summary>
/// Runtime data for one placed buildable instance.
/// </summary>
public class PlacedBuildableData
{
    public string InstanceId;
    public BuildableProperty Property;
    public Vector3Int AnchorCell;
    public int RotationStep;
    public GameObject SpawnedObject;

    // --- Hierarchy ---
    public string ParentId;
    public List<string> ChildrenIds = new List<string>();

    public void SetParent(PlacedBuildableData parent)
    {
        if (parent == null)
        {
            ParentId = null;
            return;
        }
        ParentId = parent.InstanceId;
        if (!parent.ChildrenIds.Contains(InstanceId))
            parent.ChildrenIds.Add(InstanceId);
    }

    public Vector3Int[] GetEffectiveFootprint()
    {
        return Property.GetRotatedFootprint(RotationStep);
    }

    public Vector3Int[] GetEffectiveWorldCells()
    {
        Vector3Int[] offsets = GetEffectiveFootprint();
        Vector3Int[] result = new Vector3Int[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            result[i] = AnchorCell + offsets[i];
        }
        return result;
    }
}
