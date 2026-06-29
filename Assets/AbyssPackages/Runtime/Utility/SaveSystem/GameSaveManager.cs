using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Global single-slot save manager. It owns the save file and delegates each section
/// to registered <see cref="IGameSavable"/> implementations.
/// </summary>
public class GameSaveManager : MonoBehaviour
{
    public static GameSaveManager Instance { get; private set; }

    [Header("Save File")]
    [SerializeField] private int saveVersion = 1;

    [Header("Runtime Save Path  (builds + editor when flag is off)")]
    [Tooltip("Sub-folder created inside Application.persistentDataPath.")]
    [SerializeField] private string runtimeSaveFolder = "Saves";

    [Tooltip("File name of the save file, including extension.")]
    [SerializeField] private string saveFileName = "game_save.json";

#if UNITY_EDITOR
    [Header("Editor Save Path  (only used in Play Mode inside the Editor)")]
    [Tooltip("When true, the save file is written inside the Assets folder so you can inspect it directly in the Project window.")]
    [SerializeField] private bool useAssetsPathInEditor = true;

    [Tooltip("Path relative to the project root (the folder that contains Assets/).")]
    [SerializeField] private string editorSaveFolder = "Assets/_SaveData";
#endif

    private readonly List<IGameSavable> savables = new List<IGameSavable>();

    public int RegisteredSavableCount => savables.Count;

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterSavable(IGameSavable savable)
    {
        if (savable == null) return;
        if (savables.Contains(savable)) return;

        savables.Add(savable);
        Debug.Log($"[GameSaveManager] Registered savable: {savable.SaveKey}");
    }

    public void UnregisterSavable(IGameSavable savable)
    {
        if (savable == null) return;

        if (savables.Remove(savable))
            Debug.Log($"[GameSaveManager] Unregistered savable: {savable.SaveKey}");
    }

    public string[] GetRegisteredSavableKeys()
    {
        return savables
            .Where(s => s != null)
            .OrderBy(s => s.LoadOrder)
            .Select(s => s.SaveKey)
            .ToArray();
    }

    public void Save()
    {
        var data = new GameSaveData
        {
            saveVersion = saveVersion,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        var usedKeys = new HashSet<string>();
        foreach (var savable in GetSortedSavables())
        {
            if (string.IsNullOrWhiteSpace(savable.SaveKey))
            {
                Debug.LogWarning("[GameSaveManager] Skipped a savable with an empty SaveKey.");
                continue;
            }

            if (!usedKeys.Add(savable.SaveKey))
            {
                Debug.LogWarning($"[GameSaveManager] Duplicate SaveKey '{savable.SaveKey}' skipped.");
                continue;
            }

            try
            {
                data.sections.Add(new GameSaveSection
                {
                    key = savable.SaveKey,
                    version = savable.SaveVersion,
                    json = savable.CaptureSaveJson()
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameSaveManager] Failed to capture '{savable.SaveKey}': {ex}");
            }
        }

        string path = GetSavePath();
        EnsureDirectory(path);

        string json = JsonUtility.ToJson(data, prettyPrint: true);
        File.WriteAllText(path, json);

        Debug.Log($"[GameSaveManager] Saved {data.sections.Count} sections -> {path}");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    public void Load()
    {
        string path = GetSavePath();

        if (!File.Exists(path))
        {
            Debug.LogWarning($"[GameSaveManager] No save file found at: {path}");
            NotifyNoSaveDataForAll();
            return;
        }

        string json = File.ReadAllText(path);
        GameSaveData data = JsonUtility.FromJson<GameSaveData>(json);

        if (data == null || data.sections == null)
        {
            Debug.LogError("[GameSaveManager] Failed to deserialize game save data. File may be corrupted or use an old format.");
            NotifyNoSaveDataForAll();
            return;
        }

        var sectionByKey = new Dictionary<string, GameSaveSection>();
        foreach (var section in data.sections)
        {
            if (section == null || string.IsNullOrWhiteSpace(section.key)) continue;
            if (!sectionByKey.ContainsKey(section.key))
                sectionByKey.Add(section.key, section);
        }

        foreach (var savable in GetSortedSavables())
        {
            if (sectionByKey.TryGetValue(savable.SaveKey, out var section))
            {
                try
                {
                    savable.RestoreSaveJson(section.json);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[GameSaveManager] Failed to restore '{savable.SaveKey}': {ex}");
                }
            }
            else
            {
                TryNotifyNoSaveData(savable);
            }
        }

        Debug.Log($"[GameSaveManager] Loaded {sectionByKey.Count} sections from {path} (saved: {data.savedAt})");
    }

    public void DeleteSave()
    {
        string path = GetSavePath();

        if (!File.Exists(path))
        {
            Debug.Log("[GameSaveManager] No save file to delete.");
            return;
        }

        File.Delete(path);
        Debug.Log($"[GameSaveManager] Deleted save file: {path}");

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    public bool SaveExists() => File.Exists(GetSavePath());

    public string GetSavePath()
    {
#if UNITY_EDITOR
        if (useAssetsPathInEditor)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, editorSaveFolder, saveFileName);
        }
#endif
        return Path.Combine(Application.persistentDataPath, runtimeSaveFolder, saveFileName);
    }

    private List<IGameSavable> GetSortedSavables()
    {
        return savables
            .Where(s => s != null)
            .OrderBy(s => s.LoadOrder)
            .ThenBy(s => s.SaveKey)
            .ToList();
    }

    private void NotifyNoSaveDataForAll()
    {
        foreach (var savable in GetSortedSavables())
            TryNotifyNoSaveData(savable);
    }

    private static void TryNotifyNoSaveData(IGameSavable savable)
    {
        try
        {
            savable.OnNoSaveData();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GameSaveManager] Failed to handle missing data for '{savable.SaveKey}': {ex}");
        }
    }

    private static void EnsureDirectory(string filePath)
    {
        string dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }
}
