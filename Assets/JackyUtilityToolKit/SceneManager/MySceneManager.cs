using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using JackyUtility;

// ── Supporting types ────────────────────────────────────────────────────────

/// <summary>
/// Modifier key required together with <see cref="SceneEntry.hotkey"/>.
/// </summary>
public enum SceneHotkeyModifier
{
    None,
    Ctrl,
    Alt,
    Shift
}

/// <summary>
/// One entry in the scene list configured in the Inspector.
/// </summary>
[Serializable]
public class SceneEntry
{
    [Tooltip("Friendly name shown in debug tools and logs.")]
    public string displayName;

    [Tooltip("Exact scene name as registered in File → Build Settings.")]
    public string sceneName;

    [Header("Save / Load")]
    [Tooltip("If true, GameSaveManager.Save() is called before leaving this scene.")]
    public bool autoSaveBeforeLeaving = true;

    [Tooltip("If true, GameSaveManager.Load() is called after entering this scene.")]
    public bool autoLoadAfterEntering = false;

#if UNITY_EDITOR
    [Header("Developer Hotkey (Editor only)")]
    [Tooltip("Modifier key held while pressing Hotkey to load this scene.")]
    public SceneHotkeyModifier modifier = SceneHotkeyModifier.None;

    [Tooltip("Key pressed (together with Modifier) to load this scene. Set to None to disable.")]
    public KeyCode hotkey = KeyCode.None;
#endif
}

/// <summary>
/// Central scene-switching manager.
/// Wraps <see cref="UnityEngine.SceneManagement.SceneManager"/> to provide a single entry
/// point for all scene transitions.
///
/// Deployment:
///   Add the Prefab to <see cref="MyGameSystem.allSystemPrefabSingletons"/> so it is
///   instantiated automatically on game start and persists across all scenes.
///
/// Public API:
///   MySceneManager.Instance.LoadScene("GameScene");
///   MySceneManager.Instance.GoToMainMenu();
///   MySceneManager.Instance.ReloadCurrentScene();
///   MySceneManager.Instance.GetCurrentSceneName();
///
/// Debug console (requires DebugConsoleManager):
///   scene &lt;sceneName&gt;    — load scene by name
///   scene ls             — list all configured entries
/// </summary>
public class MySceneManager : MonoBehaviour
{
    public static MySceneManager Instance { get; private set; }

    // ── Inspector ────────────────────────────────────────────────────────────

    [Header("Scene List")]
    [Tooltip("All scenes the game can load. Entries must match names in File → Build Settings.")]
    [SerializeField] private List<SceneEntry> sceneEntries = new();

    [Header("Main Menu")]
    [Tooltip("Scene name of the main menu. Used by GoToMainMenu().")]
    [SerializeField] private string mainMenuSceneName = "S_MainMenu";

    [Tooltip("Master switch for scene-entry auto save before scene transitions.")]
    [SerializeField] private bool autoSaveOnLeaveGameScene = true;

#if UNITY_EDITOR
    [Header("Developer Hotkeys (Editor only)")]
    [Tooltip("Master switch — disable to ignore all hotkeys without clearing the bindings.")]
    [SerializeField] private bool enableHotkeys = true;
#endif

