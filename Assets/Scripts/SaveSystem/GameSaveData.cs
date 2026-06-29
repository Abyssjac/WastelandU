using System;
using System.Collections.Generic;

/// <summary>
/// Top-level single-slot save file. Individual systems are stored as keyed sections.
/// </summary>
[Serializable]
public class GameSaveData
{
    public int saveVersion = 1;
    public string savedAt;
    public List<GameSaveSection> sections = new List<GameSaveSection>();
}

[Serializable]
public class GameSaveSection
{
    public string key;
    public int version;
    public string json;
}
