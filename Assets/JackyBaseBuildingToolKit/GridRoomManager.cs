using System.Collections.Generic;
using System;
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

    /// <summary>
    /// Called after flood-fill confirms a region is enclosed.
    /// Returns true when the region satisfies any additional criteria required
    /// to be recognised as a room (e.g. has a door on its boundary).
    /// </summary>
    bool ValidateRegion(HashSet<Vector3Int> region,
        IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> occupancyMap,
        BuildGrid3D grid);
}

/// <summary>
/// Default policy: a room is fully enclosed by BL_Wall faces on all 6 directions
/// (¡ÀX, ¡ÀZ walls + YNeg floor + YPos ceiling).
/// </summary>
public class FullEnclosurePolicy : IRoomDetectionPolicy
{
    private readonly HashSet<Key_BuildablePP> doorKeySet;

    /// <param name="doorKeys">
    /// Keys of all buildables that count as a door.
    /// At least one must be present on the boundary for the region to become a room.
    /// Pass null or empty array to require no door (any enclosure becomes a room).
    /// </param>
    public FullEnclosurePolicy(Key_BuildablePP[] doorKeys = null)
    {
        doorKeySet = doorKeys != null && doorKeys.Length > 0
            ? new HashSet<Key_BuildablePP>(doorKeys)
            : null;
    }
    /// <summary>
    /// Blocked when EITHER the source cell has a wall face in <paramref name="facing"/>
    /// direction OR the destination cell has a wall face in the opposite direction.
    /// </summary>
    public bool IsBarrier(Vector3Int from, Vector3Int to, SurfaceFacing facing,
        IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> occupancyMap)
    {
        // Wall on the exit face of 'from'
        if (occupancyMap.ContainsKey(new CellLayerKey(from, BuildLayer.BL_Wall, facing)))
            return true;

        // Wall on the entry face of 'to'
        SurfaceFacing opposite = GetOppositeFacing(facing);
        if (occupancyMap.ContainsKey(new CellLayerKey(to, BuildLayer.BL_Wall, opposite)))
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
        if (grid.IsCellOccupied(cell, BuildLayer.BL_Wall, SurfaceFacing.None))
            return false;

        return true;
    }

    /// <summary>
    /// If door keys were provided, scans every face of every interior cell.
    /// A door counts only when it sits on the TRUE boundary of the region ¡ª
    /// i.e. the cell on the OTHER side of the door face is NOT part of the
    /// room interior. This rejects doors placed in the middle of the room
    /// whose both sides are interior cells.
    /// If no door keys were configured, any enclosed region is accepted.
    /// </summary>
    public bool ValidateRegion(HashSet<Vector3Int> region,
        IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> occupancyMap,
        BuildGrid3D grid)
    {
        // No door requirement configured ¡ú accept any enclosure
        if (doorKeySet == null) return true;

        foreach (Vector3Int cell in region)
        {
            for (int d = 0; d < 6; d++)
            {
                SurfaceFacing facing = s_facings[d];
                if (!occupancyMap.TryGetValue(new CellLayerKey(cell, BuildLayer.BL_Wall, facing), out PlacedBuildableData data))
                    continue;

                if (!doorKeySet.Contains(data.Property.EnumKey))
                    continue;

                // A boundary door: the neighbor on the far side must NOT be in the room interior.
                // If it IS in the region, both sides are interior ¡ú this door is placed in the
                // middle of the room and does not qualify.
                Vector3Int neighbor = cell + s_offsets[d];
                if (!region.Contains(neighbor))
                    return true;
            }
        }

        return false;
    }

    // Direction arrays reused by ValidateRegion (avoids per-call allocation)
    private static readonly SurfaceFacing[] s_facings =
    {
        SurfaceFacing.XPos, SurfaceFacing.XNeg,
        SurfaceFacing.ZPos, SurfaceFacing.ZNeg,
        SurfaceFacing.YPos, SurfaceFacing.YNeg,
    };

