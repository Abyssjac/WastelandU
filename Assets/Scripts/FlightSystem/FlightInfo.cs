using System.Collections.Generic;
using UnityEngine;

public sealed class FlightInfo
{
    private readonly List<MapNodeRuntime> routeNodes = new List<MapNodeRuntime>();

    public Vector2Int StartPosition { get; } = Vector2Int.zero;
    public Vector2Int CurrentPosition { get; private set; } = Vector2Int.zero;
    public MapNodeRuntime CurrentNode { get; private set; }
    public IReadOnlyList<MapNodeRuntime> RouteNodes => routeNodes;
    public int CurrentRouteIndex { get; private set; }
    public FlightState State { get; private set; } = FlightState.Planning;

    public bool IsPlanning => State == FlightState.Planning;
    public bool HasRoute => routeNodes.Count > 0;
    public bool HasNextRouteNode => State == FlightState.Arrived && CurrentRouteIndex < routeNodes.Count - 1;

    public void ResetForNewMap()
    {
        routeNodes.Clear();
        CurrentRouteIndex = 0;
        CurrentPosition = StartPosition;
        CurrentNode = null;
        State = FlightState.Planning;
    }

    public bool CanAddNode(MapNodeRuntime node, out string failReason)
    {
        if (State != FlightState.Planning)
        {
            failReason = "Can only edit route while planning.";
            return false;
        }

        if (node == null)
        {
            failReason = "Node is null.";
            return false;
        }

        if (ContainsNode(node))
        {
            failReason = $"Node {node.RuntimeId} is already in route.";
            return false;
        }

        failReason = string.Empty;
        return true;
    }

    public bool TryAddNode(MapNodeRuntime node, out string failReason)
    {
        if (!CanAddNode(node, out failReason))
            return false;

        routeNodes.Add(node);
        return true;
    }

    public bool RemoveLastNode(out string failReason)
    {
        if (State != FlightState.Planning)
        {
            failReason = "Can only edit route while planning.";
            return false;
        }

        if (routeNodes.Count == 0)
        {
            failReason = "Route is empty.";
            return false;
        }

        routeNodes.RemoveAt(routeNodes.Count - 1);
        failReason = string.Empty;
        return true;
    }

    public bool ClearRoute(out string failReason)
    {
        if (State != FlightState.Planning)
        {
            failReason = "Can only edit route while planning.";
            return false;
        }

        routeNodes.Clear();
        failReason = string.Empty;
        return true;
    }

    public bool CanConfirmRoute(out string failReason)
    {
        if (State != FlightState.Planning)
        {
            failReason = "Can only start flight while planning.";
            return false;
        }

        if (routeNodes.Count == 0)
        {
            failReason = "Route is empty.";
            return false;
        }

        failReason = string.Empty;
        return true;
    }

    public bool StartFlight(out string failReason)
    {
        if (!CanConfirmRoute(out failReason))
            return false;

        CurrentRouteIndex = 0;
        CurrentNode = null;
        CurrentPosition = StartPosition;
        State = FlightState.Flying;
        return true;
    }

    public bool ArriveCurrentTarget(out string failReason)
    {
        if (State != FlightState.Flying)
        {
            failReason = "Can only arrive while flying.";
            return false;
        }

        MapNodeRuntime target = GetCurrentTarget();
        if (target == null)
        {
            failReason = "No current target.";
            return false;
        }

        CurrentNode = target;
        CurrentPosition = target.GridPosition;
        State = FlightState.Arrived;
        failReason = string.Empty;
        return true;
    }

    public bool ProceedToNextNode(out string failReason)
    {
        if (State != FlightState.Arrived)
        {
            failReason = "Can only proceed after arriving.";
            return false;
        }

        if (!HasNextRouteNode)
        {
            failReason = "No next route node.";
            return false;
        }

        CurrentRouteIndex++;
        State = FlightState.Flying;
        failReason = string.Empty;
        return true;
    }

    public MapNodeRuntime GetCurrentTarget()
    {
        if (routeNodes.Count == 0)
            return null;

        if (CurrentRouteIndex < 0 || CurrentRouteIndex >= routeNodes.Count)
            return null;

        return routeNodes[CurrentRouteIndex];
    }

    public Vector2Int GetRouteTailPosition()
    {
        if (routeNodes.Count == 0)
            return CurrentPosition;

        return routeNodes[routeNodes.Count - 1].GridPosition;
    }

    public Vector2Int GetCurrentSegmentStartPosition()
    {
        if (State != FlightState.Flying)
            return CurrentPosition;

        if (CurrentRouteIndex <= 0)
            return StartPosition;

        return routeNodes[CurrentRouteIndex - 1].GridPosition;
    }

    public Vector2Int GetCurrentSegmentTargetPosition()
    {
        MapNodeRuntime target = GetCurrentTarget();
        return target != null ? target.GridPosition : CurrentPosition;
    }

    public bool ContainsNode(MapNodeRuntime node)
    {
        if (node == null)
            return false;

        for (int i = 0; i < routeNodes.Count; i++)
        {
            if (routeNodes[i] != null && routeNodes[i].RuntimeId == node.RuntimeId)
                return true;
        }

        return false;
    }

    public FlightNodeState GetNodeState(MapNodeRuntime node)
    {
        if (node == null)
            return FlightNodeState.Unplanned;

        if (CurrentNode != null && CurrentNode.RuntimeId == node.RuntimeId)
            return FlightNodeState.CurrentLocation;

        MapNodeRuntime currentTarget = GetCurrentTarget();
        if (State == FlightState.Flying &&
            currentTarget != null &&
            currentTarget.RuntimeId == node.RuntimeId)
            return FlightNodeState.CurrentTarget;

        int routeIndex = IndexOfRouteNode(node);
        if (routeIndex < 0)
            return FlightNodeState.Unplanned;

        if (routeIndex < CurrentRouteIndex)
            return FlightNodeState.Visited;

        return FlightNodeState.Planned;
    }

    private int IndexOfRouteNode(MapNodeRuntime node)
    {
        if (node == null)
            return -1;

        for (int i = 0; i < routeNodes.Count; i++)
        {
            if (routeNodes[i] != null && routeNodes[i].RuntimeId == node.RuntimeId)
                return i;
        }

        return -1;
    }
}
