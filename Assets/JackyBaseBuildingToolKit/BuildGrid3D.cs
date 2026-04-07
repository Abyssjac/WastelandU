using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composite key for the layered occupancy map: (cell position, build layer).
/// Two buildables on the same XZ cell but different layers do not conflict.
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
        unchecked
        {
            return Cell.GetHashCode() * 397 ^ (int)Layer;
        }
    }

    public override string ToString() => $"({Cell}, {Layer})";
}

/// <summary>
/// Pure-data 3D grid that tracks cell occupancy across multiple <see cref="BuildLayer"/>s.
/// No MonoBehaviour �� owned and driven by BuildManager.
/// Serializable so that inspector can display grid bounds and stats.
/// </summary>
[System.Serializable]
public class BuildGrid3D
{
    // (cell, layer) �� the PlacedBuildableData that occupies it (null = free)
    private Dictionary<CellLayerKey, PlacedBuildableData> occupancyMap
        = new Dictionary<CellLayerKey, PlacedBuildableData>();

    // instanceId �� PlacedBuildableData (for fast lookup / iteration)
    private Dictionary<string, PlacedBuildableData> allPlaced
        = new Dictionary<string, PlacedBuildableData>();

    // boundary limits (serialized for inspector)
    [SerializeField] private Vector3Int gridMin;
    [SerializeField] private Vector3Int gridMax;

    // inspector-friendly read-only stats (updated at runtime)
    [SerializeField] private int placedCount;
    [SerializeField] private int occupiedCellCount;

    public Vector3Int GridMin => gridMin;
    public Vector3Int GridMax => gridMax;
    public IReadOnlyDictionary<string, PlacedBuildableData> AllPlaced => allPlaced;
    public IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> OccupancyMap => occupancyMap;

    public BuildGrid3D() { }

    public BuildGrid3D(Vector3Int gridMin, Vector3Int gridMax)
    {
        this.gridMin = gridMin;
        this.gridMax = gridMax;
    }

    public void Initialize()
    {
        occupancyMap = new Dictionary<CellLayerKey, PlacedBuildableData>();
        allPlaced = new Dictionary<string, PlacedBuildableData>();
        SyncInspectorStats();
    }

    // ������������������ Query ������������������

