using JackyUtility;
using UnityEngine;

/// <summary>
/// Database mapping <see cref="Key_NPC"/> to its authored
/// <see cref="NPCInteractionProperty"/>.
/// </summary>
[CreateAssetMenu(fileName = "NPCInteractionDB_", menuName = "AllPropertyDatabases/NPCInteractionDatabase")]
public class NPCInteractionDatabase : EnumStringKeyedDatabase<NPCInteractionProperty, Key_NPC>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}
