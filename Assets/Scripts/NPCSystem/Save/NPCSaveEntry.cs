using System;
using System.Collections.Generic;

/// <summary>Serializable long-term progress for one NPC.</summary>
[Serializable]
public class NPCSaveEntry
{
    public Key_NPC npcKey = Key_NPC.None;
    public NPCStatus npcStatus = NPCStatus.Unrecruited;

    // Explicit runtime overrides. An absent interaction uses its authored default.
    public List<NPCInteractionType> unlockedInteractions = new List<NPCInteractionType>();
    public List<NPCInteractionType> lockedInteractions = new List<NPCInteractionType>();

    public bool hasRuntimeData;
    public NPCPersistentRuntimeData runtimeData = new NPCPersistentRuntimeData();
}
