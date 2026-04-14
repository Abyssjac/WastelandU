using System.Collections.Generic;
using UnityEngine;
using JackyUtility;

// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
// Room Detection Policy
// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

/// <summary>
/// Defines the rules for how flood-fill determines room boundaries.
/// Swap implementations to change what counts as "enclosed".
/// </summary>
public interface IRoomDetectionPolicy
{
    /// <summary>
    /// Returns true when traversal from <paramref name="from"/> toward
    /// <paramref name="to"/> in the given <paramref name="facing"/> direction
    /// is blocked (i.e. there is a wall face between the two cells).
    /// </summary>
    bool IsBarrier(Vector3Int from, Vector3Int to, SurfaceFacing facing, IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> occupancyMap);

    /// <summary>
    /// Returns true when <paramref name="cell"/> is a valid candidate for
    /// the interior of a room (seed cells for flood-fill).
    /// </summary>
    bool IsValidInteriorCell(Vector3Int cell, BuildGrid3D grid);
}

/// <summary>
/// Default policy: a room is fully enclosed by BL_Wall faces on all 6 directions
/// (¡ÀX, ¡ÀZ walls + YNeg floor + YPos ceiling).
/// </summary>
public class FullEnclosurePolicy : IRoomDetectionPolicy
{
    /// <summary>
    /// Blocked when EITHER the source cell has a wall face in <paramref name="facing"/>
    /// direction OR the destination cell has a wall face in the opposite direction.
    /// </summary>
    public bool IsBarrier(Vector3Int from, Vector3Int to, SurfaceFacing facing,
        IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> occupancyMap)
    {
        // Wall on the exit face of 'from'
        if (occupancyMap.ContainsKey(new CellLayerKey(from, BuildLayer.BL_World, facing)))
            return true;

        // Wall on the entry face of 'to'
        SurfaceFacing opposite = GetOppositeFacing(facing);
        if (occupancyMap.ContainsKey(new CellLayerKey(to, BuildLayer.BL_World, opposite)))
            return true;

        return false;
    }

    /// <summary>
    /// A cell is a valid interior candidate when it is inside grid bounds and
    /// is NOT itself fully solid-occupied on the Wall layer (i.e. it's air space,
    /// possibly with directional wall faces but not a full block).
    /// </summary>
    public bool IsValidInteriorCell(Vector3Int cell, BuildGrid3D grid)
    {
        if (!grid.IsInBounds(cell)) return false;

        // If the cell itself (non-directional) is occupied as a wall, treat it as solid block ¡ú not interior
        if (grid.IsCellOccupied(cell, BuildLayer.BL_World, SurfaceFacing.None))
            return false;

        return true;
    }

    public static SurfaceFacing GetOppositeFacing(SurfaceFacing facing)
    {
        switch (facing)
        {
            case SurfaceFacing.XPos: return SurfaceFacing.XNeg;
            case SurfaceFacing.XNeg: return SurfaceFacing.XPos;
            case SurfaceFacing.ZPos: return SurfaceFacing.ZNeg;
            case SurfaceFacing.ZNeg: return SurfaceFacing.ZPos;
            case SurfaceFacing.YPos: return SurfaceFacing.YNeg;
            case SurfaceFacing.YNeg: return SurfaceFacing.YPos;
            default: return SurfaceFacing.None;
        }
    }
}

// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
// Room Data
// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

/// <summary>
/// Runtime data for a single detected room.
/// </summary>
public class RoomData
{
    public int RoomId { get; private set; }
    public HashSet<Vector3Int> Cells { get; private set; }
    public Color DebugColor { get; private set; }

    public int CellCount => Cells.Count;

    public RoomData(int roomId, HashSet<Vector3Int> cells, Color debugColor)
    {
        RoomId = roomId;
        Cells = cells;
        DebugColor = debugColor;
    }

    public bool Contains(Vector3Int cell)
    {
        return Cells.Contains(cell);
    }
}

// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
// Room removal preview result
// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

/// <summary>
/// Result of a "would this removal break a room?" query.
/// </summary>
public struct RoomBreakResult
{
    /// <summary>True if at least one room would be destroyed or modified.</summary>
    public bool WouldBreak;

    /// <summary>Rooms that would be destroyed (no longer enclosed) after the removal.</summary>
    public List<RoomData> AffectedRooms;
}

// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
// GridRoomManager
// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

