using JackyUtility;
using UnityEngine;

public class FlightManager : MonoBehaviour, IDebuggable
{
    public static FlightManager Instance { get; private set; }

    [Header("Map")]
    [SerializeField] private Key_MapDataPP currentMapKey = Key_MapDataPP.None;
    [SerializeField, Min(1)] private int maxRouteNodeCount = 1;
    [SerializeField] private bool refreshMapOnStart = true;

    [Header("Time")]
    [SerializeField, Min(1)] private int timePerGrid = 1;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled;

    private MapNodeDatabase mapNodeDatabase;
    private MapDataDatabase mapDataDatabase;
    private MapDataRuntime currentMap;
    private FlightInfo flightInfo;
    private FlightTimeCalculator timeCalculator;

    public string DebugId => "flightmanager";
    public bool DebugEnabled { get => debugEnabled; set => debugEnabled = value; }

    public Key_MapDataPP CurrentMapKey => currentMapKey;
    public int MaxRouteNodeCount => maxRouteNodeCount;
    public int EffectiveMaxRouteNodeCount => currentMap != null
        ? Mathf.Min(maxRouteNodeCount, currentMap.NodeCount)
        : maxRouteNodeCount;
    public int TimePerGrid => timePerGrid;
    public MapDataRuntime CurrentMap => currentMap;
    public FlightInfo FlightInfo => flightInfo;
    public FlightState State => flightInfo != null ? flightInfo.State : FlightState.Planning;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        flightInfo = new FlightInfo();
        timeCalculator = new FlightTimeCalculator(timePerGrid);
    }

    private void Start()
    {
        ResolveDatabases();

        if (refreshMapOnStart)
            RefreshMap();
    }

    private void OnValidate()
    {
        if (maxRouteNodeCount < 1)
            maxRouteNodeCount = 1;

        if (timePerGrid < 1)
            timePerGrid = 1;
    }

    public void SetCurrentMapKey(Key_MapDataPP mapKey)
    {
        currentMapKey = mapKey;
    }

    public bool RefreshMap()
    {
        ResolveDatabases();

        if (flightInfo == null)
            flightInfo = new FlightInfo();

        if (currentMapKey == Key_MapDataPP.None)
        {
            currentMap = null;
            flightInfo.ResetForNewMap();
            DebugLog("No current map key selected. Runtime map cleared.");
            return false;
        }

        if (mapDataDatabase == null)
        {
            Debug.LogWarning("[FlightManager] MapDataDatabase not found.");
            return false;
        }

        MapDataProperty mapProperty = mapDataDatabase.GetByEnum(currentMapKey);
        if (mapProperty == null)
        {
            currentMap = null;
            flightInfo.ResetForNewMap();
            Debug.LogWarning($"[FlightManager] No MapDataProperty found for key {currentMapKey}.");
            return false;
        }

        currentMap = mapProperty.CreateRuntimeMapData(mapNodeDatabase);
        flightInfo.ResetForNewMap();

        DebugLog($"Runtime map refreshed: {currentMapKey}, nodes: {currentMap.NodeCount}.");
        return true;
    }

    public bool TryAddNodeToRoute(int runtimeId, out string failReason)
    {
        failReason = string.Empty;

        if (currentMap == null)
        {
            failReason = "Runtime map is null.";
            return false;
        }

        if (flightInfo == null)
        {
            failReason = "FlightInfo is null.";
            return false;
        }

        if (flightInfo.RouteNodes.Count >= EffectiveMaxRouteNodeCount)
        {
            failReason = $"Route is full ({EffectiveMaxRouteNodeCount}).";
            return false;
        }

        if (!currentMap.TryGetNodeByRuntimeId(runtimeId, out MapNodeRuntime node))
        {
            failReason = $"Runtime node {runtimeId} not found.";
            return false;
        }

        bool result = flightInfo.TryAddNode(node, out failReason);
        if (result)
            DebugLog($"Added node {runtimeId} to route.");

        return result;
    }

    public bool RemoveLastRouteNode(out string failReason)
    {
        if (flightInfo == null)
        {
            failReason = "FlightInfo is null.";
            return false;
        }

        return flightInfo.RemoveLastNode(out failReason);
    }

    public bool ClearRoute(out string failReason)
    {
        if (flightInfo == null)
        {
            failReason = "FlightInfo is null.";
            return false;
        }

        return flightInfo.ClearRoute(out failReason);
    }

    public bool CanConfirmRoute(out string failReason)
    {
        if (flightInfo == null)
        {
            failReason = "FlightInfo is null.";
            return false;
        }

        return flightInfo.CanConfirmRoute(out failReason);
    }

    public bool StartFlight(out string failReason)
    {
        if (flightInfo == null)
        {
            failReason = "FlightInfo is null.";
            return false;
        }

        bool result = flightInfo.StartFlight(out failReason);
        if (result)
            DebugLog("Flight started.");

        return result;
    }

    public bool ArriveCurrentTarget(out string failReason)
    {
        if (flightInfo == null)
        {
            failReason = "FlightInfo is null.";
            return false;
        }

        bool result = flightInfo.ArriveCurrentTarget(out failReason);
        if (result)
            DebugLog("Arrived current target.");

        return result;
    }

    public bool ProceedToNextNode(out string failReason)
    {
        if (flightInfo == null)
        {
            failReason = "FlightInfo is null.";
            return false;
        }

        bool result = flightInfo.ProceedToNextNode(out failReason);
        if (result)
            DebugLog($"Proceed result state: {flightInfo.State}.");

        return result;
    }

    public FlightNodeState GetNodeState(int runtimeId)
    {
        if (currentMap == null || flightInfo == null)
            return FlightNodeState.Unplanned;

        MapNodeRuntime node = currentMap.GetNodeByRuntimeId(runtimeId);
        return flightInfo.GetNodeState(node);
    }

    public int GetCurrentSegmentTimeUnits()
    {
        if (flightInfo == null)
            return 0;

        EnsureTimeCalculator();
        return timeCalculator.CalculateTimeUnits(
            flightInfo.GetCurrentSegmentStartPosition(),
            flightInfo.GetCurrentSegmentTargetPosition());
    }

    public int CalculateTimeUnits(Vector2Int from, Vector2Int to)
    {
        EnsureTimeCalculator();
        return timeCalculator.CalculateTimeUnits(from, to);
    }

    private void ResolveDatabases()
    {
        var dbManager = PropertyDatabaseManager.Instance;
        if (dbManager == null)
            return;

        if (mapNodeDatabase == null)
            mapNodeDatabase = dbManager.GetDatabase<MapNodeDatabase>();

        if (mapDataDatabase == null)
            mapDataDatabase = dbManager.GetDatabase<MapDataDatabase>();
    }

    private void EnsureTimeCalculator()
    {
        if (timeCalculator == null)
            timeCalculator = new FlightTimeCalculator(timePerGrid);

        timeCalculator.TimePerGrid = timePerGrid;
    }

    private void DebugLog(string message)
    {
        if (debugEnabled)
            Debug.Log($"[FlightManager] {message}");
    }
}
