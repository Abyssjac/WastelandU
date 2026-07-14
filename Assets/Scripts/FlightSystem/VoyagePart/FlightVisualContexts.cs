using UnityEngine;

public sealed class FlightVisualSegmentContext
{
    public FlightVisualSegmentContext(
        Vector2Int startGridPosition,
        Vector2Int targetGridPosition,
        MapNodeRuntime targetNode,
        int routeIndex,
        float mapDistance,
        float visualDistance,
        Vector3 forward,
        Vector3 right,
        int randomSeed)
    {
        StartGridPosition = startGridPosition;
        TargetGridPosition = targetGridPosition;
        TargetNode = targetNode;
        RouteIndex = routeIndex;
        MapDistance = mapDistance;
        VisualDistance = visualDistance;
        Forward = forward;
        Right = right;
        FlowDirection = -forward;
        RandomSeed = randomSeed;
    }

    public Vector2Int StartGridPosition { get; }
    public Vector2Int TargetGridPosition { get; }
    public MapNodeRuntime TargetNode { get; }
    public int RouteIndex { get; }
    public float MapDistance { get; }
    public float VisualDistance { get; }
    public Vector3 Forward { get; }
    public Vector3 Right { get; }
    public Vector3 FlowDirection { get; }
    public int RandomSeed { get; }
}

public struct FlightVisualFrameContext
{
    public FlightVisualFrameContext(
        Vector3 shipAnchorPosition,
        Vector3 forward,
        Vector3 right,
        float scrollDelta,
        float scrollSpeed,
        float logicalProgress01,
        float visualProgress01,
        float remainingVisualDistance,
        float deltaTime,
        bool allowsAmbientSpawning)
    {
        ShipAnchorPosition = shipAnchorPosition;
        Forward = forward;
        Right = right;
        FlowDirection = -forward;
        ScrollDelta = Mathf.Max(0f, scrollDelta);
        ScrollSpeed = Mathf.Max(0f, scrollSpeed);
        LogicalProgress01 = Mathf.Clamp01(logicalProgress01);
        VisualProgress01 = Mathf.Clamp01(visualProgress01);
        RemainingVisualDistance = Mathf.Max(0f, remainingVisualDistance);
        DeltaTime = Mathf.Max(0f, deltaTime);
        AllowsAmbientSpawning = allowsAmbientSpawning;
    }

    public Vector3 ShipAnchorPosition { get; }
    public Vector3 Forward { get; }
    public Vector3 Right { get; }
    public Vector3 FlowDirection { get; }
    public float ScrollDelta { get; }
    public float ScrollSpeed { get; }
    public float LogicalProgress01 { get; }
    public float VisualProgress01 { get; }
    public float RemainingVisualDistance { get; }
    public float DeltaTime { get; }
    public bool AllowsAmbientSpawning { get; }
}

public struct FlightAmbientSpawnData
{
    public FlightAmbientSpawnData(Vector3 position, Quaternion rotation, Vector3 scale, int randomSeed)
    {
        Position = position;
        Rotation = rotation;
        Scale = scale;
        RandomSeed = randomSeed;
    }

    public Vector3 Position { get; }
    public Quaternion Rotation { get; }
    public Vector3 Scale { get; }
    public int RandomSeed { get; }
}
