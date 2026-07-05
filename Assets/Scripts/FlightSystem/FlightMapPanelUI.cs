using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FlightMapPanelUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;

    [Header("References")]
    [SerializeField] private FlightManager flightManager;
    [SerializeField] private FlightMapView mapView;
    [SerializeField] private FlightNodeDetailPanelUI detailPanel;

    [Header("Buttons")]
    [SerializeField] private Button routePlanningButton;
    [SerializeField] private TextMeshProUGUI routePlanningButtonLabel;
    [SerializeField] private string enterPlanningText = "Plan";
    [SerializeField] private string exitPlanningText = "Done";
    [SerializeField] private Button travelButton;
    [SerializeField] private TextMeshProUGUI travelButtonLabel;
    [SerializeField] private string startTravelText = "Sail";
    [SerializeField] private string flyingText = "Sailing";
    [SerializeField] private string proceedText = "Proceed";
    [SerializeField] private string completedText = "Completed";

    [Header("Progress")]
    [SerializeField] private Slider segmentProgressSlider;

    [Header("Route Lines")]
    [SerializeField] private RectTransform routeLineRoot;
    [SerializeField] private Image routeLinePrefab;
    [SerializeField] private Color routeLineColor = Color.white;
    [SerializeField, Min(1f)] private float routeLineThickness = 4f;

    private readonly List<Image> routeLines = new List<Image>();

    public FlightUIMode UIMode { get; private set; } = FlightUIMode.Preview;

    private void Awake()
    {
        if (routePlanningButton != null)
            routePlanningButton.onClick.AddListener(HandleRoutePlanningButtonClicked);

        if (travelButton != null)
            travelButton.onClick.AddListener(HandleTravelButtonClicked);

        if (mapView != null)
            mapView.SetCallbacks(HandleNodeClicked, HandleStartClicked);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (routePlanningButton != null)
            routePlanningButton.onClick.RemoveListener(HandleRoutePlanningButtonClicked);

        if (travelButton != null)
            travelButton.onClick.RemoveListener(HandleTravelButtonClicked);
    }

    private void Update()
    {
        if (panelRoot != null && !panelRoot.activeInHierarchy)
            return;

        RefreshProgress();
        RefreshButtons();
        RefreshNodeStates();
    }

    public void OpenPanel()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        UIMode = FlightUIMode.Preview;
        RenderCurrentMap();
        ShowStartDetail();
        RefreshAll();
    }

    public void ClosePanel()
    {
        UIMode = FlightUIMode.Preview;

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void EnterRoutePlanningMode()
    {
        if (flightManager == null || flightManager.State != FlightState.Planning)
            return;

        UIMode = FlightUIMode.RoutePlanning;
        RefreshAll();
    }

    public void ExitRoutePlanningMode()
    {
        UIMode = FlightUIMode.Preview;
        ShowStartDetail();
        RefreshAll();
    }

    public void RenderCurrentMap()
    {
        if (mapView == null)
            return;

        mapView.Render(flightManager != null ? flightManager.CurrentMap : null);
    }

    public void RefreshAll()
    {
        RefreshNodeStates();
        RefreshRouteLines();
        RefreshProgress();
        RefreshButtons();
    }

    private void HandleRoutePlanningButtonClicked()
    {
        if (flightManager == null)
            return;

        if (UIMode == FlightUIMode.RoutePlanning)
            flightManager.RequestCloseRoutePlanning();
        else
            flightManager.RequestOpenRoutePlanning();
    }

    private void HandleTravelButtonClicked()
    {
        if (flightManager == null)
            return;

        if (flightManager.State == FlightState.Planning)
        {
            if (!flightManager.StartFlight(out _))
                return;

            if (UIMode == FlightUIMode.RoutePlanning)
                flightManager.RequestCloseRoutePlanning();

            ShowNodeDetail(flightManager.GetCurrentTargetNode());
        }
        else if (flightManager.State == FlightState.Arrived)
        {
            if (!flightManager.ProceedToNextNode(out _))
                return;

            if (flightManager.State == FlightState.Flying)
                ShowNodeDetail(flightManager.GetCurrentTargetNode());
            else
                ShowStartDetail();
        }

        RefreshAll();
    }

    private void HandleNodeClicked(int runtimeId)
    {
        if (flightManager == null || flightManager.CurrentMap == null)
            return;

        MapNodeRuntime node = flightManager.CurrentMap.GetNodeByRuntimeId(runtimeId);
        if (node == null)
            return;

        if (flightManager.State != FlightState.Planning)
            return;

        if (UIMode == FlightUIMode.Preview)
        {
            ShowNodeDetail(node);
            return;
        }

        if (flightManager.IsLastRouteNode(runtimeId))
        {
            if (flightManager.RemoveLastRouteNode(out _))
                ShowRouteTailDetail();
        }
        else if (flightManager.IsNodeInRoute(runtimeId))
        {
            return;
        }
        else if (flightManager.CanAddNodeToRoute(runtimeId, out _))
        {
            if (flightManager.TryAddNodeToRoute(runtimeId, out _))
                ShowNodeDetail(node);
        }

        RefreshAll();
    }

    private void HandleStartClicked()
    {
        ShowStartDetail();
    }

    private void ShowRouteTailDetail()
    {
        MapNodeRuntime tail = flightManager != null ? flightManager.GetRouteTailNode() : null;
        if (tail != null)
            ShowNodeDetail(tail);
        else
            ShowStartDetail();
    }

    private void ShowStartDetail()
    {
        detailPanel?.ShowStartLocation();
    }

    private void ShowNodeDetail(MapNodeRuntime node)
    {
        detailPanel?.ShowNode(node);
    }

    private void RefreshNodeStates()
    {
        mapView?.RefreshNodeStates(flightManager);

        bool canClickNodes = flightManager != null && flightManager.State == FlightState.Planning;
        mapView?.SetNodesInteractable(canClickNodes);
    }

    private void RefreshProgress()
    {
        if (segmentProgressSlider != null)
            segmentProgressSlider.value = flightManager != null ? flightManager.SegmentProgress01 : 0f;
    }

    private void RefreshButtons()
    {
        bool canPlan = flightManager != null && flightManager.State == FlightState.Planning;
        if (routePlanningButton != null)
            routePlanningButton.interactable = canPlan;

        if (routePlanningButtonLabel != null)
            routePlanningButtonLabel.text = UIMode == FlightUIMode.RoutePlanning ? exitPlanningText : enterPlanningText;

        if (travelButton == null)
            return;

        if (flightManager == null)
        {
            travelButton.interactable = false;
            return;
        }

        switch (flightManager.State)
        {
            case FlightState.Planning:
                travelButton.interactable = flightManager.CanConfirmRoute(out _);
                SetTravelButtonText(startTravelText);
                break;
            case FlightState.Flying:
                travelButton.interactable = false;
                SetTravelButtonText(flyingText);
                break;
            case FlightState.Arrived:
                travelButton.interactable = flightManager.HasNextRouteNode();
                SetTravelButtonText(flightManager.HasNextRouteNode() ? proceedText : completedText);
                break;
        }
    }

    private void SetTravelButtonText(string value)
    {
        if (travelButtonLabel != null)
            travelButtonLabel.text = value;
    }

    private void RefreshRouteLines()
    {
        ClearRouteLines();

        if (flightManager == null || mapView == null || flightManager.FlightInfo == null)
            return;

        IReadOnlyList<MapNodeRuntime> routeNodes = flightManager.FlightInfo.RouteNodes;
        if (routeNodes.Count == 0)
            return;

        Vector2 previous = mapView.GridToAnchoredPosition(Vector2Int.zero);
        for (int i = 0; i < routeNodes.Count; i++)
        {
            Vector2 next = mapView.GridToAnchoredPosition(routeNodes[i].GridPosition);
            CreateRouteLine(previous, next);
            previous = next;
        }
    }

    private void CreateRouteLine(Vector2 from, Vector2 to)
    {
        RectTransform parent = routeLineRoot != null ? routeLineRoot : transform as RectTransform;
        if (parent == null)
            return;

        Image line;
        if (routeLinePrefab != null)
        {
            line = Instantiate(routeLinePrefab, parent);
        }
        else
        {
            GameObject lineObject = new GameObject("RouteLine");
            lineObject.transform.SetParent(parent, false);
            line = lineObject.AddComponent<Image>();
        }

        RectTransform rect = line.rectTransform;
        Vector2 delta = to - from;
        float length = delta.magnitude;
        float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = (from + to) * 0.5f;
        rect.sizeDelta = new Vector2(length, routeLineThickness);
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);

        line.color = routeLineColor;
        line.raycastTarget = false;

        routeLines.Add(line);
    }

    private void ClearRouteLines()
    {
        for (int i = routeLines.Count - 1; i >= 0; i--)
        {
            if (routeLines[i] != null)
                Destroy(routeLines[i].gameObject);
        }

        routeLines.Clear();
    }
}
