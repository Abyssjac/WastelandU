using JackyUtility;
using UnityEngine;

/// <summary>
/// Database that maps <see cref="Key_NPC"/> ¡ú <see cref="NPCProperty"/>.
/// Register this asset in PropertyDatabaseManager.allDatabases.
/// </summary>
[CreateAssetMenu(fileName = "NPCDB_", menuName = "AllPropertyDatabases/NPCDatabase")]
public class NPCDatabase : EnumStringKeyedDatabase<NPCProperty, Key_NPC>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}
