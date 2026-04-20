using JackyUtility;
using UnityEngine;

namespace JackyPuzzleInteract
{
    [CreateAssetMenu(fileName = "PuzzleLogicDB_", menuName = "AllPropertyDatabases/PuzzleInteractLogicDatabase")]
    public class PuzzleInteractLogicDatabase : EnumStringKeyedDatabase<PuzzleInteractLogicProperty, Key_PuzzleLogicPP>
    {
        [ContextMenu("Collect Entries From Folder")]
        private void CollectEntriesFromFolder()
        {
            base.EditorCollectFromFolder();
        }
    }
}