    private static readonly Vector3Int[] s_offsets =
    {
        Vector3Int.right,           // XPos
        Vector3Int.left,            // XNeg
        new Vector3Int(0, 0,  1),   // ZPos
        new Vector3Int(0, 0, -1),   // ZNeg
        Vector3Int.up,              // YPos
        Vector3Int.down,            // YNeg
    };

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

/// <summary>
/// Flat-room policy (Two Point Hospital style).
/// A room is enclosed by a floor (YNeg face) and four horizontal walls (¡ÀX, ¡ÀZ).
/// The ceiling is provided by a transparent placeholder buildable injected via preset.
/// Interior cells must have a floor piece placed (BL_Wall + YNeg) to be considered valid.
/// </summary>
public class FlatRoomPolicy : IRoomDetectionPolicy
{
    /// <summary>
    /// Same barrier logic as <see cref="FullEnclosurePolicy"/>: blocked when either
    /// the source or the destination cell has a BL_Wall wall face in the relevant direction.
    /// </summary>
    public bool IsBarrier(Vector3Int from, Vector3Int to, SurfaceFacing facing,
        IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> occupancyMap)
    {
        if (occupancyMap.ContainsKey(new CellLayerKey(from, BuildLayer.BL_Wall, facing)))
            return true;

        SurfaceFacing opposite = FullEnclosurePolicy.GetOppositeFacing(facing);
        if (occupancyMap.ContainsKey(new CellLayerKey(to, BuildLayer.BL_Wall, opposite)))
            return true;

        return false;
    }

    /// <summary>
    /// A cell is a valid interior candidate when:
    /// 1. It is within grid bounds.
    /// 2. It is NOT a solid block (BL_Wall, SurfaceFacing.None).
    /// 3. It HAS a floor piece (BL_Wall, SurfaceFacing.YNeg).
    /// </summary>
    public bool IsValidInteriorCell(Vector3Int cell, BuildGrid3D grid)
    {
        if (!grid.IsInBounds(cell)) return false;

        if (grid.IsCellOccupied(cell, BuildLayer.BL_Wall, SurfaceFacing.None))
            return false;

        if (!grid.IsCellOccupied(cell, BuildLayer.BL_Wall, SurfaceFacing.YNeg))
            return false;

        return true;
    }

