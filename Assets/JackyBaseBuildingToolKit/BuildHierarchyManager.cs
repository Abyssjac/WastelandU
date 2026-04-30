using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the scene GameObject hierarchy for all built objects.
///
/// Maintains this structure:
///   AllBuildInstances
///     AllRooms
///       Room_X_Y_Z          (one per detected room, named after StableId)
///         Walls             (structural items: walls, floors, ceilings)
///         Fur               (furniture items: furnitureTags != None)
///     Others                (everything not assigned to a room)
///
/// Reacts to:
///   - GridRoomManager.OnRoomsRecalculated  ¡ú diff-based room node management + full reassignment
///   - BuildManager.OnFurniturePlaced        ¡ú incremental furniture reparenting
/// </summary>
public class BuildHierarchyManager : MonoBehaviour
{
    public static BuildHierarchyManager Instance { get; private set; }

    // ©¤©¤ Root transforms ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private Transform _allBuildRoot;
    private Transform _allRoomsRoot;
    private Transform _othersRoot;

    // ©¤©¤ Per-room node references ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private struct RoomNodes
    {
        public Transform RoomRoot;
        public Transform WallsRoot;
        public Transform FurRoot;
    }
    private readonly Dictionary<Vector3Int, RoomNodes> _roomNodes =
        new Dictionary<Vector3Int, RoomNodes>();

    // ©¤©¤ Neighbor offsets for wall-attribution ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private static readonly Vector3Int[] s_offsets =
    {
        Vector3Int.right,          Vector3Int.left,
        new Vector3Int(0, 0,  1),  new Vector3Int(0, 0, -1),
        Vector3Int.up,             Vector3Int.down,
    };

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    // Lifecycle
    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Build the three root nodes immediately so BuildManager can use them
        _allBuildRoot = new GameObject("AllBuildInstances").transform;
        _allRoomsRoot = new GameObject("AllRooms").transform;
        _othersRoot   = new GameObject("Others").transform;

