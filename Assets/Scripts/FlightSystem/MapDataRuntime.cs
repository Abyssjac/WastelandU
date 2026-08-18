using System.Collections.Generic;
using UnityEngine;

public sealed class MapDataRuntime
{
    private readonly List<MapNodeRuntime> nodes;
    private readonly Dictionary<int, MapNodeRuntime> nodesByRuntimeId;

    public MapDataRuntime(Key_MapDataPP sourceMapKey, IReadOnlyList<MapNodeRuntime> sourceNodes)
    {
        SourceMapKey = sourceMapKey;
        nodes = new List<MapNodeRuntime>();
        nodesByRuntimeId = new Dictionary<int, MapNodeRuntime>();

        if (sourceNodes == null)
            return;

        for (int i = 0; i < sourceNodes.Count; i++)
        {
            MapNodeRuntime node = sourceNodes[i];
            if (node == null)
                continue;

            nodes.Add(node);
            nodesByRuntimeId[node.RuntimeId] = node;
        }
    }

    public Key_MapDataPP SourceMapKey { get; }
    public IReadOnlyList<MapNodeRuntime> Nodes => nodes;
    public int NodeCount => nodes.Count;

    public MapNodeRuntime GetNodeByRuntimeId(int runtimeId)
    {
        nodesByRuntimeId.TryGetValue(runtimeId, out MapNodeRuntime node);
        return node;
    }

    public bool TryGetNodeByRuntimeId(int runtimeId, out MapNodeRuntime node)
    {
        return nodesByRuntimeId.TryGetValue(runtimeId, out node);
    }
}

public sealed class MapNodeRuntime
{
    public MapNodeRuntime(int runtimeId, Key_MapNodePP nodeKey, MapNodeProperty property, Vector2Int mapPosition)
    {
        RuntimeId = runtimeId;
        NodeKey = nodeKey;
        Property = property;
        MapPosition = mapPosition;
    }

    public int RuntimeId { get; }
    public Key_MapNodePP NodeKey { get; }
    public MapNodeProperty Property { get; }
    /// <summary>Authored local position in the unscaled map content.</summary>
    public Vector2Int MapPosition { get; }

    public string DisplayName
    {
        get
        {
            if (Property != null && !string.IsNullOrWhiteSpace(Property.displayName))
                return Property.displayName;

            return NodeKey.ToString();
        }
    }
}