    /// <summary>
    /// No additional validation for flat rooms ¡ª any enclosed region with a floor qualifies.
    /// Door requirement can be added here in the future.
    /// </summary>
    public bool ValidateRegion(HashSet<Vector3Int> region,
        IReadOnlyDictionary<CellLayerKey, PlacedBuildableData> occupancyMap,
        BuildGrid3D grid)
    {
        return true;
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

    // ©¤©¤ Furniture tag counts ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private readonly Dictionary<FurnitureTag, int> _tagCounts = new Dictionary<FurnitureTag, int>();

    /// <summary>Current count of each furniture tag present in this room.</summary>
    public IReadOnlyDictionary<FurnitureTag, int> TagCounts => _tagCounts;

    /// <summary>
    /// Deterministic stable identifier: the lexicographically smallest cell
    /// (min X ¡ú min Z ¡ú min Y) in the room's cell set.
    /// Remains identical across recalculations as long as the room shape is unchanged.
    /// </summary>
    public Vector3Int StableId { get; private set; }

    public RoomData(int roomId, HashSet<Vector3Int> cells, Color debugColor)
    {
        RoomId = roomId;
        Cells = cells;
        DebugColor = debugColor;
        StableId = ComputeStableId(cells);
    }

    public bool Contains(Vector3Int cell) => Cells.Contains(cell);

    /// <summary>
    /// Returns the lexicographically smallest cell (min X ¡ú min Z ¡ú min Y)
    /// from <paramref name="cells"/> as a deterministic room fingerprint.
    /// </summary>
    public static Vector3Int ComputeStableId(HashSet<Vector3Int> cells)
    {
        var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
        foreach (Vector3Int c in cells)
        {
            if (c.x < min.x
             || (c.x == min.x && c.z < min.z)
             || (c.x == min.x && c.z == min.z && c.y < min.y))
                min = c;
        }
        return min;
    }

    /// <summary>Returns the count for a single tag bit. Returns 0 if the tag is absent.</summary>
    public int GetTagCount(FurnitureTag tag)
    {
        _tagCounts.TryGetValue(tag, out int count);
        return count;
    }

    /// <summary>Increments counts for every individual flag set in <paramref name="tags"/>.</summary>
    public void AddFurnitureTags(FurnitureTag tags)
    {
        foreach (FurnitureTag tag in Enum.GetValues(typeof(FurnitureTag)))
        {
            if (tag == FurnitureTag.None) continue;
            if ((tags & tag) != 0)
            {
                _tagCounts.TryGetValue(tag, out int current);
                _tagCounts[tag] = current + 1;
            }
        }
    }

    /// <summary>Decrements counts for every individual flag set in <paramref name="tags"/>. Removes the entry when it reaches zero.</summary>
    public void RemoveFurnitureTags(FurnitureTag tags)
    {
        foreach (FurnitureTag tag in Enum.GetValues(typeof(FurnitureTag)))
        {
            if (tag == FurnitureTag.None) continue;
            if ((tags & tag) != 0 && _tagCounts.TryGetValue(tag, out int current))
            {
                int next = current - 1;
                if (next <= 0)
                    _tagCounts.Remove(tag);
                else
                    _tagCounts[tag] = next;
            }
        }
    }

    /// <summary>Resets all tag counts. Called before a full furniture rescan.</summary>
    public void ClearTagCounts() => _tagCounts.Clear();
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

/// <summary>
/// Diff result broadcast by <see cref="GridRoomManager.OnRoomsRecalculated"/> after every
/// full room recalculation. Consumers can react only to the rooms that actually changed.
/// </summary>
public struct RoomsRecalculatedArgs
{
    /// <summary>Rooms that did not exist in the previous recalculation.</summary>
    public IReadOnlyList<RoomData> Added;

    /// <summary>Rooms that existed before but are no longer detected.</summary>
    public IReadOnlyList<RoomData> Removed;

    /// <summary>Rooms whose StableId exists in both old and new results (shape unchanged).</summary>
    public IReadOnlyList<RoomData> Unchanged;
}

// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
// GridRoomManager
// ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

/// <summary>Selects which room detection policy is used by <see cref="GridRoomManager"/>.</summary>
public enum RoomPolicyType
{
    /// <summary>All 6 faces (floor + 4 walls + ceiling) required. Original behaviour.</summary>
    FullEnclosure,
    /// <summary>Floor + 4 horizontal walls required. Ceiling supplied by a transparent preset buildable.</summary>
    FlatRoom,
}

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

    [Header("Policy")]
    [Tooltip("FullEnclosure: requires all 6 faces (original).\nFlatRoom: floor + 4 walls only; ceiling is a transparent preset buildable.")]
    [SerializeField] private RoomPolicyType policyType = RoomPolicyType.FlatRoom;

    [Tooltip("(FullEnclosure only) Buildable keys that count as a door.\n" +
             "At least one must be present on the room boundary for the region to be recognised as a room.\n" +
             "Leave empty to accept any enclosed region without requiring a door.")]
    [SerializeField] private Key_BuildablePP[] doorKeys = new Key_BuildablePP[0];

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

    // Tracks the previous recalculation's rooms by StableId for diff computation
    private Dictionary<Vector3Int, RoomData> _prevRoomsByStableId = new Dictionary<Vector3Int, RoomData>();

    /// <summary>All currently detected rooms.</summary>
    public IReadOnlyList<RoomData> ActiveRooms => activeRooms;

    /// <summary>
    /// Fired after furniture tag counts change in a room ¡ª either from an incremental
    /// furniture place/remove or after a full room recalculation.
    /// The <see cref="RoomData"/> argument is the affected room with updated tag counts.
    /// </summary>
    public event Action<RoomData> OnRoomFurnitureChanged;

    /// <summary>
    /// Fired after every full room recalculation with a diff of added, removed, and unchanged rooms.
    /// Subscribe here to react to room structure changes (e.g. hierarchy management).
    /// </summary>
    public event Action<RoomsRecalculatedArgs> OnRoomsRecalculated;

#if UNITY_EDITOR
    /// <summary>Forces an immediate room recalculation. Editor tooling only.</summary>
    public void Editor_ForceRecalculate() => RecalculateAllRooms();
    /// <summary>The active policy type. Editor tooling only.</summary>
    public RoomPolicyType Editor_PolicyType => policyType;
#endif

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

        policy = policyType == RoomPolicyType.FlatRoom
            ? (IRoomDetectionPolicy)new FlatRoomPolicy()
            : new FullEnclosurePolicy(doorKeys);
    }

    private void Start()
    {
        if (BuildManager.Instance != null)
        {
            grid = BuildManager.Instance.Grid;
            BuildManager.Instance.OnGridChanged += OnGridChanged;
            BuildManager.Instance.OnFurniturePlaced  += HandleFurniturePlaced;
            BuildManager.Instance.OnFurnitureRemoved += HandleFurnitureRemoved;
        }

        if (grid == null)
            Debug.LogWarning("[GridRoomManager] BuildManager.Instance.Grid is null. Room detection disabled until grid is available.");

        DebugConsoleManager.Instance.RegisterDebugTarget(this);
        RegisterDebugCommands();
    }

