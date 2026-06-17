using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent in-game settings overlay. Instantiated by MyGameSystem and lives across all scenes.
///
/// Prefab structure (set up in Unity):
///   Canvas  (Screen Space – Overlay, high Sort Order)
///   ├── SettingsButton          ← always visible except in MainMenu scene
///   ├── Backdrop                ← full-screen raycast blocker, hidden when panel is closed
///   └── SettingsPanel           ← ~1/3 width, 5/6 height; hidden when closed
///       ├── SaveButton
///       ├── ExitToMainMenuButton
///       └── CloseButton
///
/// Panel toggle: Settings button click  OR  Escape key.
/// The panel is force-closed on every scene transition.
/// The Settings button is hidden while in the MainMenu scene.
/// </summary>
public class UISettingPanel : MonoBehaviour
{
    public static UISettingPanel Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Settings Button")]
    [SerializeField] private Button settingsButton;

    [Header("Panel")]
    [Tooltip("Full-screen Image that blocks clicks from passing through the panel.")]
    [SerializeField] private GameObject backdrop;
    [SerializeField] private GameObject settingsPanel;

    [Header("Panel Buttons")]
    [SerializeField] private Button saveButton;
    [SerializeField] private Button exitToMainMenuButton;
    [SerializeField] private Button closeButton;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("Scene")]
    [Tooltip("Settings button is hidden while this scene is active. Must match the MainMenu scene name exactly.")]
    [SerializeField] private string mainMenuSceneName = "S_MainMenu";

    // ── Runtime ───────────────────────────────────────────────────────────────

    private bool _isOpen;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (settingsButton        != null) settingsButton.onClick.AddListener(TogglePanel);
        if (saveButton            != null) saveButton.onClick.AddListener(OnSaveClicked);
        if (exitToMainMenuButton  != null) exitToMainMenuButton.onClick.AddListener(OnExitToMainMenuClicked);
        if (closeButton           != null) closeButton.onClick.AddListener(ClosePanel);

        SetPanelVisible(false);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // sceneLoaded does not fire for the scene that is already loaded at startup
        RefreshSettingsButtonVisibility(SceneManager.GetActiveScene().name);
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (settingsButton        != null) settingsButton.onClick.RemoveListener(TogglePanel);
        if (saveButton            != null) saveButton.onClick.RemoveListener(OnSaveClicked);
        if (exitToMainMenuButton  != null) exitToMainMenuButton.onClick.RemoveListener(OnExitToMainMenuClicked);
        if (closeButton           != null) closeButton.onClick.RemoveListener(ClosePanel);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            TogglePanel();
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_isOpen) SetPanelVisible(false);
        RefreshSettingsButtonVisibility(scene.name);
    }

    private void RefreshSettingsButtonVisibility(string sceneName)
    {
        if (settingsButton != null)
            settingsButton.gameObject.SetActive(sceneName != mainMenuSceneName);
    }

    private void TogglePanel()
    {
        // ESC should not open the panel while the settings button is hidden (e.g. in MainMenu)
        if (!_isOpen && !IsSettingsButtonVisible()) return;
        SetPanelVisible(!_isOpen);
    }

    private void SetPanelVisible(bool visible)
    {
        _isOpen = visible;
        if (backdrop      != null) backdrop.SetActive(visible);
        if (settingsPanel != null) settingsPanel.SetActive(visible);
    }

    private bool IsSettingsButtonVisible() =>
        settingsButton != null && settingsButton.gameObject.activeSelf;

    // ── Button Handlers ───────────────────────────────────────────────────────

    private void OnSaveClicked()
    {
        if (BuildSaveManager.Instance == null)
        {
            Debug.LogWarning("[UISettingPanel] BuildSaveManager not found. Cannot save.");
            return;
        }
        BuildSaveManager.Instance.Save();
        Debug.Log("[UISettingPanel] Game saved.");
    }

    private void OnExitToMainMenuClicked()
    {
        if (MySceneManager.Instance == null)
        {
            Debug.LogWarning("[UISettingPanel] MySceneManager not found. Cannot exit to main menu.");
            return;
        }
        SetPanelVisible(false);
        MySceneManager.Instance.GoToMainMenu();
    }

    private void ClosePanel() => SetPanelVisible(false);
}
