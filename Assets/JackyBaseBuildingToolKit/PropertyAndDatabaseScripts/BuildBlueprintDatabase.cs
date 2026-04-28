using UnityEngine;

[CreateAssetMenu(fileName = "BuildBlueprintDB_", menuName = "AllPropertyDatabases/ BuildBlueprintDatabase")]
public class BuildBlueprintDatabase : JackyUtility.EnumStringKeyedDatabase<BuildBlueprintProperty, Key_BuildBlueprintPP>
{
#if UNITY_EDITOR
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        EditorCollectFromFolder();
    }
#endif
}
