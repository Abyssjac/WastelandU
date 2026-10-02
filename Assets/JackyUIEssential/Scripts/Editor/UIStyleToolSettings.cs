using System;
using System.Collections.Generic;
using AbyssToolKitUnity.Utility;
using JackyUtility;
using UnityEditor;
using UnityEngine;

namespace JackyUIEssential.Editor
{
    /// <summary>
    /// Project-owned editor configuration for the UI style builder and prefab
    /// tracker workflows. Folder references are Unity assets rather than fixed
    /// strings so the toolkit can move without changing tool code.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UIStyleToolSettings_",
        menuName = "AbyssTools/UIEssential/UI Style Tool Settings")]
    public sealed class UIStyleToolSettings : ScriptableObject
    {
        [Header("Style Builder")]
        [SerializeField] private UIStyleAssemblySchema _assemblySchema;
        [SerializeField] private CustomUIComponentLibrary _baseComponentLibrary;
        [SerializeField] private DefaultAsset _prefabOutputRoot;
        [SerializeField] private DefaultAsset _visualLibraryOutputFolder;
        [SerializeField] private DefaultAsset _componentLibraryOutputFolder;

        [Header("Tracked Dynamic UI Prefabs")]
        [SerializeField] private List<DefaultAsset> _trackedPrefabFolders =
            new List<DefaultAsset>();

        public UIStyleAssemblySchema AssemblySchema => _assemblySchema;
        public CustomUIComponentLibrary BaseComponentLibrary => _baseComponentLibrary;

        /// <summary>
        /// Validates and resolves all assembly inputs into AssetDatabase paths.
        /// Generated assets intentionally remain in Assets/, never in a package.
        /// </summary>
        public bool TryGetBuildPaths(
            out string prefabOutputRoot,
            out string visualLibraryOutputFolder,
            out string componentLibraryOutputFolder,
            out string message)
        {
            prefabOutputRoot = GetAssetsFolderPath(_prefabOutputRoot);
            visualLibraryOutputFolder = GetAssetsFolderPath(_visualLibraryOutputFolder);
            componentLibraryOutputFolder = GetAssetsFolderPath(_componentLibraryOutputFolder);

            if (_assemblySchema == null)
            {
                message = "Assign an Assembly Schema in UI Style Tool Settings.";
                return false;
            }

            if (_baseComponentLibrary == null)
            {
                message = "Assign a Base Component Library in UI Style Tool Settings.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(prefabOutputRoot))
            {
                message = "Assign a Prefab Output Root folder inside Assets/.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(visualLibraryOutputFolder))
            {
                message = "Assign a Visual Library Output Folder inside Assets/.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(componentLibraryOutputFolder))
            {
                message = "Assign a Component Library Output Folder inside Assets/.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        /// <summary>
        /// Returns unique, valid Assets folders. Consumers scan each folder
        /// recursively through AssetDatabase.FindAssets.
        /// </summary>
        public List<string> GetTrackedPrefabFolderPaths()
        {
            var paths = new List<string>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (_trackedPrefabFolders == null)
            {
                return paths;
            }

            for (int index = 0; index < _trackedPrefabFolders.Count; index++)
            {
                string path = GetAssetsFolderPath(_trackedPrefabFolders[index]);
                if (!string.IsNullOrWhiteSpace(path) && seenPaths.Add(path))
                {
                    paths.Add(path);
                }
            }

            return paths;
        }

        private static string GetAssetsFolderPath(DefaultAsset folderAsset)
        {
            if (folderAsset == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(folderAsset)?.Replace("\\", "/").TrimEnd('/');
            if (string.IsNullOrWhiteSpace(path)
                || (!string.Equals(path, "Assets", StringComparison.Ordinal)
                    && !path.StartsWith("Assets/", StringComparison.Ordinal))
                || !AssetDatabase.IsValidFolder(path))
            {
                return string.Empty;
            }

            return path;
        }
    }
}
