using JackyUtility;
using UnityEngine;

public enum Key_MapNodePP
{
    None = 0,
    ResourceIsland = 1,
    TradeIsland = 2,
    QuestIsland = 3,
    DangerIsland = 4,

    MainIsland_Home = 10,
    MainIsland_FairwindDock = 11,
    MainIsland_BrassbellPort = 12,
    MainIsland_MistweilMarket = 13,
    MainIsland_OldAnchor = 14,
    ResourceIsland_RuinedVillage = 20,
    ResourceIsland_Mountain = 21,
    ResourceIsland_Mine = 22,
}

public enum MapNodeType
{
    None = 0,
    Resource = 1,
    Trade = 2,
    Quest = 3,
    Danger = 4,
    Main = 5,
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

    [Header("Island Scene")]
    [Tooltip("Exact Build Settings scene name loaded when the player visits this node. Leave empty for nodes without an enterable island.")]
    public string islandSceneKey;
}
