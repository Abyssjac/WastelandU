using JackyUtility;
using UnityEngine;

/// <summary>
/// Static database containing every quest definition in the project.
/// Register this asset in <see cref="PropertyDatabaseManager"/>.
/// </summary>
[CreateAssetMenu(fileName = "QuestDB_", menuName = "AllPropertyDatabases/QuestDatabase")]
public class QuestDatabase : EnumStringKeyedDatabase<QuestProperty, Key_Quest>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}

/// <summary>
/// Stable quest identifiers. Add new values explicitly and never reuse a removed value.
/// </summary>
public enum Key_Quest
{
    None = 0,
    Quest_Tutorial_CollectResource = 10,
    Quest_Tutorial_Home = 11,
}
