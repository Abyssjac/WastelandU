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

    [Header("Legacy Start Marker")]
    [Tooltip("The map now represents the current location with the corresponding map node. This legacy marker is hidden at runtime.")]
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
        HideLegacyStartMarker();

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
                rect.anchoredPosition = MapToAnchoredPosition(node.MapPosition);

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

    /// <summary>
    /// Map positions are authored directly in the unscaled MapContent coordinate space.
    /// Keep nodeParent, route lines, map artwork and location markers under that same content transform.
    /// </summary>
    public Vector2 MapToAnchoredPosition(Vector2Int mapPosition)
    {
        return mapPosition;
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

    private void HideLegacyStartMarker()
    {
        if (startMarker != null)
            startMarker.gameObject.SetActive(false);
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
