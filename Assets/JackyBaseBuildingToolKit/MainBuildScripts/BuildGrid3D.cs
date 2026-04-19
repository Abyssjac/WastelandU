using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Composite key for the layered occupancy map: (cell position, build layer, facing).
/// Facing allows multiple directional buildables (e.g. walls) to coexist on the same cell and layer.
/// Non-directional buildables use <see cref="SurfaceFacing.None"/>.
/// </summary>
public struct CellLayerKey : System.IEquatable<CellLayerKey>
{
    public Vector3Int Cell;
    public BuildLayer Layer;
    public SurfaceFacing Facing;

    public CellLayerKey(Vector3Int cell, BuildLayer layer, SurfaceFacing facing = SurfaceFacing.None)
    {
        Cell = cell;
        Layer = layer;
        Facing = facing;
    }

    public bool Equals(CellLayerKey other)
    {
        return Cell.Equals(other.Cell) && Layer == other.Layer && Facing == other.Facing;
    }

    public override bool Equals(object obj)
    {
        return obj is CellLayerKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked { return (Cell.GetHashCode() * 397 ^ (int)Layer) * 397 ^ (int)Facing; }
    }

    public override string ToString() => Facing == SurfaceFacing.None
        ? $"({Cell}, {Layer})"
        : $"({Cell}, {Layer}, {Facing})";
}

/// <summary>
/// One entry in the surface map: what surface type is provided and by whom.
/// </summary>
public struct SurfaceEntry
{
    public BuildSurfaceType SurfaceType;
    public SurfaceFacing Facing;
    public string OwnerInstanceId;

    public SurfaceEntry(BuildSurfaceType surfaceType, SurfaceFacing facing, string ownerInstanceId)
    {
        SurfaceType = surfaceType;
        Facing = facing;
        OwnerInstanceId = ownerInstanceId;
    }
}

/// <summary>
/// Pure-data 3D grid that tracks cell occupancy and surface provision across <see cref="BuildLayer"/>s.
/// </summary>
[System.Serializable]
public class BuildGrid3D
{
    // (cell, layer, facing) -> occupant
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

            var key = new CellLayerKey(worldCell, occ[i].Layer, occ[i].OccupancyFacing);
            if (occupancyMap.ContainsKey(key)) return false;