        _allRoomsRoot.SetParent(_allBuildRoot, false);
        _othersRoot.SetParent(_allBuildRoot, false);
    }

    private void Start()
    {
        // Tell BuildManager to parent all new objects under AllBuildInstances
        if (BuildManager.Instance != null)
            BuildManager.Instance.BuildablesParent = _allBuildRoot;

        if (GridRoomManager.Instance != null)
            GridRoomManager.Instance.OnRoomsRecalculated += HandleRoomsRecalculated;

        if (BuildManager.Instance != null)
            BuildManager.Instance.OnFurniturePlaced += HandleFurniturePlaced;
    }

    private void OnDestroy()
    {
        if (GridRoomManager.Instance != null)
            GridRoomManager.Instance.OnRoomsRecalculated -= HandleRoomsRecalculated;

        if (BuildManager.Instance != null)
            BuildManager.Instance.OnFurniturePlaced -= HandleFurniturePlaced;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    // Event handlers
    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void HandleRoomsRecalculated(RoomsRecalculatedArgs args)
    {
        // 1. Destroy nodes for rooms that no longer exist
        foreach (var room in args.Removed)
            ProcessRemoved(room);

        // 2. Create nodes for newly detected rooms
        foreach (var room in args.Added)
            CreateRoomNodes(room);

        // 3. Full reassignment pass for structural items across all current rooms
        //    (unchanged rooms keep their Fur node intact; Walls are always re-scanned)
        RebuildStructuralAssignments();

        // 4. For newly added rooms, also assign furniture that was already inside
        foreach (var room in args.Added)
        {
            if (_roomNodes.TryGetValue(room.StableId, out RoomNodes nodes))
                AssignFurnitureToRoom(room, nodes.FurRoot);
        }
    }

    /// <summary>
    /// Fired when a single furniture item is confirmed placed or a move is confirmed.
    /// Moves the item to the correct Room/Fur node or Others.
    /// </summary>
    private void HandleFurniturePlaced(PlacedBuildableData data)
    {
        if (data?.SpawnedObject == null) return;

        RoomData room = GridRoomManager.Instance?.GetRoomAtCell(data.AnchorCell);
        Transform target = GetFurTarget(room);
        data.SpawnedObject.transform.SetParent(target, true);
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    // Room node management
    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void ProcessRemoved(RoomData room)
    {
        if (!_roomNodes.TryGetValue(room.StableId, out RoomNodes nodes)) return;

        MoveChildrenToOthers(nodes.WallsRoot);
        MoveChildrenToOthers(nodes.FurRoot);

        if (nodes.RoomRoot != null)
            Destroy(nodes.RoomRoot.gameObject);

        _roomNodes.Remove(room.StableId);
    }

    private void CreateRoomNodes(RoomData room)
    {
        string name = $"Room_{room.StableId.x}_{room.StableId.y}_{room.StableId.z}";
        var roomRoot  = new GameObject(name).transform;
        var wallsRoot = new GameObject("Walls").transform;
        var furRoot   = new GameObject("Fur").transform;

        roomRoot.SetParent(_allRoomsRoot, false);
        wallsRoot.SetParent(roomRoot, false);
        furRoot.SetParent(roomRoot, false);

        _roomNodes[room.StableId] = new RoomNodes
        {
            RoomRoot  = roomRoot,
            WallsRoot = wallsRoot,
            FurRoot   = furRoot,
        };
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    // Assignment passes
    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Clears all Walls nodes and re-assigns every structural (non-furniture) placed object
    /// to the correct room's Walls node or to Others.
    /// Furniture under Fur nodes is left untouched.
    /// </summary>
    private void RebuildStructuralAssignments()
    {
        // Clear all Walls nodes first
        foreach (var nodes in _roomNodes.Values)
            MoveChildrenToOthers(nodes.WallsRoot);

        if (BuildManager.Instance == null || GridRoomManager.Instance == null) return;

        foreach (var kvp in BuildManager.Instance.Grid.AllPlaced)
        {
            PlacedBuildableData data = kvp.Value;
            if (data.SpawnedObject == null) continue;
            if (data.Property.furnitureTags != FurnitureTag.None) continue; // furniture only via HandleFurniturePlaced

            RoomData room = GetRoomForStructural(data);
            Transform target = room != null && _roomNodes.TryGetValue(room.StableId, out RoomNodes nodes)
                ? nodes.WallsRoot
                : _othersRoot;

            data.SpawnedObject.transform.SetParent(target, true);
        }
    }

    /// <summary>
    /// Assigns furniture items already in the grid that belong to <paramref name="room"/>
    /// to <paramref name="furRoot"/>. Used when a new room is created around existing furniture.
    /// </summary>
    private void AssignFurnitureToRoom(RoomData room, Transform furRoot)
    {
        if (BuildManager.Instance == null || GridRoomManager.Instance == null) return;

        foreach (var kvp in BuildManager.Instance.Grid.AllPlaced)
        {
            PlacedBuildableData data = kvp.Value;
            if (data.SpawnedObject == null) continue;
            if (data.Property.furnitureTags == FurnitureTag.None) continue;

            RoomData belongs = GridRoomManager.Instance.GetRoomAtCell(data.AnchorCell);
            if (belongs == room)
                data.SpawnedObject.transform.SetParent(furRoot, true);
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    // Helpers
    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Determines which room a structural buildable belongs to.
    /// <list type="bullet">
    ///   <item>Floor / ceiling: their occupancy cells are interior cells ¡ú direct room lookup.</item>
    ///   <item>Walls: their occupancy cells sit on room boundaries ¡ú check neighbour cells.</item>
    ///   <item>Shared walls: assigned to the room with the lexicographically smallest StableId.</item>
    /// </list>
    /// </summary>
    private RoomData GetRoomForStructural(PlacedBuildableData data)
    {
        var rm = GridRoomManager.Instance;
        if (rm == null) return null;

        ResolvedOccupancyCell[] occ = data.Property.GetRotatedOccupancyCells(data.RotationStep);
        var checkedCells  = new HashSet<Vector3Int>();
        var neighborRooms = new List<RoomData>();

        for (int i = 0; i < occ.Length; i++)
        {
            Vector3Int worldCell = data.AnchorCell + occ[i].Cell;
            if (!checkedCells.Add(worldCell)) continue;

            // Direct check ¡ª floor / ceiling cells ARE interior cells
            RoomData direct = rm.GetRoomAtCell(worldCell);
            if (direct != null) return direct;

            // Neighbour check ¡ª wall cells sit outside the interior
            foreach (var offset in s_offsets)
            {
                RoomData nr = rm.GetRoomAtCell(worldCell + offset);
                if (nr != null && !neighborRooms.Contains(nr))
                    neighborRooms.Add(nr);
            }
        }

        if (neighborRooms.Count == 0) return null;

        // Shared wall: deterministically pick the room with the smallest StableId
        neighborRooms.Sort((a, b) =>
        {
            int cx = a.StableId.x.CompareTo(b.StableId.x);
            if (cx != 0) return cx;
            int cz = a.StableId.z.CompareTo(b.StableId.z);
            return cz != 0 ? cz : a.StableId.y.CompareTo(b.StableId.y);
        });
        return neighborRooms[0];
    }

    private Transform GetFurTarget(RoomData room)
    {
        if (room != null && _roomNodes.TryGetValue(room.StableId, out RoomNodes nodes))
            return nodes.FurRoot;
        return _othersRoot;
    }

    private void MoveChildrenToOthers(Transform parent)
    {
        if (parent == null) return;

        var children = new List<Transform>(parent.childCount);
        for (int i = 0; i < parent.childCount; i++)
            children.Add(parent.GetChild(i));

        foreach (var child in children)
            child.SetParent(_othersRoot, true);
    }
}
