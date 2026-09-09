using JackyUtility;
using UnityEngine;

[CreateAssetMenu(fileName = "TestDatabaseDB_", menuName = "AllPropertyDatabases/TestDatabase")]
public class TestDatabase : EnumStringKeyedDatabase<TestProperty, Key_TestPP>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}