            if (occ[i].RequiredSurface != BuildSurfaceType.None)
            {
                if (occ[i].RequiredFacing != SurfaceFacing.None)
                {
                    if (!HasSurface(worldCell, occ[i].RequiredSurface, occ[i].RequiredFacing))
                        return false;
                }
                else
                {
                    if (!HasSurface(worldCell, occ[i].RequiredSurface))
                        return false;
                }
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

            var key = new CellLayerKey(worldCell, occ[i].Layer, occ[i].OccupancyFacing);
            if (occupancyMap.ContainsKey(key))
            {
                failReason = $"Cell {key} already occupied by '{occupancyMap[key].InstanceId}'";
                return false;
            }

            if (occ[i].RequiredSurface != BuildSurfaceType.None)
            {
                if (occ[i].RequiredFacing != SurfaceFacing.None)
                {
                    if (!HasSurface(worldCell, occ[i].RequiredSurface, occ[i].RequiredFacing))
                    {
                        failReason = $"Cell {worldCell} missing surface '{occ[i].RequiredSurface}' facing '{occ[i].RequiredFacing}'";
                        return false;
                    }
                }
                else
                {
                    if (!HasSurface(worldCell, occ[i].RequiredSurface))
                    {
                        failReason = $"Cell {worldCell} missing required surface '{occ[i].RequiredSurface}'";
                        return false;
                    }
                }
            }
        }
        return true;
    }

    /// <summary>
    /// CanPlace overload that queries a <see cref="GridSandbox"/> merged view
    /// (real grid + sandbox overlay) instead of the real grid alone.
    /// Used during blueprint validation where earlier entries provide surfaces
    /// for later entries within the same blueprint.
    /// </summary>
    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep, GridSandbox sandbox)
    {
        return sandbox.CanPlace(property, anchor, rotationStep);
    }

    /// <summary>
    /// CanPlace overload with fail reason that queries a <see cref="GridSandbox"/> merged view.
    /// </summary>
    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep, GridSandbox sandbox, out string failReason)
    {
        return sandbox.CanPlace(property, anchor, rotationStep, out failReason);
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

    /// <summary>
    /// Checks whether a cell has a surface of the required type AND matching facing direction.
    /// </summary>
    public bool HasSurface(Vector3Int cell, BuildSurfaceType required, SurfaceFacing requiredFacing)
    {
        if (!surfaceMap.TryGetValue(cell, out List<SurfaceEntry> entries))
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].SurfaceType == required && entries[i].Facing == requiredFacing)
                return true;
        }
        return false;
    }

    public bool IsCellOccupied(Vector3Int cell, BuildLayer layer, SurfaceFacing facing = SurfaceFacing.None)
    {
        return occupancyMap.ContainsKey(new CellLayerKey(cell, layer, facing));
    }

    public PlacedBuildableData GetOccupant(Vector3Int cell, BuildLayer layer, SurfaceFacing facing = SurfaceFacing.None)
    {
        occupancyMap.TryGetValue(new CellLayerKey(cell, layer, facing), out PlacedBuildableData data);
        return data;
    }

    public PlacedBuildableData GetTopmostOccupant(Vector3Int cell)
    {
        for (int l = (int)BuildLayer.BL_Room; l >= (int)BuildLayer.BL_World; l--)
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
            occupancyMap[new CellLayerKey(worldCell, occ[i].Layer, occ[i].OccupancyFacing)] = data;
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
            list.Add(new SurfaceEntry(surf[i].ProvidedSurface, surf[i].Facing, data.InstanceId));
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
            occupancyMap.Remove(new CellLayerKey(worldCell, occ[i].Layer, occ[i].OccupancyFacing));
        }

        // Surface
        ResolvedSurfaceCell[] surf = data.Property.GetRotatedSurfaceCells(data.RotationStep);
        for (int i = 0; i < surf.Length; i++)
        {
            Vector3Int worldCell = data.AnchorCell + surf[i].Cell;
            if (surfaceMap.TryGetValue(worldCell, out List<SurfaceEntry> list))
            {
                list.RemoveAll(e => e.OwnerInstanceId == data.InstanceId
                    && e.SurfaceType == surf[i].ProvidedSurface
                    && e.Facing == surf[i].Facing);
                if (list.Count == 0)
                    surfaceMap.Remove(worldCell);
            }
        }

        allPlaced.Remove(data.InstanceId);
        SyncInspectorStats();
    }



    // --------- Stats ---------

    private void SyncInspectorStats()
    {
        placedCount = allPlaced.Count;
        occupiedCellCount = occupancyMap.Count;
        surfaceCellCount = surfaceMap.Count;
    }

    // --------- Conflict Query ---------

    /// <summary>
    /// Find all distinct placed buildables that would conflict (occupy the same cell/layer/facing)
    /// if the given property were placed at the specified anchor and rotation.
    /// Returns an empty list if there are no occupancy conflicts.
    /// Does NOT check surface requirements ¡ª only occupancy collisions.
    /// </summary>
    public List<PlacedBuildableData> FindConflictingOccupants(BuildableProperty property, Vector3Int anchor, int rotationStep)
    {
        HashSet<string> seen = new HashSet<string>();
        List<PlacedBuildableData> result = new List<PlacedBuildableData>();

        ResolvedOccupancyCell[] occ = property.GetRotatedOccupancyCells(rotationStep);
        for (int i = 0; i < occ.Length; i++)
        {
            Vector3Int worldCell = anchor + occ[i].Cell;
            var key = new CellLayerKey(worldCell, occ[i].Layer, occ[i].OccupancyFacing);

            if (occupancyMap.TryGetValue(key, out PlacedBuildableData occupant))
            {
                if (seen.Add(occupant.InstanceId))
                    result.Add(occupant);
            }
        }

        return result;
    }

    // --------- Removal Impact Query ---------

    /// <summary>
    /// Result of a removal impact query.
    /// </summary>
    public struct RemovalImpact
    {
        /// <summary>True if at least one other buildable would become illegal.</summary>
        public bool WouldAffectOthers;

        /// <summary>All buildables that would lose a required surface (cascaded).</summary>
        public List<PlacedBuildableData> AffectedBuildables;
    }

    /// <summary>
    /// Predicts whether removing the given buildable would cause other buildables
    /// to become illegal (lose a required surface). Checks cascading dependencies:
    /// if A provides surface for B, and B provides surface for C, removing A affects both B and C.
    /// Does NOT modify any state ¡ª pure read-only simulation.
    /// 
    /// Note: a cell may have multiple surface providers of the same type.
    /// A dependent is only affected if ALL providers of its required surface at that cell
    /// would be gone after the simulated removal.
    /// </summary>
    public RemovalImpact WouldRemoveAffectOthers(string instanceId)
    {
        var result = new RemovalImpact
        {
            WouldAffectOthers = false,
            AffectedBuildables = new List<PlacedBuildableData>()
        };

        if (!allPlaced.TryGetValue(instanceId, out PlacedBuildableData targetData))
            return result;

        // Set of instance IDs being simulated as removed (grows with cascade)
        HashSet<string> removedIds = new HashSet<string>();
        removedIds.Add(instanceId);

        // Queue of buildables whose surface contributions need to be checked
        Queue<PlacedBuildableData> toProcess = new Queue<PlacedBuildableData>();
        toProcess.Enqueue(targetData);

        while (toProcess.Count > 0)
        {
            PlacedBuildableData removed = toProcess.Dequeue();

            // 1. Collect all surface cells this buildable provides
            ResolvedSurfaceCell[] surf = removed.Property.GetRotatedSurfaceCells(removed.RotationStep);
            if (surf.Length == 0) continue;

            // For each surface cell, find dependents that might lose their required surface
            for (int s = 0; s < surf.Length; s++)
            {
                Vector3Int worldCell = removed.AnchorCell + surf[s].Cell;
                BuildSurfaceType providedType = surf[s].ProvidedSurface;
                SurfaceFacing providedFacing = surf[s].Facing;

                // Check if other providers still supply this surface type at this cell
                // (after all currently-removed IDs are excluded)
                bool stillProvided = false;
                if (surfaceMap.TryGetValue(worldCell, out List<SurfaceEntry> entries))
                {
                    for (int e = 0; e < entries.Count; e++)
                    {
                        if (removedIds.Contains(entries[e].OwnerInstanceId)) continue;
                        if (entries[e].SurfaceType == providedType)
                        {
                            // For faced surfaces, only count same facing as equivalent provider
                            if (providedFacing != SurfaceFacing.None)
                            {
                                if (entries[e].Facing == providedFacing)
                                {
                                    stillProvided = true;
                                    break;
                                }
                            }
                            else
                            {
                                stillProvided = true;
                                break;
                            }
                        }
                    }
                }

                if (stillProvided) continue;

                // Surface is gone at this cell ¡ª find all occupants that require it
                foreach (var kvp in occupancyMap)
                {
                    if (kvp.Key.Cell != worldCell) continue;
                    PlacedBuildableData occupant = kvp.Value;
                    if (removedIds.Contains(occupant.InstanceId)) continue;

                    // Check if this occupant actually requires the lost surface at this cell
                    ResolvedOccupancyCell[] occCells = occupant.Property.GetRotatedOccupancyCells(occupant.RotationStep);
                    for (int o = 0; o < occCells.Length; o++)
                    {
                        Vector3Int occWorldCell = occupant.AnchorCell + occCells[o].Cell;
                        if (occWorldCell != worldCell) continue;
                        if (occCells[o].RequiredSurface != providedType) continue;

                        // Facing match check
                        if (occCells[o].RequiredFacing != SurfaceFacing.None)
                        {
                            if (occCells[o].RequiredFacing != providedFacing) continue;
                        }

                        // This occupant depends on the lost surface ¡ú affected
                        if (!removedIds.Contains(occupant.InstanceId))
                        {
                            removedIds.Add(occupant.InstanceId);
                            result.AffectedBuildables.Add(occupant);
                            result.WouldAffectOthers = true;

                            // Cascade: this occupant might also provide surfaces to others
                            toProcess.Enqueue(occupant);
                        }
                        break;
                    }
                }
            }
        }

        return result;
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