/// <summary>
/// Detects enclosed rooms in the <see cref="BuildGrid3D"/> via face-aware flood-fill.
/// Fully independent from <see cref="BuildManager"/> ¡ª reads the grid as a data source.
/// Subscribes to <see cref="BuildManager.OnGridChanged"/> to recalculate when the grid mutates.
/// </summary>
public class GridRoomManager : MonoBehaviour, IDebuggable
{
    public static GridRoomManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private BuildPositionProvider positionProvider;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;
    [SerializeField] private bool debugDrawRooms = false;

    // ©¤©¤ IDebuggable ©¤©¤
    public string DebugId => "gridroom";
    public bool DebugEnabled
    {
        get => enableDebug;
        set => enableDebug = value;
    }

    // ©¤©¤ State ©¤©¤
    private BuildGrid3D grid;
    private IRoomDetectionPolicy policy;
    private List<RoomData> activeRooms = new List<RoomData>();
    private Dictionary<Vector3Int, RoomData> cellToRoom = new Dictionary<Vector3Int, RoomData>();
    private int nextRoomId;

    /// <summary>All currently detected rooms.</summary>
    public IReadOnlyList<RoomData> ActiveRooms => activeRooms;

    // ©¤©¤ Direction table for 6-way flood-fill ©¤©¤
    private static readonly SurfaceFacing[] s_allDirections =
    {
        SurfaceFacing.XPos, SurfaceFacing.XNeg,
        SurfaceFacing.ZPos, SurfaceFacing.ZNeg,
        SurfaceFacing.YPos, SurfaceFacing.YNeg,
    };

    private static readonly Vector3Int[] s_directionOffsets =
    {
        Vector3Int.right,                    // XPos
        Vector3Int.left,                     // XNeg
        new Vector3Int(0, 0, 1),             // ZPos
        new Vector3Int(0, 0, -1),            // ZNeg
        Vector3Int.up,                       // YPos
        Vector3Int.down,                     // YNeg
    };

