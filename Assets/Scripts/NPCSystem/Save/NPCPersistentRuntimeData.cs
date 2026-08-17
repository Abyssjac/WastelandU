using System;
using UnityEngine;

/// <summary>
/// Serializable NPC state that is not derived from static properties or the
/// current room furniture. Living-environment affinity is intentionally omitted
/// because it is recalculated from the restored room assignment and furniture.
/// </summary>
[Serializable]
public class NPCPersistentRuntimeData
{
    public float dailyInteractionAffinity;
    public float familiarityAffinity;
    public int lastInteractionDay = -1;
    public bool interactedToday;

    public bool hasRoom;
    public Vector3Int assignedRoomStableId;
}
