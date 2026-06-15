using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Manages saving and loading the build grid to/from a single JSON file.
///
/// Save path behaviour:
///   - In the Unity Editor, when <see cref="useAssetsPathInEditor"/> is true the file is written
///     inside the project's Assets folder (editorSaveFolder) so it is immediately visible
///     in the Project window and easy to inspect with a text editor.
///   - In all other cases (builds, or editor with the flag off) the file is written to
///     Application.persistentDataPath / <see cref="runtimeSaveFolder"/> / <see cref="saveFileName"/>.
///
/// Usage:
///   BuildSaveManager.Instance.Save();
///   BuildSaveManager.Instance.Load();
///   BuildSaveManager.Instance.DeleteSave();
/// </summary>
public class BuildSaveManager : MonoBehaviour
{
    public static BuildSaveManager Instance { get; private set; }

    [Header("Runtime Save Path  (builds + editor when flag is off)")]
    [Tooltip("Sub-folder created inside Application.persistentDataPath.")]
    [SerializeField] private string runtimeSaveFolder = "Saves";

    [Tooltip("File name of the save file, including extension.")]
    [SerializeField] private string saveFileName = "build_save.json";

#if UNITY_EDITOR
    [Header("Editor Save Path  (only used in Play Mode inside the Editor)")]
    [Tooltip("When true, the save file is written inside the Assets folder so you can inspect it directly in the Project window.")]
    [SerializeField] private bool useAssetsPathInEditor = true;

    [Tooltip("Path relative to the project root (the folder that contains Assets/).\n" +
             "Example: 'Assets/SaveData'  →  file lands at  <project>/Assets/SaveData/build_save.json")]
    [SerializeField] private string editorSaveFolder = "Assets/SaveData";
#endif

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes the current build grid to the configured JSON file.
    /// Overwrites any existing save. Safe to call at any time during Play Mode.
    /// </summary>
    public void Save()
    {
        var bm = BuildManager.Instance;
        if (bm == null) { Debug.LogWarning("[BuildSaveManager] BuildManager not found. Cannot save."); return; }

        var data = new BuildSaveData
        {
            instanceCounterSnapshot = bm.InstanceCounter,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };

        foreach (var kvp in bm.Grid.AllPlaced)
        {
            var d = kvp.Value;
            data.entries.Add(new BuildableSaveEntry(
                d.InstanceId,
                d.Property.EnumKey,
                d.AnchorCell,
                d.RotationStep
            ));
        }

        string path = GetSavePath();
        EnsureDirectory(path);

        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(path, json);

        Debug.Log($"[BuildSaveManager] Saved {data.entries.Count} buildables → {path}");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    /// <summary>
    /// Deserializes the JSON save file and restores the build grid via
    /// <see cref="BuildManager.RestoreFromSaveData"/>.
    /// Does nothing if the save file does not exist.
    /// </summary>
    public void Load()
    {
        string path = GetSavePath();

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[BuildSaveManager] No save file found at: {path}");
            return;
        }

        var bm = BuildManager.Instance;
        if (bm == null) { Debug.LogWarning("[BuildSaveManager] BuildManager not found. Cannot load."); return; }

        string json = File.ReadAllText(path);
        BuildSaveData data = JsonUtility.FromJson<BuildSaveData>(json);

        if (data == null)
        {
            Debug.LogError("[BuildSaveManager] Failed to deserialize save data. File may be corrupted.");
            return;
        }

        bm.RestoreFromSaveData(data);
        Debug.Log($"[BuildSaveManager] Loaded {data.entries.Count} buildables from {path}  (saved: {data.savedAt})");
    }

    /// <summary>
    /// Deletes the save file if it exists.
    /// </summary>
    public void DeleteSave()
    {
        string path = GetSavePath();

        if (!File.Exists(path))
        {
            Debug.Log("[BuildSaveManager] No save file to delete.");
            return;
        }

        File.Delete(path);
        Debug.Log($"[BuildSaveManager] Deleted save file: {path}");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    /// <summary>Returns true if a save file currently exists on disk.</summary>
    public bool SaveExists() => File.Exists(GetSavePath());

    /// <summary>Returns the full absolute path of the save file.</summary>
    public string GetSavePath()
    {
#if UNITY_EDITOR
        if (useAssetsPathInEditor)
        {
            // Application.dataPath points to <project>/Assets — go one level up to reach project root
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, editorSaveFolder, saveFileName);
        }
#endif
        return Path.Combine(Application.persistentDataPath, runtimeSaveFolder, saveFileName);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private static void EnsureDirectory(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
}
