using JackyUtility;
using UnityEngine;
using UnityEngine.UI;

public class FlightManager : MonoBehaviour, IDebuggable, IGeneralPanelOwner
{
    public static FlightManager Instance { get; private set; }

    [Header("Map")]
    [SerializeField] private Key_MapDataPP currentMapKey = Key_MapDataPP.None;
    [SerializeField, Min(1)] private int maxRouteNodeCount = 1;
    [SerializeField] private bool refreshMapOnStart = true;

    [Header("Time")]
    [SerializeField, Min(1)] private int timePerGrid = 1;
    [SerializeField, Min(1)] private int gameTimePerProgressUnit = 1;

    [Header("UI")]
    [SerializeField] private FlightMapPanelUI mapPanelUI;
    [SerializeField] private FlightRoutePlanningPanelOwner routePlanningOwner;
    [SerializeField] private Button openMapButton;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled;

    private MapNodeDatabase mapNodeDatabase;
    private MapDataDatabase mapDataDatabase;
    private MapDataRuntime currentMap;
    private FlightInfo flightInfo;
    private FlightTimeController flightTime;

    public string DebugId => "flightmanager";
    public bool DebugEnabled { get => debugEnabled; set => debugEnabled = value; }

    public Key_MapDataPP CurrentMapKey => currentMapKey;
    public int MaxRouteNodeCount => maxRouteNodeCount;
    public int EffectiveMaxRouteNodeCount => currentMap != null
        ? Mathf.Min(maxRouteNodeCount, currentMap.NodeCount)
        : maxRouteNodeCount;
    public int TimePerGrid => timePerGrid;
    public int GameTimePerProgressUnit => gameTimePerProgressUnit;
    public MapDataRuntime CurrentMap => currentMap;
    public FlightInfo FlightInfo => flightInfo;
    public FlightState State => flightInfo != null ? flightInfo.State : FlightState.Planning;
    public int CurrentSegmentElapsedTime => flightTime != null ? flightTime.ElapsedTimeUnits : 0;
    public int CurrentSegmentTotalTime => flightTime != null ? flightTime.TotalTimeUnits : 0;
    public float SegmentProgress01 => flightTime != null ? flightTime.Progress01 : 0f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        flightInfo = new FlightInfo();
        flightTime = new FlightTimeController(timePerGrid);

        if (openMapButton != null)
            openMapButton.onClick.AddListener(RequestOpenPanel);
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

