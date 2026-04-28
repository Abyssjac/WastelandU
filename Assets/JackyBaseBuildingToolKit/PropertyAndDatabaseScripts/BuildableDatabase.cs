using JackyUtility;
using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildableDB_", menuName = "AllPropertyDatabases/ BuildableDatabase")]
public class BuildableDatabase : EnumStringKeyedDatabase<BuildableProperty, Key_BuildablePP>
{
#if UNITY_EDITOR
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        //base.EditorCollectFromFolder();
        EditorCollectFromFolder();
    }
#endif
}