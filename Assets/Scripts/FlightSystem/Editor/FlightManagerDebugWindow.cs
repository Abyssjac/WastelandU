using JackyUtility;
using UnityEditor;
using UnityEngine;

public class FlightManagerDebugWindow : DebugEditorWindow<FlightManager>
{
    private Key_MapDataPP selectedMapKey = Key_MapDataPP.None;
    private string lastResult = "";

    [MenuItem("Wasteland Debug/Flight Manager")]
    public static void ShowWindow()
    {
        GetWindow<FlightManagerDebugWindow>("Flight Manager Debug").Show();
    }

    protected override void DrawContent()
    {
        FlightManager mgr = Target;
        SyncSelectedMapKey(mgr);

        DrawMapSection(mgr);
        DrawFlightStateSection(mgr);
        DrawRuntimeNodesSection(mgr);
        DrawRouteSection(mgr);
        DrawControlSection(mgr);
    }

    private void DrawMapSection(FlightManager mgr)
    {
        Header("Map");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Map Key", GUILayout.Width(90));
        selectedMapKey = (Key_MapDataPP)EditorGUILayout.EnumPopup(selectedMapKey);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply Map Key", GUILayout.Height(22)))
        {
            mgr.SetCurrentMapKey(selectedMapKey);
            lastResult = $"Applied map key: {selectedMapKey}";
        }

        if (GUILayout.Button("Refresh Runtime Map", GUILayout.Height(22)))
        {
            mgr.SetCurrentMapKey(selectedMapKey);
            bool refreshed = mgr.RefreshMap();
            lastResult = refreshed ? "Runtime map refreshed." : "Failed to refresh runtime map.";
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Simulate New Day / Refresh Map", GUILayout.Height(22)))
        {
            bool refreshed = mgr.RefreshMap();
            lastResult = refreshed ? "New day simulated. Runtime map refreshed." : "Failed to simulate new day.";
        }

        Row("Current Map", mgr.CurrentMapKey.ToString());
        Row("Runtime Nodes", mgr.CurrentMap != null ? mgr.CurrentMap.NodeCount.ToString() : "0");
        Row("Max Route Nodes", mgr.MaxRouteNodeCount.ToString());
        Row("Effective Max", mgr.EffectiveMaxRouteNodeCount.ToString());
        Row("Time Per Grid", mgr.TimePerGrid.ToString());

        if (!string.IsNullOrEmpty(lastResult))
            EditorGUILayout.HelpBox(lastResult, MessageType.None);
    }

    private void DrawFlightStateSection(FlightManager mgr)
    {
        Header("Flight State");

        FlightInfo info = mgr.FlightInfo;
        if (info == null)
        {
            EditorGUILayout.HelpBox("FlightInfo is null.", MessageType.Warning);
            return;
        }

        ColoredRow("State", info.State.ToString(), GetFlightStateColor(info.State));
        Row("Current Position", info.CurrentPosition.ToString());
        Row("Current Node", info.CurrentNode != null ? FormatNode(info.CurrentNode) : "Start (0, 0)");
        Row("Current Target", info.GetCurrentTarget() != null ? FormatNode(info.GetCurrentTarget()) : "-");
        Row("Route Index", info.CurrentRouteIndex.ToString());
        Row("Segment Time", mgr.GetCurrentSegmentTimeUnits().ToString());
        Row("Segment Progress", $"{mgr.CurrentSegmentElapsedTime} / {mgr.CurrentSegmentTotalTime} ({mgr.SegmentProgress01:P0})");
    }

    private void DrawRuntimeNodesSection(FlightManager mgr)
    {
        Header("Runtime Nodes");

        MapDataRuntime map = mgr.CurrentMap;
        if (map == null || map.NodeCount == 0)
        {
            EditorGUILayout.HelpBox("No runtime map. Select a Map Key and refresh.", MessageType.None);
            return;
        }

        for (int i = 0; i < map.Nodes.Count; i++)
        {
            MapNodeRuntime node = map.Nodes[i];
            FlightNodeState nodeState = mgr.GetNodeState(node.RuntimeId);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(
                $"{node.RuntimeId} | {node.NodeKey} | {node.GridPosition} | {nodeState}",
                GUILayout.MinWidth(260));

            GUI.enabled = mgr.FlightInfo != null && mgr.FlightInfo.State == FlightState.Planning;
            if (GUILayout.Button("Add", GUILayout.Width(60), GUILayout.Height(20)))
                SetResult(mgr.TryAddNodeToRoute(node.RuntimeId, out string failReason), failReason);
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawRouteSection(FlightManager mgr)
    {
        Header("Route");

        FlightInfo info = mgr.FlightInfo;
        if (info == null || info.RouteNodes.Count == 0)
        {
            EditorGUILayout.HelpBox("Route is empty.", MessageType.None);
            return;
        }

        for (int i = 0; i < info.RouteNodes.Count; i++)
        {
            MapNodeRuntime node = info.RouteNodes[i];
            string marker = i == info.CurrentRouteIndex ? ">" : " ";
            Row($"{marker} {i}", FormatNode(node));
        }
    }

    private void DrawControlSection(FlightManager mgr)
    {
        Header("Controls");

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Remove Last", GUILayout.Height(22)))
            SetResult(mgr.RemoveLastRouteNode(out string failReason), failReason);

        if (GUILayout.Button("Clear Route", GUILayout.Height(22)))
            SetResult(mgr.ClearRoute(out string failReason), failReason);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Start Flight", GUILayout.Height(22)))
            SetResult(mgr.StartFlight(out string failReason), failReason);

        if (GUILayout.Button("Arrive", GUILayout.Height(22)))
            SetResult(mgr.ArriveCurrentTarget(out string failReason), failReason);

        if (GUILayout.Button("Proceed", GUILayout.Height(22)))
            SetResult(mgr.ProceedToNextNode(out string failReason), failReason);

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("+1 TimeUnit", GUILayout.Height(22)))
        {
            mgr.AdvanceSegmentProgress(1);
            lastResult = "Advanced segment progress by 1.";
        }

        if (GUILayout.Button("Complete Segment Progress", GUILayout.Height(22)))
        {
            mgr.CompleteSegmentProgress();
            lastResult = "Segment progress completed.";
        }

        if (GUILayout.Button("Reset Progress", GUILayout.Height(22)))
        {
            mgr.ResetSegmentProgress();
            lastResult = "Segment progress reset.";
        }

        EditorGUILayout.EndHorizontal();
    }

    private void SyncSelectedMapKey(FlightManager mgr)
    {
        if (selectedMapKey == Key_MapDataPP.None && mgr.CurrentMapKey != Key_MapDataPP.None)
            selectedMapKey = mgr.CurrentMapKey;
    }

    private void SetResult(bool success, string failReason)
    {
        lastResult = success
            ? "Operation succeeded."
            : string.IsNullOrEmpty(failReason) ? "Operation failed." : failReason;
    }

    private static string FormatNode(MapNodeRuntime node)
    {
        if (node == null)
            return "-";

        return $"{node.RuntimeId}: {node.DisplayName} ({node.NodeKey}) {node.GridPosition}";
    }

    private static Color GetFlightStateColor(FlightState state)
    {
        switch (state)
        {
            case FlightState.Flying:
                return Color.cyan;
            case FlightState.Arrived:
                return Color.green;
            default:
                return Color.white;
        }
    }
}
