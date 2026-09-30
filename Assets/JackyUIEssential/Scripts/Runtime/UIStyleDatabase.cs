using JackyUtility;
using UnityEngine;

namespace JackyUIEssential
{
    /// <summary>
    /// Database of complete UI style pairings. Its main asset must be
    /// registered once with PropertyDatabaseManager so editor Property links
    /// and style tools can resolve their selected Key_UIStylePP values.
    /// </summary>
    [CreateAssetMenu(fileName = "UIStyleDB_", menuName = "AllPropertyDatabases/UIStyleDatabase")]
    public class UIStyleDatabase : EnumStringKeyedDatabase<UIStyleProperty, Key_UIStylePP>
    {
#if UNITY_EDITOR
        [ContextMenu("Collect Entries From Folder")]
        private void CollectEntriesFromFolder()
        {
            base.EditorCollectFromFolder();
        }
#endif
    }
}
