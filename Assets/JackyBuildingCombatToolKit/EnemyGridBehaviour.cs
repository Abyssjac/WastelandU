using System;
using System.Collections.Generic;
using UnityEngine;
using JackyUtility;

/// <summary>
/// Manages a local 3D grid on an enemy. Handles coordinate conversion between
/// world space and the enemy's local grid space so that the grid automatically
/// follows the enemy's movement and rotation.
/// <para>
/// Coordinate conversion uses <see cref="Transform.InverseTransformPoint"/> and
/// <see cref="Transform.TransformPoint"/>, which inherently account for the enemy's
/// position, rotation, and scale. No separate rotation parameter is needed.
/// </para>
/// </summary>
public class EnemyGridBehaviour : MonoBehaviour
{
    // ───────── Inspector ─────────

    [Header("Grid Shape (local cells — union of boxes + individual cells)")]
    [Tooltip("Rectangular box regions that define part of the grid shape. All boxes are merged additively.")]
    [SerializeField] private FootprintBox[] boundsBoxes = new FootprintBox[]
    {
        new FootprintBox(Vector3Int.zero, new Vector3Int(2, 2, 2))
    };

    [Tooltip("Additional individual cells to include in the grid shape (merged with boxes).")]
    [SerializeField] private Vector3Int[] boundsCells = new Vector3Int[0];

    [Header("Grid Settings")]
    [Tooltip("Local-space offset of the grid origin relative to the enemy's pivot.\n" +
             "Use this to align the grid with the enemy's visual mesh.")]
    [SerializeField] private Vector3 gridOriginLocal = Vector3.zero;

    [Tooltip("Size of a single cell in world units.")]
    [SerializeField] private Vector3 cellSize = Vector3.one;

    [Header("Preset")]
    [Tooltip("Optional preset to auto-place buildables into this enemy grid at initialization.")]
    [SerializeField] private BuildPreset startPreset;

    [Header("Forced Occupancy")]
    [Tooltip("Cells in this region are marked as occupied at initialization without spawning any object.\n" +
             "Use this to reserve slots for externally managed objects (e.g. UnstableObjBehaviour).")]
    [SerializeField] private BoxVisualizeRegion forcedOccupiedRegion;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;
    [SerializeField] private bool debugPermanent = false;

    // ───────── Runtime ─────────

    [SerializeField] private EnemyGrid3D grid;

    // Cached flat list of all valid cells (built from boundsBoxes + boundsCells)
    private List<Vector3Int> cachedAllCells = new List<Vector3Int>();

    private int instanceCounter;

    // ───────── Public API ─────────

    public EnemyGrid3D Grid => grid;
    public Vector3 CellSize => cellSize;
    public Vector3 GridOriginLocal => gridOriginLocal;
    public FootprintBox[] BoundsBoxes => boundsBoxes;
    public Vector3Int[] BoundsCells => boundsCells;

    /// <summary>All valid cells in the grid shape (read-only snapshot from last initialization).</summary>
    public IReadOnlyList<Vector3Int> AllValidCells => cachedAllCells;

    /// <summary>Fired after any grid mutation (place / remove).</summary>
    public event Action OnGridChanged;

    /// <summary>
    /// Fired when grid fulfilled state changes.
    /// true = fulfilled, false = unfulfilled.
    /// </summary>
    public event Action<bool> OnGridStateChanged;

    /// <summary>Fired when all cells inside the grid bounds become occupied.</summary>
    public event Action OnGridFulfilled;

    public bool IsGridFulfilled => grid != null && grid.AreAllCellsFilled();

    private bool? lastGridFulfilledState;

    // ───────── Lifecycle ─────────

    private void Awake()
    {
        InitializeGrid();
        LoadPreset();
    }

