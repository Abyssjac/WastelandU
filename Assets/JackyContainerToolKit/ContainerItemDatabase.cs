using UnityEngine;
using JackyUtility;

[CreateAssetMenu(fileName = "ContainerItemDB_", menuName = "AllPropertyDatabases/ContainerItemDatabase")]
public class ContainerItemDatabase : EnumStringKeyedDatabase<ContainerItemProperty, Key_ContainerItemPP>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}
