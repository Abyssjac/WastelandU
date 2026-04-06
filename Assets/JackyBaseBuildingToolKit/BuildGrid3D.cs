using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Pure-data 3D grid that tracks cell occupancy.
/// No MonoBehaviour — owned and driven by BuildManager.
/// Serializable so that inspector can display grid bounds and stats.
/// </summary>
[System.Serializable]
public class BuildGrid3D
{
    // cell → the PlacedBuildableData that occupies it (null = free)
    private Dictionary<Vector3Int, PlacedBuildableData> occupancyMap
        = new Dictionary<Vector3Int, PlacedBuildableData>();

    // instanceId → PlacedBuildableData (for fast lookup / iteration)
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

    /// <summary>
    /// Parameterless ctor required by Unity serialization.
    /// Call <see cref="Initialize"/> before use at runtime.
    /// </summary>
    public BuildGrid3D() { }

    public BuildGrid3D(Vector3Int gridMin, Vector3Int gridMax)
    {
        this.gridMin = gridMin;
        this.gridMax = gridMax;
    }

    /// <summary>
    /// Re-initialise runtime dictionaries (needed after Unity deserialization
    /// because Unity does not serialize Dictionary).
    /// </summary>
    public void Initialize()
    {
        occupancyMap = new Dictionary<Vector3Int, PlacedBuildableData>();
        allPlaced = new Dictionary<string, PlacedBuildableData>();
        SyncInspectorStats();
    }

    // ───────── Query ─────────

    /// <summary>
    /// Returns true if every cell the buildable would occupy is free and in-bounds.
    /// </summary>
    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep)
    {
        Vector3Int[] cells = property.GetRotatedFootprint(rotationStep);
        for (int i = 0; i < cells.Length; i++)
        {
            Vector3Int worldCell = anchor + cells[i];
            if (!IsInBounds(worldCell)) return false;
            if (occupancyMap.ContainsKey(worldCell)) return false;
        }
        return true;
    }

    /// <summary>
    /// Same as CanPlace but also outputs the specific failure reason for debugging.
    /// </summary>
    public bool CanPlace(BuildableProperty property, Vector3Int anchor, int rotationStep, out string failReason)
    {
        failReason = null;
        Vector3Int[] cells = property.GetRotatedFootprint(rotationStep);
        for (int i = 0; i < cells.Length; i++)
        {
            Vector3Int worldCell = anchor + cells[i];
            if (!IsInBounds(worldCell))
            {
                failReason = $"Cell {worldCell} out of bounds (min={gridMin}, max={gridMax})";
                return false;
            }
            if (occupancyMap.ContainsKey(worldCell))
            {
                failReason = $"Cell {worldCell} already occupied by '{occupancyMap[worldCell].InstanceId}'";
                return false;
            }
        }
        return true;
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

    public bool IsInBounds(Vector3Int cell)
    {
        return cell.x >= gridMin.x && cell.x < gridMax.x
            && cell.y >= gridMin.y && cell.y < gridMax.y
            && cell.z >= gridMin.z && cell.z < gridMax.z;
    }

    // ───────── Mutate ─────────

    /// <summary>
    /// Place a buildable. Returns false if blocked.
    /// </summary>
    public bool TryPlace(PlacedBuildableData data)
    {
        if (!CanPlace(data.Property, data.AnchorCell, data.RotationStep))
            return false;

        Vector3Int[] cells = data.GetEffectiveWorldCells();
        for (int i = 0; i < cells.Length; i++)
        {
            occupancyMap[cells[i]] = data;
        }

        allPlaced[data.InstanceId] = data;
        SyncInspectorStats();
        return true;
    }

    /// <summary>
    /// Remove a placed buildable, freeing all cells it occupied.
    /// </summary>
    public bool TryRemove(string instanceId)
    {
        if (!allPlaced.TryGetValue(instanceId, out PlacedBuildableData data))
            return false;

        Vector3Int[] cells = data.GetEffectiveWorldCells();
        for (int i = 0; i < cells.Length; i++)
        {
            occupancyMap.Remove(cells[i]);
        }

        allPlaced.Remove(instanceId);
        SyncInspectorStats();
        return true;
    }

    /// <summary>
    /// Convenience: remove, update anchor/rotation, re-place.
    /// Used for drag-move operations.
    /// </summary>
    public bool TryMove(string instanceId, Vector3Int newAnchor, int newRotationStep)
    {
        if (!allPlaced.TryGetValue(instanceId, out PlacedBuildableData data))
            return false;

        // cache old state for rollback
        Vector3Int oldAnchor = data.AnchorCell;
        int oldRotation = data.RotationStep;

        // temporarily remove
        TryRemove(instanceId);

        // check if new position is valid
        if (!CanPlace(data.Property, newAnchor, newRotationStep))
        {
            // rollback — re-place at old position
            data.AnchorCell = oldAnchor;
            data.RotationStep = oldRotation;
            TryPlace(data);
            return false;
        }

        data.AnchorCell = newAnchor;
        data.RotationStep = newRotationStep;
        TryPlace(data);
        return true;
    }

    // ───────── Inspector Sync ─────────

    private void SyncInspectorStats()
    {
        placedCount = allPlaced.Count;
        occupiedCellCount = occupancyMap.Count;
    }

    // ───────── Utility ─────────

    /// <summary>
    /// Returns the absolute world-cell positions for a given anchor + footprint offsets.
    /// Useful for preview highlighting.
    /// </summary>
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
    public string InstanceId;                // unique per placement
    public BuildableProperty Property;
    public Vector3Int AnchorCell;            // 锚点所在格子（放置参考点）
    public int RotationStep;                 // 0-3, Y-axis 90° increments
    public GameObject SpawnedObject;         // scene中的实际 GameObject

    /// <summary>
    /// The footprint cell offsets after rotation (relative to anchor).
    /// </summary>
    public Vector3Int[] GetEffectiveFootprint()
    {
        return Property.GetRotatedFootprint(RotationStep);
    }

    /// <summary>
    /// The absolute world-cell positions this instance occupies.
    /// </summary>
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