    public void InitializeGrid()
    {
        // Build the union of all boxes + individual cells (deduplicated via HashSet)
        HashSet<Vector3Int> cellSet = new HashSet<Vector3Int>();

        if (boundsBoxes != null)
        {
            List<Vector3Int> tmp = new List<Vector3Int>();
            for (int b = 0; b < boundsBoxes.Length; b++)
            {
                tmp.Clear();
                boundsBoxes[b].GenerateCells(tmp);
                for (int i = 0; i < tmp.Count; i++)
                    cellSet.Add(tmp[i]);
            }
        }

        if (boundsCells != null)
        {
            for (int i = 0; i < boundsCells.Length; i++)
                cellSet.Add(boundsCells[i]);
        }

        cachedAllCells = new List<Vector3Int>(cellSet);

        grid = new EnemyGrid3D();
        grid.Initialize(cellSet);
        instanceCounter = 0;
        lastGridFulfilledState = null;

        ApplyForcedOccupancy();
    }

    private void ApplyForcedOccupancy()
    {
        Vector3Int[] forcedCells = forcedOccupiedRegion.GatherAllCells();
        if (forcedCells == null || forcedCells.Length == 0) return;

        grid.ForcedOccupyCells(forcedCells);

        if (enableDebug)
            Debug.Log($"[EnemyGridBehaviour] '{name}': forced-occupied {forcedCells.Length} cell(s).", this);
    }

    // ───────── Coordinate Conversion ─────────
    // These methods use Transform.InverseTransformPoint / TransformPoint which
    // inherently account for the enemy's position, rotation, AND scale.
    // When the enemy moves or rotates, world↔local conversion automatically adapts.

    /// <summary>
    /// Convert a world position to the local grid cell coordinate.
    /// Accounts for the enemy's position and rotation via <see cref="Transform.InverseTransformPoint"/>.
    /// </summary>
    public Vector3Int WorldToLocalCell(Vector3 worldPosition)
    {
        Vector3 local = transform.InverseTransformPoint(worldPosition) - gridOriginLocal;
        return new Vector3Int(
            Mathf.FloorToInt(local.x / cellSize.x),
            Mathf.FloorToInt(local.y / cellSize.y),
            Mathf.FloorToInt(local.z / cellSize.z));
    }

    /// <summary>
    /// Convert a local cell coordinate to the world-space position (cell corner).
    /// Accounts for the enemy's position and rotation via <see cref="Transform.TransformPoint"/>.
    /// </summary>
    public Vector3 LocalCellToWorld(Vector3Int cell)
    {
        Vector3 local = gridOriginLocal + new Vector3(
            cell.x * cellSize.x,
            cell.y * cellSize.y,
            cell.z * cellSize.z);
        return transform.TransformPoint(local);
    }

    /// <summary>
    /// Convert a local cell coordinate to the world-space position (cell center).
    /// Accounts for the enemy's position and rotation via <see cref="Transform.TransformPoint"/>.
    /// </summary>
    public Vector3 LocalCellToWorldCenter(Vector3Int cell)
    {
        Vector3 local = gridOriginLocal + new Vector3(
            cell.x * cellSize.x + cellSize.x * 0.5f,
            cell.y * cellSize.y + cellSize.y * 0.5f,
            cell.z * cellSize.z + cellSize.z * 0.5f);
        return transform.TransformPoint(local);
    }

    // ───────── Grid Operations ─────────

    /// <summary>
    /// Try to place a buildable at the given world position.
    /// The world position is converted to a local cell, then placement is checked against the grid.
    /// On success the prefab is instantiated as a child of this enemy.
    /// </summary>
    /// <param name="property">The buildable to place.</param>
    /// <param name="worldPosition">Hit world position (will be snapped to local cell).</param>
    /// <param name="rotationStep">Rotation step (0-3, 90° increments around Y).</param>
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

        SpawnPlacedObject(data);
        placed = data.SpawnedObject;

        OnGridChanged?.Invoke();
        EvaluateAndNotifyGridState();

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
        EvaluateAndNotifyGridState();

