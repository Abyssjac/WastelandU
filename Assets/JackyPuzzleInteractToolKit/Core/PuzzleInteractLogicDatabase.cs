using JackyUtility;
using UnityEngine;

namespace JackyPuzzleInteract
{
    [CreateAssetMenu(fileName = "PuzzleLogicDB_", menuName = "AllPropertyDatabases/PuzzleInteractLogicDatabase")]
    public class PuzzleInteractLogicDatabase : EnumStringKeyedDatabase<PuzzleInteractLogicProperty, Key_PuzzleLogicPP>
    {
#if UNITY_EDITOR
        [ContextMenu("Collect Entries From Folder")]
        private void CollectEntriesFromFolder()
        {
            EditorCollectFromFolder();
        }
#endif
    }
}
