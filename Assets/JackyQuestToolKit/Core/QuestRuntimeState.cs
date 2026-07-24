using System;
using System.Collections.Generic;

/// <summary>
/// Mutable state for one accepted quest. A missing runtime record represents a hidden, locked quest.
/// </summary>
[Serializable]
public class QuestRuntimeState
{
    public Key_Quest questKey = Key_Quest.None;
    public QuestProperty.QuestState state = QuestProperty.QuestState.Ongoing;

    /// <summary>
    /// Islands reached after this quest was accepted. Item requirements deliberately do not store progress;
    /// they always evaluate against the live inventory count.
    /// </summary>
    public List<Key_MapNodePP> arrivedRequiredIslands = new List<Key_MapNodePP>();
}
