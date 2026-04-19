using JackyUtility;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildableDB_", menuName = "AllPropertyDatabases/ BuildableDatabase")]
public class BuildableDatabase : EnumStringKeyedDatabase<BuildableProperty, Key_BuildablePP>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}