    /// <summary>
    /// Returns true if every cell the buildable would occupy is free on its layer,
    /// in-bounds, and has the required surface from the layer below.
    /// </summary>
    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep)
    {
        BuildLayer layer = property.buildLayer;
        BuildSurfaceType required = property.requiredSurface;
        Vector3Int[] offsets = property.GetRotatedFootprint(rotationStep);

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3Int worldCell = anchor + offsets[i];
            if (!IsInBounds(worldCell)) return false;

            // same-layer collision check
            var key = new CellLayerKey(worldCell, layer);
            if (occupancyMap.ContainsKey(key)) return false;

            // surface requirement check
            if (required != BuildSurfaceType.None)
            {
                if (!HasSurfaceProvider(worldCell, required))
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Same as CanPlace but also outputs the specific failure reason for debugging.
    /// </summary>
    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep, out string failReason)
    {
        failReason = null;
        BuildLayer layer = property.buildLayer;
        BuildSurfaceType required = property.requiredSurface;
        Vector3Int[] offsets = property.GetRotatedFootprint(rotationStep);

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3Int worldCell = anchor + offsets[i];
            if (!IsInBounds(worldCell))
            {
                failReason = $"Cell {worldCell} out of bounds (min={gridMin}, max={gridMax})";
                return false;
            }

            var key = new CellLayerKey(worldCell, layer);
            if (occupancyMap.ContainsKey(key))
            {
                failReason = $"Cell {key} already occupied by '{occupancyMap[key].InstanceId}'";
                return false;
            }

            if (required != BuildSurfaceType.None)
            {
                if (!HasSurfaceProvider(worldCell, required))
                {
                    failReason = $"Cell {worldCell} missing required surface '{required}'";
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Checks whether a cell has a placed buildable (on any layer below) whose
    /// <see cref="BuildableProperty.providedSurface"/> matches the required type.
    /// </summary>
    private bool HasSurfaceProvider(Vector3Int cell, BuildSurfaceType required)
    {
        // Check layers below the required one.
        // Platform surface is provided by World-layer buildables.
        // Room surface is provided by Platform-layer buildables.
        BuildLayer providerLayer;
        switch (required)
        {
            case BuildSurfaceType.Platform: providerLayer = BuildLayer.World; break;
            case BuildSurfaceType.Room:     providerLayer = BuildLayer.Platform; break;
            default: return false;
        }

        var providerKey = new CellLayerKey(cell, providerLayer);
        if (occupancyMap.TryGetValue(providerKey, out PlacedBuildableData provider))
        {
            return provider.Property.providedSurface == required;
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

    /// <summary>
    /// Get the topmost occupant at a cell (Room > Platform > World).
    /// Useful for click-to-select: player clicks a cell, we return the highest layer item.
    /// </summary>
    public PlacedBuildableData GetTopmostOccupant(Vector3Int cell)
    {
        // Check from highest layer to lowest
        for (int l = (int)BuildLayer.Room; l >= (int)BuildLayer.World; l--)
        {
            var key = new CellLayerKey(cell, (BuildLayer)l);
            if (occupancyMap.TryGetValue(key, out PlacedBuildableData data))
                return data;
        }
        return null;
    }

    /// <summary>
    /// Find the parent buildable for a given cell and required surface type.
    /// e.g. placing furniture (requiredSurface=Room) �� find the Room on Platform layer.
    /// Returns null if no matching parent found.
    /// </summary>
    public PlacedBuildableData FindParentAt(Vector3Int cell, BuildSurfaceType requiredSurface)
    {
        if (requiredSurface == BuildSurfaceType.None) return null;

        BuildLayer providerLayer;
        switch (requiredSurface)
        {
            case BuildSurfaceType.Platform: providerLayer = BuildLayer.World; break;
            case BuildSurfaceType.Room:     providerLayer = BuildLayer.Platform; break;
            default: return null;
        }

        var key = new CellLayerKey(cell, providerLayer);
        if (occupancyMap.TryGetValue(key, out PlacedBuildableData provider))
        {
            if (provider.Property.providedSurface == requiredSurface)
                return provider;
        }
        return null;
    }

    public bool IsInBounds(Vector3Int cell)
    {
        return cell.x >= gridMin.x && cell.x < gridMax.x
            && cell.y >= gridMin.y && cell.y < gridMax.y
            && cell.z >= gridMin.z && cell.z < gridMax.z;
    }

    // ������������������ Mutate ������������������

    /// <summary>
    /// Place a buildable. Returns false if blocked or surface requirement not met.
    /// Does NOT establish parent-child relationships �� caller should do that via
    /// <see cref="PlacedBuildableData.SetParent"/> after placement.
    /// </summary>
    public bool TryPlace(PlacedBuildableData data)
    {
        if (!CanPlace(data.Property, data.AnchorCell, data.RotationStep))
            return false;

        BuildLayer layer = data.Property.buildLayer;
        Vector3Int[] cells = data.GetEffectiveWorldCells();
        for (int i = 0; i < cells.Length; i++)
        {
            occupancyMap[new CellLayerKey(cells[i], layer)] = data;
        }

        allPlaced[data.InstanceId] = data;
        SyncInspectorStats();
        return true;
    }

    /// <summary>
    /// Remove a placed buildable, freeing all cells it occupied.
    /// Returns false if the buildable still has children (must remove children first).
    /// </summary>
    public bool TryRemove(string instanceId)
    {
        return TryRemove(instanceId, out _);
    }

    /// <summary>
    /// Remove a placed buildable, freeing all cells it occupied.
    /// Returns false if the buildable still has children (must remove children first).
    /// </summary>
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

        // Unlink from parent
        if (data.ParentId != null && allPlaced.TryGetValue(data.ParentId, out PlacedBuildableData parent))
        {
            parent.ChildrenIds.Remove(instanceId);
        }

        BuildLayer layer = data.Property.buildLayer;
        Vector3Int[] cells = data.GetEffectiveWorldCells();
        for (int i = 0; i < cells.Length; i++)
        {
            occupancyMap.Remove(new CellLayerKey(cells[i], layer));
        }

        allPlaced.Remove(instanceId);
        SyncInspectorStats();
        return true;
    }

    /// <summary>
    /// Force-remove a buildable from the grid without checking children.
    /// Used internally during batch move operations.
    /// Does NOT unlink from parent �� caller manages relationships.
    /// </summary>
    internal void ForceRemoveFromGrid(PlacedBuildableData data)
    {
        BuildLayer layer = data.Property.buildLayer;
        Vector3Int[] cells = data.GetEffectiveWorldCells();
        for (int i = 0; i < cells.Length; i++)
        {
            occupancyMap.Remove(new CellLayerKey(cells[i], layer));
        }
        allPlaced.Remove(data.InstanceId);
        SyncInspectorStats();
    }

    /// <summary>
    /// Force-place a buildable into the grid without validation.
    /// Used internally during batch move operations.
    /// </summary>
    internal void ForcePlaceIntoGrid(PlacedBuildableData data)
    {
        BuildLayer layer = data.Property.buildLayer;
        Vector3Int[] cells = data.GetEffectiveWorldCells();
        for (int i = 0; i < cells.Length; i++)
        {
            occupancyMap[new CellLayerKey(cells[i], layer)] = data;
        }
        allPlaced[data.InstanceId] = data;
        SyncInspectorStats();
    }

    /// <summary>
    /// Move a buildable (and all its children) to a new anchor.
    /// Children maintain their relative offset to the parent.
    /// Returns false (with rollback) if the new position is invalid.
    /// </summary>
    public bool TryMoveWithChildren(string instanceId, Vector3Int newAnchor, int newRotationStep, out string failReason)
    {
        failReason = null;

        if (!allPlaced.TryGetValue(instanceId, out PlacedBuildableData data))
        {
            failReason = $"Instance '{instanceId}' not found.";
            return false;
        }

        Vector3Int oldAnchor = data.AnchorCell;
        int oldRotation = data.RotationStep;
        Vector3Int delta = newAnchor - oldAnchor;

        // 1. Collect everything that needs to move: self + all children (recursive)
        List<PlacedBuildableData> movedSet = new List<PlacedBuildableData>();
        CollectSelfAndChildren(data, movedSet);

        // 2. Cache old anchors for rollback
        Vector3Int[] oldAnchors = new Vector3Int[movedSet.Count];
        int[] oldRotations = new int[movedSet.Count];
        for (int i = 0; i < movedSet.Count; i++)
        {
            oldAnchors[i] = movedSet[i].AnchorCell;
            oldRotations[i] = movedSet[i].RotationStep;
        }

        // 3. Remove all from grid
        for (int i = 0; i < movedSet.Count; i++)
            ForceRemoveFromGrid(movedSet[i]);

        // 4. Compute new anchors
        // Parent gets the explicit new anchor+rotation; children shift by delta
        data.AnchorCell = newAnchor;
        data.RotationStep = newRotationStep;
        for (int i = 1; i < movedSet.Count; i++) // skip index 0 (the parent itself)
        {
            movedSet[i].AnchorCell = oldAnchors[i] + delta;
            // children keep their own rotation (furniture doesn't rotate when room moves)
        }

        // 5. Validate: check parent placement (includes surface check)
        if (!CanPlace(data.Property, newAnchor, newRotationStep))
        {
            failReason = $"Parent cannot be placed at {newAnchor}.";
            Rollback(movedSet, oldAnchors, oldRotations);
            return false;
        }

        // Place parent first (so children's surface check can find it)
        ForcePlaceIntoGrid(data);

        // 6. Validate and place each child
        for (int i = 1; i < movedSet.Count; i++)
        {
            var child = movedSet[i];
            if (!CanPlace(child.Property, child.AnchorCell, child.RotationStep))
            {
                failReason = $"Child '{child.InstanceId}' cannot be placed at {child.AnchorCell}.";
                // Rollback everything already placed
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

    /// <summary>
    /// Recursively collect a buildable and all its descendants.
    /// </summary>
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

    // ������������������ Inspector Sync ������������������

    private void SyncInspectorStats()
    {
        placedCount = allPlaced.Count;
        occupiedCellCount = occupancyMap.Count;
    }

    // ������������������ Utility ������������������

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
/// Holds layer info and parent-child relationships for hierarchical building.
/// </summary>
public class PlacedBuildableData
{
    public string InstanceId;
    public BuildableProperty Property;
    public Vector3Int AnchorCell;
    public int RotationStep;                 // 0-3, Y-axis 90�� increments
    public GameObject SpawnedObject;

    // ������ Hierarchy ������
    public string ParentId;
    public List<string> ChildrenIds = new List<string>();

    /// <summary>
    /// Establish a parent-child link between this buildable and a parent.
    /// </summary>
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