    // ── Lifecycle ────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
        RegisterDebugCommands();
    }

    private void Start()
    {
        ScheduleAutoLoadForScene(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!enableHotkeys) return;
        PollHotkeys();
#endif
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Load a scene by its exact Build Settings name.
    /// If the current scene entry allows it, <see cref="GameSaveManager"/> is asked to save
    /// before the transition.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[MySceneManager] LoadScene called with empty scene name.");
            return;
        }

        TryAutoSave();

        Debug.Log($"[MySceneManager] Loading scene: {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Load a scene by its index in <see cref="sceneEntries"/>.
    /// </summary>
    public void LoadScene(int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= sceneEntries.Count)
        {
            Debug.LogWarning($"[MySceneManager] Entry index {entryIndex} is out of range (count: {sceneEntries.Count}).");
            return;
        }

        LoadScene(sceneEntries[entryIndex].sceneName);
    }

    /// <summary>
    /// Reload the currently active scene.
    /// Triggers auto-save if enabled.
    /// </summary>
    public void ReloadCurrentScene() => LoadScene(SceneManager.GetActiveScene().name);

    /// <summary>
    /// Load the main menu scene defined by <see cref="mainMenuSceneName"/>.
    /// </summary>
    public void GoToMainMenu() => LoadScene(mainMenuSceneName);

    /// <summary>Returns the name of the currently active scene.</summary>
    public string GetCurrentSceneName() => SceneManager.GetActiveScene().name;

    // ── Internal ─────────────────────────────────────────────────────────────

    private void TryAutoSave()
    {
        if (!autoSaveOnLeaveGameScene) return;
        string currentSceneName = GetCurrentSceneName();
        if (!ShouldAutoSaveBeforeLeaving(currentSceneName)) return;

        if (GameSaveManager.Instance != null)
        {
            GameSaveManager.Instance.Save();
            Debug.Log("[MySceneManager] Auto-saved before scene transition.");
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ScheduleAutoLoadForScene(scene.name);
    }

    private void ScheduleAutoLoadForScene(string sceneName)
    {
        if (!ShouldAutoLoadAfterEntering(sceneName)) return;
        StartCoroutine(AutoLoadAfterSceneReady(sceneName));
    }

    private IEnumerator AutoLoadAfterSceneReady(string sceneName)
    {
        yield return null;

        if (SceneManager.GetActiveScene().name != sceneName) yield break;

        if (GameSaveManager.Instance == null)
        {
            Debug.LogWarning("[MySceneManager] GameSaveManager not found. Cannot auto-load.");
            yield break;
        }

        GameSaveManager.Instance.Load();
        Debug.Log($"[MySceneManager] Auto-loaded save after entering scene: {sceneName}");
    }

    private bool ShouldAutoSaveBeforeLeaving(string sceneName)
    {
        SceneEntry entry = FindSceneEntry(sceneName);
        if (entry != null)
            return entry.autoSaveBeforeLeaving;

        return sceneName != mainMenuSceneName;
    }

    private bool ShouldAutoLoadAfterEntering(string sceneName)
    {
        SceneEntry entry = FindSceneEntry(sceneName);
        return entry != null && entry.autoLoadAfterEntering;
    }

    private SceneEntry FindSceneEntry(string sceneName)
    {
        for (int i = 0; i < sceneEntries.Count; i++)
        {
            var entry = sceneEntries[i];
            if (entry != null && entry.sceneName == sceneName)
                return entry;
        }

        return null;
    }

    private void RegisterDebugCommands()
    {
        if (DebugConsoleManager.Instance == null) return;

        // Usage: scene <sceneName>   e.g. "scene GameScene"
        //        scene ls            — list all entries
        DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
            "scene",
            "Load a scene by name. Usage: scene <sceneName>  |  scene ls (list entries)",
            args =>
            {
                if (args.Length == 0)
                {
                    Debug.LogWarning("[scene] Usage: scene <sceneName>  |  scene ls");
                    return;
                }

                if (args[0].Equals("ls", StringComparison.OrdinalIgnoreCase))
                {
                    if (sceneEntries.Count == 0)
                    {
                        Debug.Log("[scene] No entries configured.");
                        return;
                    }
                    for (int i = 0; i < sceneEntries.Count; i++)
                    {
                        var e = sceneEntries[i];
                        Debug.Log($"  [{i}] {e.displayName} → \"{e.sceneName}\"");
                    }
                    return;
                }

                LoadScene(args[0]);
            }
        ));
    }

#if UNITY_EDITOR
    private void PollHotkeys()
    {
        foreach (var entry in sceneEntries)
        {
            if (entry.hotkey == KeyCode.None) continue;
            if (!Input.GetKeyDown(entry.hotkey)) continue;
            if (!IsModifierHeld(entry.modifier)) continue;

            Debug.Log($"[MySceneManager] Hotkey triggered → {entry.displayName}");
            LoadScene(entry.sceneName);
            break; // only one transition per frame
        }
    }

    private static bool IsModifierHeld(SceneHotkeyModifier modifier) =>
        modifier switch
        {
            SceneHotkeyModifier.Ctrl  => Input.GetKey(KeyCode.LeftControl)  || Input.GetKey(KeyCode.RightControl),
            SceneHotkeyModifier.Alt   => Input.GetKey(KeyCode.LeftAlt)      || Input.GetKey(KeyCode.RightAlt),
            SceneHotkeyModifier.Shift => Input.GetKey(KeyCode.LeftShift)    || Input.GetKey(KeyCode.RightShift),
            _                         => true   // SceneHotkeyModifier.None — no modifier required
        };
#endif
}