    private void OnDestroy()
    {
        if (BuildManager.Instance != null)
        {
            BuildManager.Instance.OnGridChanged      -= OnGridChanged;
            BuildManager.Instance.OnFurniturePlaced  -= HandleFurniturePlaced;
            BuildManager.Instance.OnFurnitureRemoved -= HandleFurnitureRemoved;
        }

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
            RoomData room = GetRoomById(roomId);
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
                        // Additional policy validation (e.g. door-on-boundary check)
                        if (!policy.ValidateRegion(region, occupancyMap, grid))
                        {
                            if (enableDebug)
                                Debug.Log($"[GridRoomManager] Enclosed region of {region.Count} cell(s) rejected by ValidateRegion (e.g. no door on boundary).");
                            continue;
                        }

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

        // ©¤©¤ Rebuild furniture tag counts for all freshly detected rooms ©¤©¤
        ScanFurnitureTagsForAllRooms();

        // ©¤©¤ Diff against previous recalculation and broadcast ©¤©¤
        var newByStableId = new Dictionary<Vector3Int, RoomData>(activeRooms.Count);
        foreach (var r in activeRooms)
            newByStableId[r.StableId] = r;

        var added     = new List<RoomData>();
        var removed   = new List<RoomData>();
        var unchanged = new List<RoomData>();

        foreach (var kvp in newByStableId)
        {
            if (_prevRoomsByStableId.ContainsKey(kvp.Key))
                unchanged.Add(kvp.Value);
            else
                added.Add(kvp.Value);
        }
        foreach (var kvp in _prevRoomsByStableId)
        {
            if (!newByStableId.ContainsKey(kvp.Key))
                removed.Add(kvp.Value);
        }

        _prevRoomsByStableId = newByStableId;

        OnRoomsRecalculated?.Invoke(new RoomsRecalculatedArgs
        {
            Added     = added,
            Removed   = removed,
            Unchanged = unchanged,
        });

        if (enableDebug)
        {
            Debug.Log($"[GridRoomManager] Recalculated: {activeRooms.Count} room(s) detected. +{added.Count} -{removed.Count} ={unchanged.Count}");
            for (int i = 0; i < activeRooms.Count; i++)
                Debug.Log($"  Room {activeRooms[i].RoomId} (stable {activeRooms[i].StableId}): {activeRooms[i].CellCount} cells");
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

    /// <summary>
    /// Returns the <see cref="RoomData"/> with the given ID, or null if not found.
    /// </summary>
    public RoomData GetRoomById(int roomId)
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

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Furniture Tag Tracking ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Full rescan: clears and rebuilds tag counts for every active room using
    /// all furniture currently in <see cref="BuildGrid3D.AllPlaced"/>.
    /// Called after every full room recalculation.
    /// </summary>
    private void ScanFurnitureTagsForAllRooms()
    {
        for (int i = 0; i < activeRooms.Count; i++)
            activeRooms[i].ClearTagCounts();

        if (grid == null) return;

        foreach (var kvp in grid.AllPlaced)
        {
            PlacedBuildableData data = kvp.Value;
            if (data.Property.furnitureTags == FurnitureTag.None) continue;

            RoomData room = GetRoomIfAllCellsInSameRoom(data);
            if (room != null)
                room.AddFurnitureTags(data.Property.furnitureTags);
        }

        // Notify all rooms (NPC system may subscribe per-room)
        for (int i = 0; i < activeRooms.Count; i++)
            OnRoomFurnitureChanged?.Invoke(activeRooms[i]);
    }

    private void HandleFurniturePlaced(PlacedBuildableData data)
    {
        if (data == null || data.Property.furnitureTags == FurnitureTag.None) return;

        RoomData room = GetRoomIfAllCellsInSameRoom(data);
        if (room == null) return;

        room.AddFurnitureTags(data.Property.furnitureTags);
        OnRoomFurnitureChanged?.Invoke(room);
    }

    private void HandleFurnitureRemoved(PlacedBuildableData data)
    {
        if (data == null || data.Property.furnitureTags == FurnitureTag.None) return;

        RoomData room = GetRoomIfAllCellsInSameRoom(data);
        if (room == null) return;

        room.RemoveFurnitureTags(data.Property.furnitureTags);
        OnRoomFurnitureChanged?.Invoke(room);
    }

    /// <summary>
    /// Returns the room that contains ALL occupancy cells of <paramref name="data"/>,
    /// only when every cell maps to the exact same room. Returns null otherwise.
    /// </summary>
    private RoomData GetRoomIfAllCellsInSameRoom(PlacedBuildableData data)
    {
        ResolvedOccupancyCell[] occ = data.Property.GetRotatedOccupancyCells(data.RotationStep);
        RoomData foundRoom = null;
        HashSet<Vector3Int> checkedCells = new HashSet<Vector3Int>();

        for (int i = 0; i < occ.Length; i++)
        {
            Vector3Int worldCell = data.AnchorCell + occ[i].Cell;
            if (!checkedCells.Add(worldCell)) continue;  // skip duplicate cell positions

            if (!cellToRoom.TryGetValue(worldCell, out RoomData room))
                return null;  // cell not inside any room

            if (foundRoom == null)
                foundRoom = room;
            else if (foundRoom != room)
                return null;  // cells span two different rooms
        }

        return foundRoom;  // null when occ is empty
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
