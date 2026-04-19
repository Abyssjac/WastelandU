using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure-data 3D grid for enemy combat.
/// Simplified version of <see cref="BuildGrid3D"/> ¡ª no layers, surfaces, or facings.
/// Each cell is either empty or occupied by a <see cref="PlacedBuildableData"/>.
/// </summary>
[System.Serializable]
public class EnemyGrid3D
{
    // cell -> occupant
    private Dictionary<Vector3Int, PlacedBuildableData> occupancyMap
        = new Dictionary<Vector3Int, PlacedBuildableData>();

    // instanceId -> placement data
    private Dictionary<string, PlacedBuildableData> allPlaced
        = new Dictionary<string, PlacedBuildableData>();

    [SerializeField] private Vector3Int gridMin;
    [SerializeField] private Vector3Int gridMax;
    [SerializeField] private int totalCellCount;
    [SerializeField] private int occupiedCellCount;

    public Vector3Int GridMin => gridMin;
    public Vector3Int GridMax => gridMax;

    /// <summary>Total number of cells inside the bounds.</summary>
    public int TotalCellCount => totalCellCount;

    /// <summary>Number of currently occupied cells.</summary>
    public int OccupiedCellCount => occupiedCellCount;

    public IReadOnlyDictionary<Vector3Int, PlacedBuildableData> OccupancyMap => occupancyMap;
    public IReadOnlyDictionary<string, PlacedBuildableData> AllPlaced => allPlaced;

    // --------- Construction ---------

    public EnemyGrid3D() { }

    public EnemyGrid3D(Vector3Int gridMin, Vector3Int gridMax)
    {
        this.gridMin = gridMin;
        this.gridMax = gridMax;
    }

    public void Initialize()
    {
        occupancyMap = new Dictionary<Vector3Int, PlacedBuildableData>();
        allPlaced = new Dictionary<string, PlacedBuildableData>();
        totalCellCount = (gridMax.x - gridMin.x)
                       * (gridMax.y - gridMin.y)
                       * (gridMax.z - gridMin.z);
        occupiedCellCount = 0;
    }

    // --------- Query ---------

    public bool IsInBounds(Vector3Int cell)
    {
        return cell.x >= gridMin.x && cell.x < gridMax.x
            && cell.y >= gridMin.y && cell.y < gridMax.y
            && cell.z >= gridMin.z && cell.z < gridMax.z;
    }

    public bool IsCellOccupied(Vector3Int cell)
    {
        return occupancyMap.ContainsKey(cell);
    }

    public PlacedBuildableData GetOccupant(Vector3Int cell)
    {
        occupancyMap.TryGetValue(cell, out PlacedBuildableData data);
        return data;
    }

    public PlacedBuildableData GetPlacedById(string instanceId)
    {
        allPlaced.TryGetValue(instanceId, out PlacedBuildableData data);
        return data;
    }

    /// <summary>
    /// Check whether the given cells can all be placed (in bounds and unoccupied).
    /// </summary>
    public bool CanPlace(Vector3Int[] worldCells)
    {
        for (int i = 0; i < worldCells.Length; i++)
        {
            if (!IsInBounds(worldCells[i])) return false;
            if (occupancyMap.ContainsKey(worldCells[i])) return false;
        }
        return true;
    }

    /// <summary>
    /// Check whether a buildable property can be placed at the given anchor with the given rotation.
    /// Only checks bounds and occupancy ¡ª no layer/surface logic.
    /// </summary>
    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep)
    {
        Vector3Int[] offsets = property.GetRotatedFootprint(rotationStep);
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3Int cell = anchor + offsets[i];
            if (!IsInBounds(cell)) return false;
            if (occupancyMap.ContainsKey(cell)) return false;
        }
        return true;
    }

    /// <summary>
    /// Overload with fail reason for debugging.
    /// </summary>
    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep, out string failReason)
    {
        failReason = null;
        Vector3Int[] offsets = property.GetRotatedFootprint(rotationStep);
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3Int cell = anchor + offsets[i];
            if (!IsInBounds(cell))
            {
                failReason = $"Cell {cell} out of bounds (min={gridMin}, max={gridMax})";
                return false;
            }
            if (occupancyMap.ContainsKey(cell))
            {
                failReason = $"Cell {cell} already occupied by '{occupancyMap[cell].InstanceId}'";
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Returns per-cell placement status for preview coloring.
    /// 0 = valid (in bounds, empty), 1 = conflict (in bounds, occupied), 2 = invalid (out of bounds).
    /// </summary>
    public void EvaluatePlacement(BuildableProperty property, Vector3Int anchor, int rotationStep,
                                   out Vector3Int[] worldCells, out int[] cellStatus)
    {
        Vector3Int[] offsets = property.GetRotatedFootprint(rotationStep);
        worldCells = new Vector3Int[offsets.Length];
        cellStatus = new int[offsets.Length];

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3Int cell = anchor + offsets[i];
            worldCells[i] = cell;

            if (!IsInBounds(cell))
                cellStatus[i] = 2; // invalid
            else if (occupancyMap.ContainsKey(cell))
                cellStatus[i] = 1; // conflict
            else
                cellStatus[i] = 0; // valid
        }
    }

    /// <summary>
    /// Are all cells within the grid bounds occupied?
    /// </summary>
    public bool AreAllCellsFilled()
    {
        return occupiedCellCount >= totalCellCount && totalCellCount > 0;
    }

    // --------- Mutate ---------

    /// <summary>
    /// Place a buildable into the grid. Returns false if placement is blocked.
    /// </summary>
    public bool TryPlace(PlacedBuildableData data)
    {
        if (!CanPlace(data.Property, data.AnchorCell, data.RotationStep))
            return false;

        WritePlacement(data);
        return true;
    }

    /// <summary>
    /// Remove a placed buildable by instance id. Returns false if not found.
    /// </summary>
    public bool TryRemove(string instanceId)
    {
        if (!allPlaced.TryGetValue(instanceId, out PlacedBuildableData data))
            return false;

        Vector3Int[] worldCells = data.GetEffectiveWorldCells();
        for (int i = 0; i < worldCells.Length; i++)
        {
            if (occupancyMap.Remove(worldCells[i]))
                occupiedCellCount--;
        }

        allPlaced.Remove(instanceId);
        return true;
    }

    // --------- Internal ---------

    private void WritePlacement(PlacedBuildableData data)
    {
        Vector3Int[] offsets = data.Property.GetRotatedFootprint(data.RotationStep);
        for (int i = 0; i < offsets.Length; i++)
        {
            Vector3Int cell = data.AnchorCell + offsets[i];
            occupancyMap[cell] = data;
            occupiedCellCount++;
        }
        allPlaced[data.InstanceId] = data;
    }
}
