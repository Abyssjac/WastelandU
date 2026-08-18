using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

public class FlightManager : MonoBehaviour, IDebuggable, IGeneralPanelOwner
{
    public static FlightManager Instance { get; private set; }

    [Header("Map")]
    [SerializeField] private Key_MapDataPP currentMapKey = Key_MapDataPP.None;
    [Tooltip("Used only when no Flight save exists or when a map is explicitly refreshed.")]
    [SerializeField] private Key_MapNodePP initialIslandKey = Key_MapNodePP.MainIsland_Home;
    [SerializeField, Min(1)] private int maxRouteNodeCount = 1;
    [SerializeField] private bool refreshMapOnStart = true;

    [Header("Time")]
    [FormerlySerializedAs("timePerGrid")]
    [SerializeField, Min(1)] private int timePerTravelUnit = 1;
    [Tooltip("How many authored MapContent UI units equal one logical travel unit.")]
    [SerializeField, Min(0.0001f)] private float uiUnitsPerTravelUnit = 200f;
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
    private Key_MapNodePP currentIslandKey = Key_MapNodePP.None;
    private bool hasRestoredSave;

    public string DebugId => "flightmanager";
    public bool DebugEnabled { get => debugEnabled; set => debugEnabled = value; }

    public Key_MapDataPP CurrentMapKey => currentMapKey;
    public int MaxRouteNodeCount => maxRouteNodeCount;
    public int EffectiveMaxRouteNodeCount => currentMap != null
        ? Mathf.Min(maxRouteNodeCount, currentMap.NodeCount)
        : maxRouteNodeCount;
    public Key_MapNodePP InitialIslandKey => initialIslandKey;
    public int TimePerTravelUnit => timePerTravelUnit;
    public float UiUnitsPerTravelUnit => uiUnitsPerTravelUnit;
    public int GameTimePerProgressUnit => gameTimePerProgressUnit;
    public MapDataRuntime CurrentMap => currentMap;
    public FlightInfo FlightInfo => flightInfo;
    public FlightState State => flightInfo != null ? flightInfo.State : FlightState.Planning;
    public int CurrentSegmentElapsedTime => flightTime != null ? flightTime.ElapsedTimeUnits : 0;
    public int CurrentSegmentTotalTime => flightTime != null ? flightTime.TotalTimeUnits : 0;
    public float SegmentProgress01 => flightTime != null ? flightTime.Progress01 : 0f;
    public Vector2Int CurrentMapPosition => flightInfo != null ? flightInfo.CurrentPosition : Vector2Int.zero;
    /// <summary>
    /// Authoritative island key for the player's docked location. It is <see cref="Key_MapNodePP.None"/>
    /// only while travelling or when no valid initial island exists.
    /// </summary>
    public Key_MapNodePP CurrentIslandKey => currentIslandKey;

    /// <summary>Fired whenever the authoritative current island changes, including changes to None while flying.</summary>
    public event Action<Key_MapNodePP> OnCurrentIslandChanged;

    /// <summary>
    /// Fired after a segment has reached an island and the docked location has been recorded.
    /// This is intentionally independent from <see cref="State"/>, because a final arrival can
    /// immediately return to route planning while its docked presentation remains active.
    /// </summary>
    public event Action<MapNodeRuntime> OnIslandDocked;

    /// <summary>Fired before the runtime map/flight data is replaced, so dependent presentation can clear stale data.</summary>
    public event Action OnFlightRuntimeReset;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        flightInfo = new FlightInfo();
        flightTime = new FlightTimeController(timePerTravelUnit);

