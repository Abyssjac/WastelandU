using JackyUtility;
using UnityEngine;

[CreateAssetMenu(fileName = "StoreInventoryDatabaseDB_", menuName = "AllPropertyDatabases/StoreInventoryDatabase")]
public class StoreInventoryDatabase : EnumStringKeyedDatabase<StoreInventoryProperty, Key_StoreInventory>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}