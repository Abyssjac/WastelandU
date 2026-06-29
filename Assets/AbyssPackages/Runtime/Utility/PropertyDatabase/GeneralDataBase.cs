using System.Collections.Generic;
using UnityEngine;

namespace JackyUtility 
{
    public interface IEnumStringKeyedEntry<TEnum> where TEnum : struct
    {
        TEnum EnumKey { get; }
        string StringKey { get; }
    }

    public abstract class EnumStringKeyedProperty<TEnum> : ScriptableObject, IEnumStringKeyedEntry<TEnum>
        where TEnum : struct
    {
        [SerializeField] private TEnum enumKey;
        [SerializeField] private string stringKey;

        public TEnum EnumKey => enumKey;
        public string StringKey => stringKey;
    }

    public abstract class EnumStringKeyedDatabase<TEntry, TEnum> : ScriptableObject
        where TEntry : ScriptableObject, IEnumStringKeyedEntry<TEnum>
        where TEnum : struct
    {
        [SerializeField] private List<TEntry> entries = new List<TEntry>();

        private Dictionary<TEnum, TEntry> byEnum;
        private Dictionary<string, TEntry> byString;

        public IReadOnlyList<TEntry> Entries => entries;
        public IReadOnlyDictionary<TEnum, TEntry> ByEnum => byEnum;
        public IReadOnlyDictionary<string, TEntry> ByString => byString;

        public void InitializeDictionaries()
        {
            byEnum = new Dictionary<TEnum, TEntry>();
            byString = new Dictionary<string, TEntry>();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry == null) continue;

                var enumKey = entry.EnumKey;
                var stringKey = entry.StringKey;

                if (!byEnum.ContainsKey(enumKey))
                    byEnum.Add(enumKey, entry);

                if (!string.IsNullOrEmpty(stringKey) && !byString.ContainsKey(stringKey))
                    byString.Add(stringKey, entry);
            }
        }

        public bool TryGetByEnum(TEnum key, out TEntry entry)
        {
            EnsureInitialized();
            return byEnum.TryGetValue(key, out entry);
        }

        public TEntry GetByEnum(TEnum key)
        {
            return TryGetByEnum(key, out var entry) ? entry : null;
        }

        public bool TryGetByString(string key, out TEntry entry)
        {
            EnsureInitialized();
            if (string.IsNullOrEmpty(key))
            {
                entry = null;
                return false;
            }

            return byString.TryGetValue(key, out entry);
        }

        public TEntry GetByString(string key)
        {
            return TryGetByString(key, out var entry) ? entry : null;
        }

        private void EnsureInitialized()
        {
            if (byEnum == null || byString == null)
                InitializeDictionaries();
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            InitializeDictionaries();
        }
        [Tooltip("Folder to scan for entries (relative to Assets).\n" +
                 "e.g. 'JackyBaseBuildingToolKit/Properties'\n" +
                 "Leave empty to scan entire project.")]
        [SerializeField] private string scanFolder = "";

        /// <summary>
        /// Editor-only: Scan a folder (or entire project if scanFolder is empty)
        /// for all SO assets of type TEntry and populate the entries list.
        /// </summary>
        [ContextMenu("Collect Entries From Folder")]
        protected void EditorCollectFromFolder()
        {
            string searchPath = string.IsNullOrWhiteSpace(scanFolder)
                ? "Assets"
                : "Assets/" + scanFolder.TrimStart('/');

            if (!UnityEditor.AssetDatabase.IsValidFolder(searchPath))
            {
                Debug.LogWarning($"[{GetType().Name}] Folder not found: '{searchPath}'. Check the scanFolder field.");
                return;
            }

            var guids = UnityEditor.AssetDatabase.FindAssets(
                $"t:{typeof(TEntry).Name}", new[] { searchPath });

            entries.Clear();

            for (int i = 0; i < guids.Length; i++)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TEntry>(path);
                if (asset != null)
                    entries.Add(asset);
            }

            entries.Sort((a, b) =>
                Comparer<TEnum>.Default.Compare(a.EnumKey, b.EnumKey));

            InitializeDictionaries();
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[{GetType().Name}] Collected {entries.Count} entries of type " +
                      $"{typeof(TEntry).Name} from '{searchPath}'.");
        }

        [ContextMenu("Test Void")]
        public void EditorTestVoid()
        {
            Debug.Log($"[{GetType().Name}] Test Void called. This is just a placeholder for testing context menu.");
        }
#endif
    }
}