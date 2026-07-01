using JackyUtility;
using UnityEngine;

[CreateAssetMenu(fileName = "MapNodeDB_", menuName = "AllPropertyDatabases/MapNodeDatabase")]
public class MapNodeDatabase : EnumStringKeyedDatabase<MapNodeProperty, Key_MapNodePP>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}
