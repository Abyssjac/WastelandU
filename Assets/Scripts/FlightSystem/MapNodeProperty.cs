using JackyUtility;
using UnityEngine;

public enum Key_MapNodePP
{
    None = 0,
    ResourceIsland = 1,
    TradeIsland = 2,
    QuestIsland = 3,
    DangerIsland = 4,
}

public enum MapNodeType
{
    None = 0,
    Resource = 1,
    Trade = 2,
    Quest = 3,
    Danger = 4,
}

[CreateAssetMenu(fileName = "MapNodePP_", menuName = "AllProperties/MapNodeProperty")]
public class MapNodeProperty : EnumStringKeyedProperty<Key_MapNodePP>
{
    [Header("Display")]
    public string displayName;

    [TextArea(2, 5)]
    public string description;

    public Sprite icon;

    [Header("Node")]
    public MapNodeType nodeType = MapNodeType.None;
}
