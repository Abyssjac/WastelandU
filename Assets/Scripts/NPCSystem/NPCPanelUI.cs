using JackyUtility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Singleton NPC interaction panel.
/// Pre-placed in the scene Canvas. Opened by <see cref="NPCInteractable"/> and
/// implements <see cref="IInteractablePanel"/>.
///
/// Internal states:
///   MainMenu      — panel is visible and all buttons are available; ESC closes the panel.
///   InSubOperation — room-selection is active; ESC cancels room-selection but keeps panel open.
/// </summary>
public class NPCPanelUI : MonoBehaviour, IInteractablePanel
{
    // ── Singleton ─────────────────────────────────────────────────

    public static NPCPanelUI Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────

    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("NPC Info")]
    [SerializeField] private TextMeshProUGUI npcNameLabel;
    [SerializeField] private Image           npcPortraitImage;

    [Header("Buttons")]
    [SerializeField] private Button assignRoomButton;
    [SerializeField] private Button dailyInteractButton;
    [SerializeField] private Button placeholderButtonC;
    [SerializeField] private Button placeholderButtonD;

    [Header("Input")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    // ── Internal State ────────────────────────────────────────────

    private enum PanelState { Closed, MainMenu, InSubOperation }

    private PanelState          _state  = PanelState.Closed;
    private BasePanelInteractable _owner;
    private Key_NPC             _currentNpcKey = Key_NPC.None;

    // ─────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        panelRoot.SetActive(false);

        // Wire buttons
        if (assignRoomButton    != null) assignRoomButton.onClick.AddListener(OnAssignRoomClicked);
        if (dailyInteractButton != null) dailyInteractButton.onClick.AddListener(OnDailyInteractClicked);
    }

    private void OnEnable()
    {
        if (NPCRoomAssignmentManager.Instance != null)
            NPCRoomAssignmentManager.Instance.OnSelectionModeExited += HandleSelectionModeExited;
    }

    private void OnDisable()
    {
        if (NPCRoomAssignmentManager.Instance != null)
            NPCRoomAssignmentManager.Instance.OnSelectionModeExited -= HandleSelectionModeExited;
    }

    private void Update()
    {
        if (_state == PanelState.Closed) return;

        if (Input.GetKeyDown(closeKey))
        {
            if (_state == PanelState.MainMenu)
            {
                // ESC in main menu → fully close, release interact state
                ExitPanel();
            }
            else if (_state == PanelState.InSubOperation)
            {
                // ESC during room-selection → cancel selection, stay in panel, also update the potential pending selection by using ExitSelecctRoomMode instead of CancelSelectRoomMode
                NPCRoomAssignmentManager.Instance?.ExitSelectRoomMode();
                // HandleSelectionModeExited will transition us back to MainMenu
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // IInteractablePanel
    // ─────────────────────────────────────────────────────────────

    public void OpenPanel(BasePanelInteractable owner)
    {
        _owner = owner;
        _currentNpcKey = owner is NPCInteractable npcInteractable
            ? npcInteractable.NpcKey
            : Key_NPC.None;

        panelRoot.SetActive(true);
        _state = PanelState.MainMenu;

        RefreshNPCInfo();
        RefreshButtons();
    }

    /// <summary>
    /// Forcibly close without notifying the owner (external cleanup only).
    /// Cancels any active sub-operation (e.g. room selection) before closing.
    /// </summary>
    public void ClosePanel()
    {
        // Set state first so HandleSelectionModeExited is a no-op if it fires
        _state = PanelState.Closed;

        NPCRoomAssignmentManager.Instance?.CancelSelectRoomMode();

        _owner = null;
        _currentNpcKey = Key_NPC.None;
        panelRoot.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────────
    // Button Handlers
    // ─────────────────────────────────────────────────────────────

    private void OnAssignRoomClicked()
    {
        if (_currentNpcKey == Key_NPC.None) return;
        if (NPCRoomAssignmentManager.Instance == null) return;

        _state = PanelState.InSubOperation;
        NPCRoomAssignmentManager.Instance.EnterSelectRoomMode(_currentNpcKey);
    }

    private void OnDailyInteractClicked()
    {
        if (_currentNpcKey == Key_NPC.None) return;
        if (NPCManager.Instance == null) return;

        GameObject npcGo = NPCManager.Instance.GetSpawnedNPC(_currentNpcKey);
        NPCBehaviour behaviour = npcGo != null ? npcGo.GetComponent<NPCBehaviour>() : null;

        if (behaviour == null)
        {
            Debug.LogWarning($"[NPCPanelUI] Could not find NPCBehaviour for {_currentNpcKey}.");
            return;
        }

        if (behaviour.RuntimeData.InteractedToday) return;

        behaviour.AddDailyInteractionAffinity();
        RefreshButtons();
    }

    // ─────────────────────────────────────────────────────────────
    // Event Handlers
    // ─────────────────────────────────────────────────────────────

    private void HandleSelectionModeExited()
    {
        if (_state != PanelState.InSubOperation) return;

        _state = PanelState.MainMenu;
        RefreshButtons();
    }

    // ─────────────────────────────────────────────────────────────
    // Refresh
    // ─────────────────────────────────────────────────────────────

    private void RefreshNPCInfo()
    {
        NPCProperty prop = ResolveProperty(_currentNpcKey);

        if (npcNameLabel != null)
            npcNameLabel.text = prop != null && !string.IsNullOrEmpty(prop.displayName)
                ? prop.displayName
                : _currentNpcKey.ToString();

        if (npcPortraitImage != null)
            npcPortraitImage.sprite = prop != null ? prop.portrait : null;
    }

    private void RefreshButtons()
    {
        RefreshDailyInteractButton();
        // Assign-room button is always interactable in MainMenu
        if (assignRoomButton != null)
            assignRoomButton.interactable = true;
    }

    private void RefreshDailyInteractButton()
    {
        if (dailyInteractButton == null) return;
        if (_currentNpcKey == Key_NPC.None)
        {
            dailyInteractButton.interactable = false;
            return;
        }

        bool interactedToday = false;
        if (NPCManager.Instance != null)
        {
            GameObject npcGo = NPCManager.Instance.GetSpawnedNPC(_currentNpcKey);
            interactedToday = npcGo != null
                && (npcGo.GetComponent<NPCBehaviour>()?.RuntimeData.InteractedToday ?? false);
        }

        dailyInteractButton.interactable = !interactedToday;
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    /// <summary>Cleanly exit the panel and notify the owning interactable.</summary>
    private void ExitPanel()
    {
        _state = PanelState.Closed;
        panelRoot.SetActive(false);

        var owner = _owner;
        _owner         = null;
        _currentNpcKey = Key_NPC.None;

        // Notify last — owner may trigger a scan that re-opens things
        owner?.NotifyPanelClosed();
    }

    private static NPCProperty ResolveProperty(Key_NPC key)
    {
        var dbMgr = PropertyDatabaseManager.Instance;
        if (dbMgr == null) return null;
        var db = dbMgr.GetDatabase<NPCDatabase>();
        return db?.GetByEnum(key);
    }
}
