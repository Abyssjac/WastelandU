using JackyUtility;
using UnityEngine;

[CreateAssetMenu(fileName = "MapDataDB_", menuName = "AllPropertyDatabases/MapDataDatabase")]
public class MapDataDatabase : EnumStringKeyedDatabase<MapDataProperty, Key_MapDataPP>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}
