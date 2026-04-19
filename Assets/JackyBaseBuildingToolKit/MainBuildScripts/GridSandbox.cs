using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lightweight overlay on top of a real <see cref="BuildGrid3D"/> that accumulates
/// tentative placements without mutating the real grid.
/// Used for blueprint validation: each entry writes into the sandbox so that
/// subsequent entries can see surfaces provided by earlier ones.
/// On success, call <see cref="Flush"/> to commit everything to the real grid.
/// On failure, simply discard the sandbox ¡ª the real grid is untouched.
/// </summary>
public class GridSandbox
{
    private readonly BuildGrid3D baseGrid;

    // Tentative additions (overlay on top of baseGrid)
    private readonly Dictionary<CellLayerKey, string> addedOccupancy
        = new Dictionary<CellLayerKey, string>();

    private readonly Dictionary<Vector3Int, List<SurfaceEntry>> addedSurface
        = new Dictionary<Vector3Int, List<SurfaceEntry>>();

    // Staged PlacedBuildableData, in order, ready to flush
    private readonly List<PlacedBuildableData> stagedPlacements = new List<PlacedBuildableData>();

    public IReadOnlyList<PlacedBuildableData> StagedPlacements => stagedPlacements;

    public GridSandbox(BuildGrid3D baseGrid)
    {
        this.baseGrid = baseGrid;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Query (merged view: baseGrid + sandbox overlay) ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Check whether a cell-layer-facing key is occupied in either the real grid or the sandbox.
    /// </summary>
    public bool IsOccupied(CellLayerKey key)
    {
        if (addedOccupancy.ContainsKey(key)) return true;
        return baseGrid.OccupancyMap.ContainsKey(key);
    }

    /// <summary>
    /// Check whether a cell has a surface of the required type (real grid + sandbox).
    /// </summary>
    public bool HasSurface(Vector3Int cell, BuildSurfaceType required)
    {
        // Check sandbox first
        if (addedSurface.TryGetValue(cell, out List<SurfaceEntry> sandboxEntries))
        {
            for (int i = 0; i < sandboxEntries.Count; i++)
            {
                if (sandboxEntries[i].SurfaceType == required)
                    return true;
            }
        }

        // Fall through to real grid
        return baseGrid.HasSurface(cell, required);
    }

    /// <summary>
    /// Check whether a cell has a surface of the required type AND facing (real grid + sandbox).
    /// </summary>
    public bool HasSurface(Vector3Int cell, BuildSurfaceType required, SurfaceFacing requiredFacing)
    {
        if (addedSurface.TryGetValue(cell, out List<SurfaceEntry> sandboxEntries))
        {
            for (int i = 0; i < sandboxEntries.Count; i++)
            {
                if (sandboxEntries[i].SurfaceType == required && sandboxEntries[i].Facing == requiredFacing)
                    return true;
            }
        }

        return baseGrid.HasSurface(cell, required, requiredFacing);
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Validation ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Check whether a buildable can be placed at the given anchor,
    /// considering both the real grid and all previously staged sandbox entries.
    /// </summary>
    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep)
    {
        return CanPlace(property, anchor, rotationStep, out _);
    }

    /// <summary>
    /// Check with fail reason.
    /// </summary>
    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep, out string failReason)
    {
        failReason = null;
        ResolvedOccupancyCell[] occ = property.GetRotatedOccupancyCells(rotationStep);

        for (int i = 0; i < occ.Length; i++)
        {
            Vector3Int worldCell = anchor + occ[i].Cell;
            if (!baseGrid.IsInBounds(worldCell))
            {
                failReason = $"Cell {worldCell} out of bounds (grid: {baseGrid.GridMin}~{baseGrid.GridMax})";
                return false;
            }

            var key = new CellLayerKey(worldCell, occ[i].Layer, occ[i].OccupancyFacing);
            if (addedOccupancy.ContainsKey(key))
            {
                failReason = $"Cell {key} occupied by earlier blueprint entry '{addedOccupancy[key]}'";
                return false;
            }
            if (baseGrid.OccupancyMap.ContainsKey(key))
            {
                failReason = $"Cell {key} occupied by existing buildable '{baseGrid.OccupancyMap[key].InstanceId}'";
                return false;
            }

            if (occ[i].RequiredSurface != BuildSurfaceType.None)
            {
                if (occ[i].RequiredFacing != SurfaceFacing.None)
                {
                    if (!HasSurface(worldCell, occ[i].RequiredSurface, occ[i].RequiredFacing))
                    {
                        failReason = $"Cell {worldCell} missing surface '{occ[i].RequiredSurface}' facing '{occ[i].RequiredFacing}' (not in grid or earlier entries)";
                        return false;
                    }
                }
                else
                {
                    if (!HasSurface(worldCell, occ[i].RequiredSurface))
                    {
                        failReason = $"Cell {worldCell} missing required surface '{occ[i].RequiredSurface}' (not in grid or earlier entries)";
                        return false;
                    }
                }
            }
        }
        return true;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Staging (write into sandbox, not real grid) ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Validate and stage a placement into the sandbox.
    /// Writes occupancy and surface data into the sandbox overlay so subsequent
    /// entries can see this entry's contributions.
    /// Returns false if the entry cannot be placed.
    /// </summary>
    public bool TryStage(PlacedBuildableData data, out string failReason)
    {
        if (!CanPlace(data.Property, data.AnchorCell, data.RotationStep, out failReason))
            return false;

        WriteToSandbox(data);
        stagedPlacements.Add(data);
        return true;
    }

    private void WriteToSandbox(PlacedBuildableData data)
    {
        // Occupancy
        ResolvedOccupancyCell[] occ = data.Property.GetRotatedOccupancyCells(data.RotationStep);
        for (int i = 0; i < occ.Length; i++)
        {
            Vector3Int worldCell = data.AnchorCell + occ[i].Cell;
            var key = new CellLayerKey(worldCell, occ[i].Layer, occ[i].OccupancyFacing);
            addedOccupancy[key] = data.InstanceId;
        }

        // Surface
        ResolvedSurfaceCell[] surf = data.Property.GetRotatedSurfaceCells(data.RotationStep);
        for (int i = 0; i < surf.Length; i++)
        {
            Vector3Int worldCell = data.AnchorCell + surf[i].Cell;
            if (!addedSurface.TryGetValue(worldCell, out List<SurfaceEntry> list))
            {
                list = new List<SurfaceEntry>(2);
                addedSurface[worldCell] = list;
            }
            list.Add(new SurfaceEntry(surf[i].ProvidedSurface, surf[i].Facing, data.InstanceId));
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Flush (commit all staged data to real grid) ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Commit all staged placements to the real grid.
    /// Call this only after all entries have been successfully staged.
    /// After flush, this sandbox should be discarded.
    /// </summary>
    public void Flush()
    {
        for (int i = 0; i < stagedPlacements.Count; i++)
        {
            baseGrid.ForcePlaceIntoGrid(stagedPlacements[i]);
        }
    }
}
