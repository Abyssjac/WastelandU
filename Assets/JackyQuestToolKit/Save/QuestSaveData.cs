using System;
using System.Collections.Generic;

/// <summary>Top-level data for the "quests" section in the unified game save.</summary>
[Serializable]
public class QuestSaveData
{
    public List<QuestSaveEntry> entries = new List<QuestSaveEntry>();
}
