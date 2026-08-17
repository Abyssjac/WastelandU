using System;
using System.Collections.Generic;

/// <summary>Top-level data for the NPC section in the unified game save.</summary>
[Serializable]
public class NPCSaveData
{
    public List<NPCSaveEntry> entries = new List<NPCSaveEntry>();
}