        if (openMapButton != null)
            openMapButton.onClick.AddListener(RequestOpenPanel);
    }

    private void Start()
    {
        ResolveDatabases();

        if (refreshMapOnStart && !hasRestoredSave)
            RefreshMap();
    }

    private void OnValidate()
    {
        if (maxRouteNodeCount < 1)
            maxRouteNodeCount = 1;

        if (timePerTravelUnit < 1)
            timePerTravelUnit = 1;

        if (uiUnitsPerTravelUnit < 0.0001f)
            uiUnitsPerTravelUnit = 0.0001f;

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
        OnFlightRuntimeReset?.Invoke();
        hasRestoredSave = false;
        ResolveDatabases();

        if (flightInfo == null)
            flightInfo = new FlightInfo();

        if (currentMapKey == Key_MapDataPP.None)
        {
            currentMap = null;
            flightInfo.ResetForNewMap(null);
            ResetSegmentProgress();
            SetCurrentIslandKey(Key_MapNodePP.None);
            DebugLog("No current map key selected. Runtime map cleared.");
            return false;
        }

        if (mapDataDatabase == null)
        {
            SetCurrentIslandKey(Key_MapNodePP.None);
            Debug.LogWarning("[FlightManager] MapDataDatabase not found.");
            return false;
        }

        MapDataProperty mapProperty = mapDataDatabase.GetByEnum(currentMapKey);
        if (mapProperty == null)
        {
            currentMap = null;
            flightInfo.ResetForNewMap(null);
            ResetSegmentProgress();
            SetCurrentIslandKey(Key_MapNodePP.None);
            Debug.LogWarning($"[FlightManager] No MapDataProperty found for key {currentMapKey}.");
            return false;
        }

        currentMap = mapProperty.CreateRuntimeMapData(mapNodeDatabase);
        MapNodeRuntime initialNode = FindNodeByKey(initialIslandKey);
        if (initialNode == null)
            Debug.LogWarning($"[FlightManager] Initial island '{initialIslandKey}' does not exist on map '{currentMapKey}'.", this);

        flightInfo.ResetForNewMap(initialNode);
        ResetSegmentProgress();
        SetCurrentIslandKey(initialNode != null ? initialNode.NodeKey : Key_MapNodePP.None);

        DebugLog($"Runtime map refreshed: {currentMapKey}, nodes: {currentMap.NodeCount}.");
        return true;
    }

    /// <summary>Restores the authored default map and places the player at <see cref="InitialIslandKey"/>.</summary>
    public void RestoreDefaultState()
    {
        hasRestoredSave = false;
        RefreshMap();
    }

    /// <summary>Captures only runtime flight progress. Authored map data is rebuilt from MapDataPP on load.</summary>
    public FlightSaveData CaptureSaveData()
    {
        FlightSaveData data = new FlightSaveData
        {
            currentMapKey = currentMapKey,
            currentIslandKey = currentIslandKey,
            state = State,
            currentMapPosition = CurrentMapPosition,
            currentRouteIndex = flightInfo != null ? flightInfo.CurrentRouteIndex : 0,
            segmentProgress01 = State == FlightState.Flying ? SegmentProgress01 : 0f,
        };

        if (flightInfo != null)
        {
            for (int i = 0; i < flightInfo.RouteNodes.Count; i++)
            {
                MapNodeRuntime node = flightInfo.RouteNodes[i];
                if (node != null && node.NodeKey != Key_MapNodePP.None)
                    data.routeNodeKeys.Add(node.NodeKey);
            }
        }

        return data;
    }

    /// <summary>Restores a saved fixed-map flight state, including an in-progress route segment.</summary>
    public bool RestoreFromSaveData(FlightSaveData data)
    {
        if (data == null || data.currentMapKey == Key_MapDataPP.None)
        {
            Debug.LogWarning("[FlightManager] Flight save does not contain a valid map key.", this);
            return false;
        }

        ResolveDatabases();
        if (mapDataDatabase == null)
        {
            Debug.LogWarning("[FlightManager] MapDataDatabase is unavailable during Flight restore.", this);
            return false;
        }

        MapDataProperty mapProperty = mapDataDatabase.GetByEnum(data.currentMapKey);
        if (mapProperty == null)
        {
            Debug.LogWarning($"[FlightManager] Saved map '{data.currentMapKey}' no longer exists.", this);
            return false;
        }

        OnFlightRuntimeReset?.Invoke();
        currentMapKey = data.currentMapKey;
        currentMap = mapProperty.CreateRuntimeMapData(mapNodeDatabase);

        var savedRouteNodes = new List<MapNodeRuntime>();
        if (data.routeNodeKeys != null)
        {
            for (int i = 0; i < data.routeNodeKeys.Count; i++)
            {
                Key_MapNodePP routeKey = data.routeNodeKeys[i];
                MapNodeRuntime routeNode = FindNodeByKey(routeKey);
                if (routeNode == null)
                {
                    Debug.LogWarning($"[FlightManager] Saved route node '{routeKey}' does not exist on map '{currentMapKey}'.", this);
                    return false;
                }

                savedRouteNodes.Add(routeNode);
            }
        }

        MapNodeRuntime savedCurrentNode = data.state == FlightState.Flying
            ? null
            : FindNodeByKey(data.currentIslandKey);

        if (data.state != FlightState.Flying && savedCurrentNode == null)
        {
            Debug.LogWarning($"[FlightManager] Saved current island '{data.currentIslandKey}' does not exist on map '{currentMapKey}'.", this);
            return false;
        }

        if (flightInfo == null)
            flightInfo = new FlightInfo();

        if (!flightInfo.RestoreFromSave(
                data.currentMapPosition,
                savedCurrentNode,
                savedRouteNodes,
                data.currentRouteIndex,
                data.state,
                out string failReason))
        {
            Debug.LogWarning($"[FlightManager] Flight restore failed: {failReason}", this);
            return false;
        }

        ResetSegmentProgress();
        SetCurrentIslandKey(data.state == FlightState.Flying ? Key_MapNodePP.None : data.currentIslandKey);

        if (data.state == FlightState.Flying)
            StartCurrentSegmentTimer(data.segmentProgress01);

        hasRestoredSave = true;
        DebugLog($"Flight save restored: map={currentMapKey}, state={State}, island={currentIslandKey}.");
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

    /// <summary>
    /// Clears a completed route and returns to planning without moving the player away from
    /// their current island. A route cannot be replaced while the player is travelling.
    /// </summary>
    public bool BeginNewRoutePlanning(out string failReason)
    {
        if (flightInfo == null)
        {
            failReason = "FlightInfo is null.";
            return false;
        }

        bool result = flightInfo.BeginNewRoutePlanning(out failReason);
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
            SetCurrentIslandKey(Key_MapNodePP.None);
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
            MapNodeRuntime arrivedNode = GetCurrentArrivedNode();
            SetCurrentIslandKey(arrivedNode != null ? arrivedNode.NodeKey : Key_MapNodePP.None);

            // Notify presentation and island-entry systems before a final arrival returns to
            // Planning. Docking is a location fact, not an Arrived-state-only fact.
            if (arrivedNode != null)
                OnIslandDocked?.Invoke(arrivedNode);

            // Once the final destination is reached, return to an empty planning state at
            // the island just reached. The next route therefore begins from that island.
            if (!flightInfo.HasNextRouteNode)
                BeginNewRoutePlanning(out _);

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
            SetCurrentIslandKey(Key_MapNodePP.None);
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

    public MapNodeRuntime GetCurrentArrivedNode()
    {
        return State == FlightState.Arrived && flightInfo != null
            ? flightInfo.CurrentNode
            : null;
    }

    /// <summary>Returns the island where the player is currently docked, or null while flying.</summary>
    public MapNodeRuntime GetCurrentLocationNode()
    {
        return flightInfo != null && State != FlightState.Flying
            ? flightInfo.CurrentNode
            : null;
    }

    /// <summary>Attempts to resolve the runtime map node represented by <see cref="CurrentIslandKey"/>.</summary>
    public bool TryGetCurrentIslandNode(out MapNodeRuntime node)
    {
        node = null;
        if (currentIslandKey == Key_MapNodePP.None || currentMap == null)
            return false;

        for (int i = 0; i < currentMap.Nodes.Count; i++)
        {
            MapNodeRuntime candidate = currentMap.Nodes[i];
            if (candidate != null && candidate.NodeKey == currentIslandKey)
            {
                node = candidate;
                return true;
            }
        }

        return false;
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

        return CalculateTimeUnits(
            flightInfo.GetCurrentSegmentStartPosition(),
            flightInfo.GetCurrentSegmentTargetPosition());
    }

    public int CalculateTimeUnits(Vector2Int from, Vector2Int to)
    {
        EnsureFlightTimeController();
        return flightTime.CalculateTimeUnits(CalculateRouteDistance(from, to));
    }

    /// <summary>Converts authored MapContent coordinates into logical travel units.</summary>
    public float CalculateRouteDistance(Vector2Int from, Vector2Int to)
    {
        return Vector2Int.Distance(from, to) / Mathf.Max(0.0001f, uiUnitsPerTravelUnit);
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

    private MapNodeRuntime FindNodeByKey(Key_MapNodePP nodeKey)
    {
        if (nodeKey == Key_MapNodePP.None || currentMap == null)
            return null;

        for (int i = 0; i < currentMap.Nodes.Count; i++)
        {
            MapNodeRuntime node = currentMap.Nodes[i];
            if (node != null && node.NodeKey == nodeKey)
                return node;
        }

        return null;
    }

    private void SetCurrentIslandKey(Key_MapNodePP newIslandKey)
    {
        if (currentIslandKey == newIslandKey)
            return;

        currentIslandKey = newIslandKey;
        OnCurrentIslandChanged?.Invoke(currentIslandKey);
        DebugLog($"Current island changed to {currentIslandKey}.");
    }

    private void EnsureFlightTimeController()
    {
        if (flightTime == null)
            flightTime = new FlightTimeController(timePerTravelUnit);

        flightTime.TimePerTravelUnit = timePerTravelUnit;
    }

    private void StartCurrentSegmentTimer(float progress01 = 0f)
    {
        if (flightInfo == null)
            return;

        EnsureFlightTimeController();
        flightTime.StartSegment(
            CalculateRouteDistance(
                flightInfo.GetCurrentSegmentStartPosition(),
                flightInfo.GetCurrentSegmentTargetPosition()),
            progress01);
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
        if (flightTime.TotalTimeUnits > 0 &&
            !flightTime.IsFinished &&
            flightTime.Progress01 < 1f)
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
