using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FlightMapView : MonoBehaviour
{
    [Header("Map")]
    [SerializeField] private RectTransform mapRect;
    [SerializeField] private RectTransform nodeParent;
    [SerializeField] private FlightMapNodeUI nodePrefab;

    [Header("Grid Range")]
    [SerializeField] private Vector2Int gridMin = Vector2Int.zero;
    [SerializeField] private Vector2Int gridMax = new Vector2Int(10, 10);

    [Header("Start Marker")]
    [SerializeField] private RectTransform startMarker;
    [SerializeField] private Button startMarkerButton;

    private readonly List<FlightMapNodeUI> nodeUIs = new List<FlightMapNodeUI>();
    private readonly Dictionary<int, FlightMapNodeUI> nodeUIsByRuntimeId = new Dictionary<int, FlightMapNodeUI>();

    private Action<int> onNodeClicked;
    private Action onStartClicked;

    public IReadOnlyList<FlightMapNodeUI> NodeUIs => nodeUIs;

    private void Awake()
    {
        if (startMarkerButton != null)
            startMarkerButton.onClick.AddListener(HandleStartClicked);
    }

    private void OnDestroy()
    {
        if (startMarkerButton != null)
            startMarkerButton.onClick.RemoveListener(HandleStartClicked);
    }

    public void SetCallbacks(Action<int> nodeClicked, Action startClicked)
    {
        onNodeClicked = nodeClicked;
        onStartClicked = startClicked;
    }

    public void Render(MapDataRuntime map)
    {
        ClearNodes();
        PlaceStartMarker();

        if (map == null || nodePrefab == null)
            return;

        RectTransform parent = nodeParent != null ? nodeParent : mapRect;
        if (parent == null)
            return;

        for (int i = 0; i < map.Nodes.Count; i++)
        {
            MapNodeRuntime node = map.Nodes[i];
            FlightMapNodeUI nodeUI = Instantiate(nodePrefab, parent);
            nodeUI.Bind(node);
            nodeUI.SetClickCallback(HandleNodeClicked);
            nodeUI.SetState(FlightNodeState.Unplanned);

            RectTransform rect = nodeUI.transform as RectTransform;
            if (rect != null)
                rect.anchoredPosition = GridToAnchoredPosition(node.GridPosition);

            nodeUIs.Add(nodeUI);
            nodeUIsByRuntimeId[node.RuntimeId] = nodeUI;
        }
    }

    public void RefreshNodeStates(FlightManager manager)
    {
        for (int i = 0; i < nodeUIs.Count; i++)
        {
            FlightMapNodeUI nodeUI = nodeUIs[i];
            if (nodeUI == null)
                continue;

            FlightNodeState state = manager != null
                ? manager.GetNodeState(nodeUI.RuntimeId)
                : FlightNodeState.Unplanned;

            nodeUI.SetState(state);
        }
    }

    public void SetNodesInteractable(bool interactable)
    {
        for (int i = 0; i < nodeUIs.Count; i++)
        {
            if (nodeUIs[i] != null)
                nodeUIs[i].SetInteractable(interactable);
        }
    }

    public MapNodeRuntime GetNode(int runtimeId)
    {
        return nodeUIsByRuntimeId.TryGetValue(runtimeId, out FlightMapNodeUI nodeUI)
            ? nodeUI.Node
            : null;
    }

    public Vector2 GridToAnchoredPosition(Vector2Int gridPosition)
    {
        Rect rect = mapRect != null ? mapRect.rect : new Rect(0f, 0f, 1f, 1f);
        float width = Mathf.Max(1f, gridMax.x - gridMin.x);
        float height = Mathf.Max(1f, gridMax.y - gridMin.y);

        float normalizedX = Mathf.Clamp01((gridPosition.x - gridMin.x) / width);
        float normalizedY = Mathf.Clamp01((gridPosition.y - gridMin.y) / height);

        float x = (normalizedX - 0.5f) * rect.width;
        float y = (normalizedY - 0.5f) * rect.height;
        return new Vector2(x, y);
    }

    private void ClearNodes()
    {
        for (int i = nodeUIs.Count - 1; i >= 0; i--)
        {
            if (nodeUIs[i] != null)
                Destroy(nodeUIs[i].gameObject);
        }

        nodeUIs.Clear();
        nodeUIsByRuntimeId.Clear();
    }

    private void PlaceStartMarker()
    {
        if (startMarker != null)
            startMarker.anchoredPosition = GridToAnchoredPosition(Vector2Int.zero);
    }

    private void HandleNodeClicked(int runtimeId)
    {
        onNodeClicked?.Invoke(runtimeId);
    }

    private void HandleStartClicked()
    {
        onStartClicked?.Invoke();
    }
}