        return true;
    }

    /// <summary>
    /// Detach a placed buildable from the grid without destroying its GameObject.
    /// Frees the grid slots, removes all tracking data, un-parents the GO to the scene root,
    /// and marks its BuildableBehaviour as detached so the weapon system can handle it correctly.
    /// Returns the detached GameObject, or null if the instanceId was not found.
    /// </summary>
    public GameObject DetachFromGrid(string instanceId)
    {
        PlacedBuildableData data = grid.GetPlacedById(instanceId);
        if (data == null) return null;

        GameObject go = data.SpawnedObject;

        if (!grid.TryRemove(instanceId))
            return null;

        if (go != null)
        {
            go.transform.SetParent(null, worldPositionStays: true);

            var bb = go.GetComponent<BuildableBehaviour>();
            if (bb != null) bb.MarkDetached();
        }

        OnGridChanged?.Invoke();
        EvaluateAndNotifyGridState();

        return go;
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

        grid.Initialize(cachedAllCells);
        OnGridChanged?.Invoke();
        EvaluateAndNotifyGridState();
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

    // ───────── Preset ─────────

    /// <summary>
    /// Load the start preset, placing all entries into the grid.
    /// Blueprint entries are ignored — only individual buildable entries are supported.
    /// </summary>
    private void LoadPreset()
    {
        if (startPreset == null || startPreset.groups == null || startPreset.groups.Length == 0) return;

        var dbManager = PropertyDatabaseManager.Instance;
        if (dbManager == null) return;
        var db = dbManager.GetDatabase<BuildableDatabase>();
        if (db == null)
        {
            Debug.LogWarning($"[EnemyGridBehaviour] BuildableDatabase not found. Cannot load preset.");
            return;
        }

        int totalPlaced = 0;

        for (int g = 0; g < startPreset.groups.Length; g++)
        {
            var group = startPreset.groups[g];
            string groupLabel = string.IsNullOrEmpty(group.groupName) ? $"Group[{g}]" : group.groupName;

            if (group.entries == null) continue;

            for (int i = 0; i < group.entries.Length; i++)
            {
                var entry = group.entries[i];
                var prop = db.GetByEnum(entry.buildableEnumKey);
                if (prop == null)
                {
                    Debug.LogWarning($"[EnemyGridBehaviour] Preset {groupLabel} entry [{i}]: No BuildableProperty for '{entry.buildableEnumKey}'. Skipped.");
                    continue;
                }
                if (prop.prefab == null)
                {
                    Debug.LogWarning($"[EnemyGridBehaviour] Preset {groupLabel} entry [{i}]: '{entry.buildableEnumKey}' has no prefab. Skipped.");
                    continue;
                }

                bool ok;
                if (group.forced)
                    ok = ForcePlaceImmediate(prop, entry.anchorCell, entry.rotationStep);
                else
                    ok = PlaceImmediate(prop, entry.anchorCell, entry.rotationStep);

                if (!ok)
                    Debug.LogWarning($"[EnemyGridBehaviour] Preset {groupLabel} entry [{i}]: Failed to place '{entry.buildableEnumKey}' at {entry.anchorCell}.");
                else
                    totalPlaced++;
            }
        }

        Debug.Log($"[EnemyGridBehaviour] Preset loaded: {startPreset.name} ({totalPlaced} placed)");

        OnGridChanged?.Invoke();
        EvaluateAndNotifyGridState(forceEmit: true);
    }

    private void EvaluateAndNotifyGridState(bool forceEmit = false)
    {
        bool isFulfilled = IsGridFulfilled;

        if (forceEmit || !lastGridFulfilledState.HasValue || lastGridFulfilledState.Value != isFulfilled)
        {
            lastGridFulfilledState = isFulfilled;
            OnGridStateChanged?.Invoke(isFulfilled);
        }

        if (isFulfilled)
            OnGridFulfilled?.Invoke();
    }

    /// <summary>
    /// Place a buildable at the given local cell with validation. Used by preset loading.
    /// </summary>
    private bool PlaceImmediate(BuildableProperty property, Vector3Int localAnchor, int rotationStep)
    {
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

        SpawnPlacedObject(data);
        return true;
    }

    /// <summary>
    /// Force-place a buildable at the given local cell, skipping validation. Used by preset loading.
    /// </summary>
    private bool ForcePlaceImmediate(BuildableProperty property, Vector3Int localAnchor, int rotationStep)
    {
        string instanceId = $"enemy_{gameObject.GetInstanceID()}_{instanceCounter++}";

        PlacedBuildableData data = new PlacedBuildableData
        {
            InstanceId = instanceId,
            Property = property,
            AnchorCell = localAnchor,
            RotationStep = rotationStep,
        };

        grid.ForcePlace(data);
        SpawnPlacedObject(data);
        return true;
    }

    /// <summary>
    /// Spawn the GameObject for a placed buildable and attach it as a child of this enemy.
    /// </summary>
    private void SpawnPlacedObject(PlacedBuildableData data)
    {
        Vector3 worldPos = LocalCellToWorldCenter(data.AnchorCell);
        float yaw = data.Property.GetRotationDegrees(data.RotationStep);
        Quaternion worldRot = transform.rotation * Quaternion.Euler(0f, yaw, 0f);

        GameObject go = Instantiate(data.Property.prefab, worldPos, worldRot, transform);
        go.transform.localScale = cellSize;
        data.SpawnedObject = go;

        var behaviour = go.GetComponent<BuildableBehaviour>();
        if (behaviour == null)
            behaviour = go.AddComponent<BuildableBehaviour>();
        behaviour.Initialize(data);
    }

    // ───────── Debug ─────────

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (debugPermanent)
            DrawGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugPermanent)
            DrawGizmos();
    }

    private void DrawGizmos()
    {
        if (!enableDebug) return;

        // Build cell list for editor preview (outside play mode cachedAllCells may be empty)
        List<Vector3Int> drawCells = cachedAllCells;
        if (drawCells == null || drawCells.Count == 0)
        {
            drawCells = new List<Vector3Int>();
            if (boundsBoxes != null)
                for (int b = 0; b < boundsBoxes.Length; b++)
                    boundsBoxes[b].GenerateCells(drawCells);
            if (boundsCells != null)
                for (int i = 0; i < boundsCells.Length; i++)
                    drawCells.Add(boundsCells[i]);
        }

        if (drawCells.Count == 0) return;

        // Draw individual cell wireframes
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        for (int i = 0; i < drawCells.Count; i++)
        {
            Vector3 center = LocalCellToWorldCenter(drawCells[i]);
            Vector3 size = transform.TransformVector(cellSize);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            Gizmos.DrawWireCube(center, size * 0.95f);
        }

        // Compute AABB for overall bounds display
        Vector3 boundsMin = LocalCellToWorldCenter(drawCells[0]);
        Vector3 boundsMax = boundsMin;
        for (int i = 1; i < drawCells.Count; i++)
        {
            Vector3 p = LocalCellToWorldCenter(drawCells[i]);
            boundsMin = Vector3.Min(boundsMin, p);
            boundsMax = Vector3.Max(boundsMax, p);
        }
        Vector3 worldCellSize = transform.TransformVector(cellSize);
        worldCellSize = new Vector3(Mathf.Abs(worldCellSize.x), Mathf.Abs(worldCellSize.y), Mathf.Abs(worldCellSize.z));
        Vector3 overallCenter = (boundsMin + boundsMax) * 0.5f;
        Vector3 overallSize = (boundsMax - boundsMin) + worldCellSize;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(overallCenter, overallSize);

        // Forced occupied cells
        Vector3Int[] forcedCells = forcedOccupiedRegion.GatherAllCells();
        if (forcedCells != null && forcedCells.Length > 0)
        {
            Gizmos.color = Color.red;
            for (int i = 0; i < forcedCells.Length; i++)
            {
                Vector3 center = LocalCellToWorldCenter(forcedCells[i]);
                Gizmos.DrawWireCube(center, Vector3.one * 0.5f);
            }
        }

        // Label
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(overallCenter + Vector3.up * (overallSize.y * 0.5f + 0.3f),
            $"EnemyGrid [{drawCells.Count} cells]\n" +
            $"Filled: {(grid != null ? grid.OccupiedCellCount : 0)}/{(grid != null ? grid.TotalCellCount : 0)}");
    }
#endif
}