        if (gameTimePerProgressUnit < 1)
            gameTimePerProgressUnit = 1;

    }

    private void Update()
    {
        AdvanceTravelByGameTime(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (openMapButton != null)
            openMapButton.onClick.RemoveListener(RequestOpenPanel);
    }

    public void OnPanelOpenRequested()
    {
        if (currentMap == null)
            RefreshMap();

        mapPanelUI?.OpenPanel();
    }

    public void OnPanelCloseRequested()
    {
        if (routePlanningOwner != null && routePlanningOwner.IsRoutePlanningOpen)
            routePlanningOwner.OnPanelCloseRequested();

        mapPanelUI?.ClosePanel();
    }

    public void RequestOpenPanel()
    {
        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestOpen(this, PanelOpenType.Override);
        else
            OnPanelOpenRequested();
    }

    public void RequestClosePanel()
    {
        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestClose(this);
        else
            OnPanelCloseRequested();
    }

    public void RequestOpenRoutePlanning()
    {
        if (routePlanningOwner == null || State != FlightState.Planning)
            return;

        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestOpen(routePlanningOwner, PanelOpenType.Overlay);
        else
            routePlanningOwner.OnPanelOpenRequested();
    }

    public void RequestCloseRoutePlanning()
    {
        if (routePlanningOwner == null)
            return;

        if (AllUIManager.Instance != null && AllUIManager.Instance.IsTopPanel(routePlanningOwner))
            AllUIManager.Instance.RequestClose(routePlanningOwner);
        else
            routePlanningOwner.OnPanelCloseRequested();
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
            ResetSegmentProgress();
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
            ResetSegmentProgress();
            Debug.LogWarning($"[FlightManager] No MapDataProperty found for key {currentMapKey}.");
            return false;
        }

        currentMap = mapProperty.CreateRuntimeMapData(mapNodeDatabase);
        flightInfo.ResetForNewMap();
        ResetSegmentProgress();

        DebugLog($"Runtime map refreshed: {currentMapKey}, nodes: {currentMap.NodeCount}.");
        return true;
    }

    public bool CanAddNodeToRoute(int runtimeId, out string failReason)
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

        return flightInfo.CanAddNode(node, out failReason);
    }

    public bool TryAddNodeToRoute(int runtimeId, out string failReason)
    {
        if (!CanAddNodeToRoute(runtimeId, out failReason))
            return false;

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

        bool result = flightInfo.RemoveLastNode(out failReason);
        if (result)
            ResetSegmentProgress();

        return result;
    }

    public bool ClearRoute(out string failReason)
    {
        if (flightInfo == null)
        {
            failReason = "FlightInfo is null.";
            return false;
        }

        bool result = flightInfo.ClearRoute(out failReason);
        if (result)
            ResetSegmentProgress();

        return result;
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
        {
            StartCurrentSegmentTimer();
            DebugLog("Flight started.");
        }

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
        {
            CompleteSegmentProgress();
            DebugLog("Arrived current target.");
        }

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
        {
            ResetSegmentProgress();
            if (flightInfo.State == FlightState.Flying)
                StartCurrentSegmentTimer();

            DebugLog($"Proceed result state: {flightInfo.State}.");
        }

        return result;
    }

    public bool IsNodeInRoute(int runtimeId)
    {
        if (flightInfo == null)
            return false;

        for (int i = 0; i < flightInfo.RouteNodes.Count; i++)
        {
            MapNodeRuntime node = flightInfo.RouteNodes[i];
            if (node != null && node.RuntimeId == runtimeId)
                return true;
        }

        return false;
    }

    public bool IsLastRouteNode(int runtimeId)
    {
        if (flightInfo == null || flightInfo.RouteNodes.Count == 0)
            return false;

        MapNodeRuntime last = flightInfo.RouteNodes[flightInfo.RouteNodes.Count - 1];
        return last != null && last.RuntimeId == runtimeId;
    }

    public MapNodeRuntime GetRouteTailNode()
    {
        if (flightInfo == null || flightInfo.RouteNodes.Count == 0)
            return null;

        return flightInfo.RouteNodes[flightInfo.RouteNodes.Count - 1];
    }

    public MapNodeRuntime GetCurrentTargetNode()
    {
        return flightInfo != null ? flightInfo.GetCurrentTarget() : null;
    }

    public bool HasNextRouteNode()
    {
        return flightInfo != null && flightInfo.HasNextRouteNode;
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

        EnsureFlightTimeController();
        return flightTime.CalculateTimeUnits(
            flightInfo.GetCurrentSegmentStartPosition(),
            flightInfo.GetCurrentSegmentTargetPosition());
    }

    public int CalculateTimeUnits(Vector2Int from, Vector2Int to)
    {
        EnsureFlightTimeController();
        return flightTime.CalculateTimeUnits(from, to);
    }

    public float CalculateRouteDistance(Vector2Int from, Vector2Int to)
    {
        EnsureFlightTimeController();
        return flightTime.GetEuclideanDistance(from, to);
    }

    public void AdvanceTravelByGameTime(float gameTimeAmount)
    {
        if (flightInfo == null || flightInfo.State != FlightState.Flying)
            return;

        EnsureFlightTimeController();
        float requiredGameTime = Mathf.Max(0.0001f, gameTimePerProgressUnit);
        flightTime.Advance(Mathf.Max(0f, gameTimeAmount) / requiredGameTime);
        TryAutoArriveWhenSegmentFinished();
    }

    public void AdvanceSegmentProgress(int timeUnits)
    {
        if (flightInfo == null || flightInfo.State != FlightState.Flying)
            return;

        EnsureSegmentTimerReady();
        flightTime.Advance(timeUnits);
        TryAutoArriveWhenSegmentFinished();
    }

    public void CompleteSegmentProgress()
    {
        EnsureSegmentTimerReady();
        flightTime.Complete();
        TryAutoArriveWhenSegmentFinished();
    }

    public void ResetSegmentProgress()
    {
        EnsureFlightTimeController();
        flightTime.Reset();
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

    private void EnsureFlightTimeController()
    {
        if (flightTime == null)
            flightTime = new FlightTimeController(timePerGrid);

        flightTime.TimePerGrid = timePerGrid;
    }

    private void StartCurrentSegmentTimer()
    {
        if (flightInfo == null)
            return;

        EnsureFlightTimeController();
        flightTime.StartSegment(
            flightInfo.GetCurrentSegmentStartPosition(),
            flightInfo.GetCurrentSegmentTargetPosition());
        TryAutoArriveWhenSegmentFinished();
    }

    private void EnsureSegmentTimerReady()
    {
        EnsureFlightTimeController();

        if (flightTime.TotalTimeUnits <= 0 && flightInfo != null && flightInfo.State == FlightState.Flying)
            StartCurrentSegmentTimer();
    }

    private bool TryAutoArriveWhenSegmentFinished()
    {
        if (flightInfo == null || flightInfo.State != FlightState.Flying)
            return false;

        EnsureFlightTimeController();
        if (!flightTime.IsFinished)
            return false;

        flightTime.Stop();
        return ArriveCurrentTarget(out _);
    }

    private void DebugLog(string message)
    {
        if (debugEnabled)
            Debug.Log($"[FlightManager] {message}");
    }

}
