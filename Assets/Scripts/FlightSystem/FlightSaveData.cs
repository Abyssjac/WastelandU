using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent flight progress for the shared game-save pipeline.
/// Runtime nodes are intentionally not serialized: fixed maps recreate them from MapDataPP.
/// </summary>
[Serializable]
public class FlightSaveData
{
    public Key_MapDataPP currentMapKey = Key_MapDataPP.None;
    public Key_MapNodePP currentIslandKey = Key_MapNodePP.None;
    public FlightState state = FlightState.Planning;

    /// <summary>
    /// The last docked position. While flying, this is the current segment's departure position.
    /// It lets a first route segment resume even though currentIslandKey is None during travel.
    /// </summary>
    public Vector2Int currentMapPosition;

    public List<Key_MapNodePP> routeNodeKeys = new List<Key_MapNodePP>();
    public int currentRouteIndex;

    /// <summary>Normalized elapsed progress for the active segment; meaningful only while Flying.</summary>
    [Range(0f, 1f)] public float segmentProgress01;
}
