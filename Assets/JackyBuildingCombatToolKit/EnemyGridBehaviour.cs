using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a local 3D grid on an enemy. Handles coordinate conversion between
/// world space and the enemy's local grid space so that the grid automatically
/// follows the enemy's movement and rotation.
/// </summary>
public class EnemyGridBehaviour : MonoBehaviour
{
    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Inspector ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    [Header("Grid Bounds (local cells)")]
    [Tooltip("Define the grid region using a footprint box (two diagonal corners).")]
    [SerializeField] private FootprintBox boundsBox = new FootprintBox(Vector3Int.zero, new Vector3Int(2, 2, 2));

    [Header("Grid Settings")]
    [Tooltip("Size of a single cell in world units.")]
    [SerializeField] private Vector3 cellSize = Vector3.one;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Runtime ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    [SerializeField] private EnemyGrid3D grid;

    private int instanceCounter;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public EnemyGrid3D Grid => grid;
    public Vector3 CellSize => cellSize;
    public FootprintBox BoundsBox => boundsBox;

    /// <summary>Fired after any grid mutation (place / remove).</summary>
    public event Action OnGridChanged;

    /// <summary>Fired when all cells inside the grid bounds become occupied.</summary>
    public event Action OnGridFulfilled;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Awake()
    {
        InitializeGrid();
    }

    public void InitializeGrid()
    {
        Vector3Int min = new Vector3Int(
            Mathf.Min(boundsBox.cornerA.x, boundsBox.cornerB.x),
            Mathf.Min(boundsBox.cornerA.y, boundsBox.cornerB.y),
            Mathf.Min(boundsBox.cornerA.z, boundsBox.cornerB.z));

        Vector3Int max = new Vector3Int(
            Mathf.Max(boundsBox.cornerA.x, boundsBox.cornerB.x) + 1,
            Mathf.Max(boundsBox.cornerA.y, boundsBox.cornerB.y) + 1,
            Mathf.Max(boundsBox.cornerA.z, boundsBox.cornerB.z) + 1);

        grid = new EnemyGrid3D(min, max);
        grid.Initialize();
        instanceCounter = 0;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Coordinate Conversion ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Convert a world position to the local grid cell coordinate.
    /// Accounts for the enemy's position and rotation.
    /// </summary>
    public Vector3Int WorldToLocalCell(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition);
        return new Vector3Int(
            Mathf.FloorToInt(local.x / cellSize.x),
            Mathf.FloorToInt(local.y / cellSize.y),
            Mathf.FloorToInt(local.z / cellSize.z));
    }

    /// <summary>
    /// Convert a local cell coordinate to the world-space position (cell corner).
    /// </summary>
    public Vector3 LocalCellToWorld(Vector3Int cell)
    {
        Vector3 local = new Vector3(
            cell.x * cellSize.x,
            cell.y * cellSize.y,
            cell.z * cellSize.z);
        return transform.TransformPoint(local);
    }

