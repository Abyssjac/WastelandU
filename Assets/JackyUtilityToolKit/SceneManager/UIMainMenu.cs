using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Drives the main menu buttons.
/// Attach to the Canvas root (or any persistent GameObject) in the MainMenu scene.
///
/// Inspector wiring:
///   • Start Button   → calls MySceneManager.LoadScene(gameSceneName)
///   • Settings Button → placeholder; wired to OnSettingsClicked() for future use
///   • Exit Button    → calls Application.Quit() (or stops Play Mode in the Editor)
///
/// Requirements:
///   • MySceneManager must be alive (instantiated by MyGameSystem).
/// </summary>
public class UIMainMenu : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Buttons")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    [Header("Scene")]
    [Tooltip("Exact scene name to load when Start is pressed. Must match File → Build Settings.")]
    [SerializeField] private string gameSceneName = "S_MainGame";

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);
    }

    private void OnDestroy()
    {
        if (startButton != null)
            startButton.onClick.RemoveListener(OnStartClicked);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsClicked);

        if (exitButton != null)
            exitButton.onClick.RemoveListener(OnExitClicked);
    }

    // ── Button Handlers ───────────────────────────────────────────────────────

    private void OnStartClicked()
    {
        if (MySceneManager.Instance == null)
        {
            Debug.LogWarning("[UIMainMenu] MySceneManager not found. Cannot load game scene.");
            return;
        }

        MySceneManager.Instance.LoadScene(gameSceneName);
    }

    /// <summary>
    /// Placeholder — wire up Settings panel logic here when ready.
    /// </summary>
    private void OnSettingsClicked()
    {
        Debug.Log("[UIMainMenu] Settings button pressed. (not yet implemented)");
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
