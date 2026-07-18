using JackyUtility;
using UnityEngine;

[CreateAssetMenu(fileName = "SellableDB_", menuName = "AllPropertyDatabases/SellableDatabase")]
public class SellableDatabase : EnumStringKeyedDatabase<SellableProperty, Key_SellablePP>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}