    /// <summary>
    /// Convert a local cell coordinate to the world-space position (cell center).
    /// </summary>
    public Vector3 LocalCellToWorldCenter(Vector3Int cell)
    {
        Vector3 local = new Vector3(
            cell.x * cellSize.x + cellSize.x * 0.5f,
            cell.y * cellSize.y + cellSize.y * 0.5f,
            cell.z * cellSize.z + cellSize.z * 0.5f);
        return transform.TransformPoint(local);
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Grid Operations ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Try to place a buildable at the given world position.
    /// The world position is converted to a local cell, then placement is checked against the grid.
    /// On success the prefab is instantiated as a child of this enemy.
    /// </summary>
    /// <param name="property">The buildable to place.</param>
    /// <param name="worldPosition">Hit world position (will be snapped to local cell).</param>
    /// <param name="rotationStep">Rotation step (0-3, 90¡ã increments around Y).</param>
    /// <param name="placed">The spawned GameObject, or null on failure.</param>
    /// <returns>True if placement succeeded.</returns>
    public bool TryPlaceAtWorld(BuildableProperty property, Vector3 worldPosition, int rotationStep, out GameObject placed)
    {
        placed = null;
        Vector3Int localCell = WorldToLocalCell(worldPosition);
        return TryPlace(property, localCell, rotationStep, out placed);
    }

    /// <summary>
    /// Try to place a buildable at the given local cell anchor.
    /// </summary>
    public bool TryPlace(BuildableProperty property, Vector3Int localAnchor, int rotationStep, out GameObject placed)
    {
        placed = null;

        if (!grid.CanPlace(property, localAnchor, rotationStep))
            return false;

        string instanceId = $"enemy_{gameObject.GetInstanceID()}_{instanceCounter++}";

        PlacedBuildableData data = new PlacedBuildableData
        {
            InstanceId = instanceId,
            Property = property,
            AnchorCell = localAnchor,
            RotationStep = rotationStep,
        };

        if (!grid.TryPlace(data))
            return false;

        // Spawn as child so it follows enemy transform automatically
        Vector3 worldPos = LocalCellToWorldCenter(localAnchor);
        float yaw = property.GetRotationDegrees(rotationStep);
        Quaternion worldRot = transform.rotation * Quaternion.Euler(0f, yaw, 0f);

        GameObject go = Instantiate(property.prefab, worldPos, worldRot, transform);
        go.transform.localScale = cellSize;
        data.SpawnedObject = go;

        var behaviour = go.GetComponent<BuildableBehaviour>();
        if (behaviour == null)
            behaviour = go.AddComponent<BuildableBehaviour>();
        behaviour.Initialize(data);

        placed = go;

        OnGridChanged?.Invoke();

        if (grid.AreAllCellsFilled())
            OnGridFulfilled?.Invoke();

        return true;
    }

    /// <summary>
    /// Remove a placed buildable by instance id. Destroys the spawned GameObject.
    /// </summary>
    public bool TryRemove(string instanceId)
    {
        PlacedBuildableData data = grid.GetPlacedById(instanceId);
        if (data == null) return false;

        if (data.SpawnedObject != null)
            Destroy(data.SpawnedObject);

        if (!grid.TryRemove(instanceId))
            return false;

        OnGridChanged?.Invoke();

        if (grid.AreAllCellsFilled())
            OnGridFulfilled?.Invoke();

        return true;
    }

    /// <summary>
    /// Remove all placed buildables, destroying their GameObjects.
    /// </summary>
    public void ClearAll()
    {
        // Collect ids first to avoid modifying dictionary during iteration
        List<string> ids = new List<string>(grid.AllPlaced.Keys);
        for (int i = 0; i < ids.Count; i++)
        {
            PlacedBuildableData data = grid.GetPlacedById(ids[i]);
            if (data != null && data.SpawnedObject != null)
                Destroy(data.SpawnedObject);
        }

        grid.Initialize();
        OnGridChanged?.Invoke();
    }

    /// <summary>
    /// Query whether a buildable can be placed at a world position (snapped to local cell).
    /// </summary>
    public bool CanPlaceAtWorld(BuildableProperty property, Vector3 worldPosition, int rotationStep)
    {
        Vector3Int localCell = WorldToLocalCell(worldPosition);
        return grid.CanPlace(property, localCell, rotationStep);
    }

    /// <summary>
    /// Query whether a buildable can be placed at a local cell anchor.
    /// </summary>
    public bool CanPlace(BuildableProperty property, Vector3Int localAnchor, int rotationStep)
    {
        return grid.CanPlace(property, localAnchor, rotationStep);
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Debug ©¤©¤©¤©¤©¤©¤©¤©¤©¤

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!enableDebug) return;

        // Draw grid bounds wireframe in world space
        Vector3Int min = new Vector3Int(
            Mathf.Min(boundsBox.cornerA.x, boundsBox.cornerB.x),
            Mathf.Min(boundsBox.cornerA.y, boundsBox.cornerB.y),
            Mathf.Min(boundsBox.cornerA.z, boundsBox.cornerB.z));
        Vector3Int max = new Vector3Int(
            Mathf.Max(boundsBox.cornerA.x, boundsBox.cornerB.x) + 1,
            Mathf.Max(boundsBox.cornerA.y, boundsBox.cornerB.y) + 1,
            Mathf.Max(boundsBox.cornerA.z, boundsBox.cornerB.z) + 1);

        // Draw individual cell wireframes
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
                for (int x = min.x; x < max.x; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, z);
                    Vector3 center = LocalCellToWorldCenter(cell);
                    Vector3 size = transform.TransformVector(cellSize);
                    // Use absolute values since TransformVector may flip axes
                    size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                    Gizmos.DrawWireCube(center, size * 0.95f);
                }

        // Draw overall bounds
        Vector3 boundsMin = LocalCellToWorld(min);
        Vector3 boundsMax = LocalCellToWorld(max);
        Vector3 boundsCenter = (boundsMin + boundsMax) * 0.5f;
        Vector3 boundsSize = boundsMax - boundsMin;
        boundsSize = new Vector3(Mathf.Abs(boundsSize.x), Mathf.Abs(boundsSize.y), Mathf.Abs(boundsSize.z));

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(boundsCenter, boundsSize);

        // Label
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(boundsCenter + Vector3.up * (boundsSize.y * 0.5f + 0.3f),
            $"EnemyGrid [{(max.x - min.x)}x{(max.y - min.y)}x{(max.z - min.z)}]\n" +
            $"Filled: {(grid != null ? grid.OccupiedCellCount : 0)}/{(grid != null ? grid.TotalCellCount : 0)}");
    }
#endif
}
