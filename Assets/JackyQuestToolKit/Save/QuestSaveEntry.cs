using System;
using System.Collections.Generic;

/// <summary>Serializable save representation of one accepted quest.</summary>
[Serializable]
public class QuestSaveEntry
{
    public Key_Quest questKey = Key_Quest.None;
    public QuestProperty.QuestState state = QuestProperty.QuestState.Ongoing;
    public List<Key_MapNodePP> arrivedRequiredIslands = new List<Key_MapNodePP>();
}