    // Stable palette for debug visualization (up to 16 rooms, then wraps)
    private static readonly Color[] s_roomColors =
    {
        new Color(0.2f, 0.8f, 0.2f, 0.35f),  // green
        new Color(0.2f, 0.5f, 1.0f, 0.35f),  // blue
        new Color(1.0f, 0.6f, 0.1f, 0.35f),  // orange
        new Color(0.9f, 0.2f, 0.9f, 0.35f),  // magenta
        new Color(0.0f, 0.9f, 0.9f, 0.35f),  // cyan
        new Color(1.0f, 1.0f, 0.2f, 0.35f),  // yellow
        new Color(0.6f, 0.3f, 0.1f, 0.35f),  // brown
        new Color(0.5f, 1.0f, 0.5f, 0.35f),  // light green
        new Color(1.0f, 0.4f, 0.4f, 0.35f),  // salmon
        new Color(0.4f, 0.4f, 1.0f, 0.35f),  // lavender
        new Color(0.8f, 0.8f, 0.0f, 0.35f),  // olive
        new Color(0.0f, 0.6f, 0.6f, 0.35f),  // teal
        new Color(0.7f, 0.0f, 0.5f, 0.35f),  // plum
        new Color(1.0f, 0.8f, 0.6f, 0.35f),  // peach
        new Color(0.3f, 0.7f, 0.3f, 0.35f),  // forest
        new Color(0.6f, 0.6f, 0.9f, 0.35f),  // periwinkle
    };

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        policy = new FullEnclosurePolicy();
    }

    private void Start()
    {
        if (BuildManager.Instance != null)
        {
            grid = BuildManager.Instance.Grid;
            BuildManager.Instance.OnGridChanged += OnGridChanged;
        }

        if (grid == null)
            Debug.LogWarning("[GridRoomManager] BuildManager.Instance.Grid is null. Room detection disabled until grid is available.");

        DebugConsoleManager.Instance.RegisterDebugTarget(this);
        RegisterDebugCommands();
    }

    private void OnDestroy()
    {
        if (BuildManager.Instance != null)
            BuildManager.Instance.OnGridChanged -= OnGridChanged;

        if (DebugConsoleManager.Instance != null)
            DebugConsoleManager.Instance.UnregisterDebugTarget(this);
    }

    private void OnGridChanged()
    {
        // Lazy-acquire grid if it wasn't ready at Start
        if (grid == null && BuildManager.Instance != null)
            grid = BuildManager.Instance.Grid;

        if (grid != null)
            RecalculateAllRooms();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Replace the active room detection policy at runtime.
    /// Automatically triggers a recalculation.
    /// </summary>
    public void SetPolicy(IRoomDetectionPolicy newPolicy)
    {
        policy = newPolicy;
        if (grid != null) RecalculateAllRooms();
    }

    /// <summary>
    /// Returns the <see cref="RoomData"/> that contains the given cell, or null if the cell is not inside any room.
    /// </summary>
    public RoomData GetRoomAtCell(Vector3Int cell)
    {
        cellToRoom.TryGetValue(cell, out RoomData room);
        return room;
    }

    /// <summary>
    /// Predicts whether removing the given buildable would break (un-enclose) any existing room.
    /// Does NOT modify any state ¡ª this is a pure read-only simulation.
    /// </summary>
    public RoomBreakResult WouldRemoveBreakRoom(PlacedBuildableData data)
    {
        var result = new RoomBreakResult { WouldBreak = false, AffectedRooms = new List<RoomData>() };
        if (data == null || grid == null) return result;

        // 1. Determine which wall faces this buildable contributes
        //    (only BL_Wall occupancy matters for room boundaries)
        ResolvedOccupancyCell[] occ = data.Property.GetRotatedOccupancyCells(data.RotationStep);
        List<CellLayerKey> wallKeys = new List<CellLayerKey>();
        for (int i = 0; i < occ.Length; i++)
        {
            if (occ[i].Layer == BuildLayer.BL_Wall)
            {
                Vector3Int worldCell = data.AnchorCell + occ[i].Cell;
                wallKeys.Add(new CellLayerKey(worldCell, BuildLayer.BL_Wall, occ[i].OccupancyFacing));
            }
        }

        // If this buildable has no wall-layer occupancy, it can't break any room
        if (wallKeys.Count == 0) return result;

        // 2. Quick check: do any of the wall faces touch an existing room?
        HashSet<int> potentiallyAffectedIds = new HashSet<int>();
        for (int i = 0; i < wallKeys.Count; i++)
        {
            Vector3Int cell = wallKeys[i].Cell;

            // Check adjacent cells on both sides of this wall face
            for (int d = 0; d < s_allDirections.Length; d++)
            {
                Vector3Int neighbor = cell + s_directionOffsets[d];
                if (cellToRoom.TryGetValue(neighbor, out RoomData neighborRoom))
                    potentiallyAffectedIds.Add(neighborRoom.RoomId);
            }

            // Also check the cell itself
            if (cellToRoom.TryGetValue(cell, out RoomData selfRoom))
                potentiallyAffectedIds.Add(selfRoom.RoomId);
        }

        if (potentiallyAffectedIds.Count == 0) return result;

        // 3. Build a simulated occupancy map without this buildable's wall keys
        HashSet<CellLayerKey> removedKeys = new HashSet<CellLayerKey>();
        for (int i = 0; i < wallKeys.Count; i++)
            removedKeys.Add(wallKeys[i]);

        var simulatedOccupancy = new SimulatedOccupancyMap(grid.OccupancyMap, removedKeys);

        // 4. For each potentially affected room, re-flood-fill from one of its cells
        //    using the simulated map. If the fill escapes ¡ú room is broken.
        foreach (int roomId in potentiallyAffectedIds)
        {
            RoomData room = FindRoomById(roomId);
            if (room == null) continue;

            // Pick any cell from this room as seed
            Vector3Int seed = Vector3Int.zero;
            foreach (Vector3Int c in room.Cells) { seed = c; break; }

            bool escaped = false;
            HashSet<Vector3Int> visited = new HashSet<Vector3Int>();
            Queue<Vector3Int> queue = new Queue<Vector3Int>();
            queue.Enqueue(seed);
            visited.Add(seed);

            while (queue.Count > 0)
            {
                Vector3Int current = queue.Dequeue();

                for (int d = 0; d < s_allDirections.Length; d++)
                {
                    SurfaceFacing facing = s_allDirections[d];
                    Vector3Int neighbor = current + s_directionOffsets[d];

                    if (!grid.IsInBounds(neighbor))
                    {
                        escaped = true;
                        break;
                    }

                    if (visited.Contains(neighbor)) continue;
                    if (!policy.IsValidInteriorCell(neighbor, grid)) continue;

                    // Use simulated map for barrier check
                    if (IsBarrierSimulated(current, neighbor, facing, simulatedOccupancy)) continue;

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }

                if (escaped) break;
            }

            if (escaped)
            {
                result.WouldBreak = true;
                result.AffectedRooms.Add(room);
            }
        }

        return result;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Core Algorithm ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Full recalculation: flood-fill all potential interior cells, classify enclosed regions as rooms.
    /// </summary>
    private void RecalculateAllRooms()
    {
        activeRooms.Clear();
        cellToRoom.Clear();
        nextRoomId = 0;

        if (grid == null || policy == null) return;

        var occupancyMap = grid.OccupancyMap;
        HashSet<Vector3Int> globalVisited = new HashSet<Vector3Int>();

        // Iterate every cell in grid bounds
        Vector3Int min = grid.GridMin;
        Vector3Int max = grid.GridMax;

        for (int y = min.y; y < max.y; y++)
        {
            for (int z = min.z; z < max.z; z++)
            {
                for (int x = min.x; x < max.x; x++)
                {
                    Vector3Int cell = new Vector3Int(x, y, z);

                    if (globalVisited.Contains(cell)) continue;
                    if (!policy.IsValidInteriorCell(cell, grid)) continue;

                    // Flood-fill from this cell
                    bool escaped = false;
                    HashSet<Vector3Int> region = new HashSet<Vector3Int>();
                    Queue<Vector3Int> queue = new Queue<Vector3Int>();

                    queue.Enqueue(cell);
                    region.Add(cell);

                    while (queue.Count > 0)
                    {
                        Vector3Int current = queue.Dequeue();

                        for (int d = 0; d < s_allDirections.Length; d++)
                        {
                            SurfaceFacing facing = s_allDirections[d];
                            Vector3Int neighbor = current + s_directionOffsets[d];

                            // If we can reach outside grid bounds ¡ú this region is "outdoors"
                            if (!grid.IsInBounds(neighbor))
                            {
                                escaped = true;
                                continue; // keep filling to mark all cells as visited
                            }

                            if (region.Contains(neighbor)) continue;
                            if (!policy.IsValidInteriorCell(neighbor, grid)) continue;

                            // Face-aware barrier check
                            if (IsBarrier(current, neighbor, facing, occupancyMap)) continue;

                            region.Add(neighbor);
                            queue.Enqueue(neighbor);
                        }
                    }

                    // Mark all cells in this region as globally visited regardless of outcome
                    foreach (Vector3Int c in region)
                        globalVisited.Add(c);

                    // Only register as a room if the region never escaped
                    if (!escaped && region.Count > 0)
                    {
                        int id = nextRoomId++;
                        Color color = s_roomColors[id % s_roomColors.Length];
                        RoomData room = new RoomData(id, region, color);
                        activeRooms.Add(room);

                        foreach (Vector3Int c in region)
                            cellToRoom[c] = room;
                    }
                }
            }
        }

        if (enableDebug)
        {
            Debug.Log($"[GridRoomManager] Recalculated: {activeRooms.Count} room(s) detected.");
            for (int i = 0; i < activeRooms.Count; i++)
                Debug.Log($"  Room {activeRooms[i].RoomId}: {activeRooms[i].CellCount} cells");
        }
    }

    /// <summary>
    /// Face-aware barrier check using the real occupancy map.
    /// </summary>
    private bool IsBarrier(Vector3Int from, Vector3Int to, SurfaceFacing facing,
        IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> occupancyMap)
    {
        return policy.IsBarrier(from, to, facing, occupancyMap);
    }

    /// <summary>
    /// Face-aware barrier check using a simulated occupancy map (for removal preview).
    /// </summary>
    private bool IsBarrierSimulated(Vector3Int from, Vector3Int to, SurfaceFacing facing,
        SimulatedOccupancyMap simulatedMap)
    {
        // Re-implement the same logic as FullEnclosurePolicy but against the simulated map
        if (simulatedMap.ContainsKey(new CellLayerKey(from, BuildLayer.BL_Wall, facing)))
            return true;

        SurfaceFacing opposite = FullEnclosurePolicy.GetOppositeFacing(facing);
        if (simulatedMap.ContainsKey(new CellLayerKey(to, BuildLayer.BL_Wall, opposite)))
            return true;

        return false;
    }

    private RoomData FindRoomById(int roomId)
    {
        for (int i = 0; i < activeRooms.Count; i++)
        {
            if (activeRooms[i].RoomId == roomId)
                return activeRooms[i];
        }
        return null;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Simulated Occupancy Map ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Thin read-only wrapper over the real occupancy map that pretends
    /// certain keys don't exist (for removal simulation).
    /// </summary>
    private struct SimulatedOccupancyMap
    {
        private readonly IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> real;
        private readonly HashSet<CellLayerKey> excluded;

        public SimulatedOccupancyMap(IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> real, HashSet<CellLayerKey> excluded)
        {
            this.real = real;
            this.excluded = excluded;
        }

        public bool ContainsKey(CellLayerKey key)
        {
            if (excluded.Contains(key)) return false;
            return real.ContainsKey(key);
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Debug Commands ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void RegisterDebugCommands()
    {
        if (DebugConsoleManager.Instance == null) return;

        DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
            "roominfo",
            "List all detected rooms and their cell counts.",
            args =>
            {
                if (activeRooms.Count == 0)
                {
                    Debug.Log("[GridRoomManager] No rooms detected.");
                    return;
                }

                Debug.Log($"[GridRoomManager] {activeRooms.Count} room(s):");
                for (int i = 0; i < activeRooms.Count; i++)
                {
                    var room = activeRooms[i];
                    Debug.Log($"  Room {room.RoomId}: {room.CellCount} cells");
                }
            }
        ));

        DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
            "roominfo-detail",
            "List all detected rooms with every cell coordinate. Usage: roominfo-detail [roomId]",
            args =>
            {
                if (activeRooms.Count == 0)
                {
                    Debug.Log("[GridRoomManager] No rooms detected.");
                    return;
                }

                // Optional filter by room ID
                int filterRoomId = -1;
                if (args.Length > 0)
                    int.TryParse(args[0], out filterRoomId);

                for (int i = 0; i < activeRooms.Count; i++)
                {
                    var room = activeRooms[i];
                    if (filterRoomId >= 0 && room.RoomId != filterRoomId) continue;

                    Debug.Log($"  Room {room.RoomId} ({room.CellCount} cells):");
                    foreach (Vector3Int c in room.Cells)
                        Debug.Log($"    {c}");
                }
            }
        ));

        DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
            "gridroom-gizmo",
            "Toggle room gizmo visualization in Scene view.",
            args =>
            {
                debugDrawRooms = !debugDrawRooms;
                Debug.Log($"[GridRoomManager] Room gizmo: {(debugDrawRooms ? "ON" : "OFF")}");
            }
        ));

        DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
            "gridroom-recalc",
            "Force room recalculation now.",
            args =>
            {
                if (grid == null)
                {
                    Debug.LogWarning("[GridRoomManager] Grid not available.");
                    return;
                }
                RecalculateAllRooms();
                Debug.Log("[GridRoomManager] Forced recalculation complete.");
            }
        ));
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Debug Gizmos ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void OnDrawGizmos()
    {
        if (!debugDrawRooms) return;
        if (activeRooms == null || activeRooms.Count == 0) return;
        if (positionProvider == null) return;

        Vector3 cellSize = positionProvider.CellSize;
        Vector3 cubeSize = cellSize * 0.85f;

        for (int i = 0; i < activeRooms.Count; i++)
        {
            RoomData room = activeRooms[i];
            Color fillColor = room.DebugColor;
            Color wireColor = fillColor;
            wireColor.a = 0.9f;

            foreach (Vector3Int cell in room.Cells)
            {
                Vector3 worldCenter = positionProvider.CellToWorldCenter(cell);

                Gizmos.color = fillColor;
                Gizmos.DrawCube(worldCenter, cubeSize);

                Gizmos.color = wireColor;
                Gizmos.DrawWireCube(worldCenter, cellSize);
            }

#if UNITY_EDITOR
            // Draw room label at the first cell (arbitrary but stable per recalc)
            Vector3Int labelCell = Vector3Int.zero;
            foreach (Vector3Int c in room.Cells) { labelCell = c; break; }
            Vector3 labelPos = positionProvider.CellToWorldCenter(labelCell) + Vector3.up * (cellSize.y * 0.7f);
            UnityEditor.Handles.color = wireColor;
            UnityEditor.Handles.Label(labelPos, $"Room {room.RoomId}\n{room.CellCount} cells");
#endif
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Debug GUI ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void OnGUI()
    {
        if (!enableDebug) return;

        float yOffset = 300f; // below BuildManager's debug panel
        var panel = DebugGUIPanel.Begin(new Vector2(10f, yOffset), 360f, 16);

        panel.DrawLine("<b>©¤©¤ GridRoomManager ©¤©¤</b>");
        panel.DrawLine($"Rooms: <color=cyan><b>{activeRooms.Count}</b></color>");
        panel.DrawLine($"Gizmo: {(debugDrawRooms ? "<color=lime>ON</color>" : "<color=red>OFF</color>")}");

        for (int i = 0; i < activeRooms.Count && i < 8; i++)
        {
            var room = activeRooms[i];
            panel.DrawLine($"  Room {room.RoomId}: {room.CellCount} cells");
        }

        if (activeRooms.Count > 8)
            panel.DrawLine($"  ... and {activeRooms.Count - 8} more");

        panel.End();
    }
}
