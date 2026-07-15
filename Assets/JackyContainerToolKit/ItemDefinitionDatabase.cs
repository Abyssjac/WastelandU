using JackyUtility;
using UnityEngine;

[CreateAssetMenu(fileName = "ItemDefinitionDB_", menuName = "AllPropertyDatabases/ItemDefinitionDatabase")]
public class ItemDefinitionDatabase : EnumStringKeyedDatabase<ItemDefinitionSO, Key_ItemDefinitionPP>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}
