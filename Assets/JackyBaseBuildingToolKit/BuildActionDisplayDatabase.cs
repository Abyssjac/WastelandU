using System.Collections.Generic;
using UnityEngine;
using JackyUtility;

/// <summary>
/// Database of <see cref="BuildActionDisplayInfo"/> entries,
/// indexed by <see cref="Key_BuildActionDisplayPP"/>.
/// </summary>
[CreateAssetMenu(fileName = "BuildActionDisplayDB_", menuName = "AllPropertyDatabases/ BuildActionDisplayDatabase")]
public class BuildActionDisplayDatabase : EnumStringKeyedDatabase<BuildActionDisplayInfo, Key_BuildActionDisplayPP>
{
    [ContextMenu("Collect Entries From Folder")]
    private void CollectEntriesFromFolder()
    {
        base.EditorCollectFromFolder();
    }
}
