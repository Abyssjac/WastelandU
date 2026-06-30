using JackyUtility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// NPC panel coordinator. It is opened by NPCInteractable, then routed through
/// AllUIManager so it participates in central UI ownership.
/// </summary>
public class NPCPanelUI : MonoBehaviour, IInteractablePanel, IGeneralPanelOwner
{
    public static NPCPanelUI Instance { get; private set; }

    private enum TabKind
    {
        Affinity,
        Room,
        Profile
    }

    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("NPC Header")]
    [SerializeField] private TextMeshProUGUI npcNameLabel;
    [SerializeField] private Image npcPortraitImage;

    [Header("Tab Buttons")]
    [SerializeField] private Button affinityTabButton;
    [SerializeField] private Button roomTabButton;
    [SerializeField] private Button profileTabButton;

    [Header("Close")]
    [SerializeField] private Button closeButton;

    [Header("Tabs")]
    [SerializeField] private NPCAffinityTabUI affinityTab;
    [SerializeField] private NPCRoomTabUI roomTab;
    [SerializeField] private NPCProfileTabUI profileTab;

    [Header("Stack Panels")]
    [Tooltip("Optional future room-selection panel. It must implement IGeneralPanelOwner.")]
    [SerializeField] private MonoBehaviour roomSelectionPanelOwner;

    [Header("Affinity")]
    [SerializeField] private float defaultOtherAffinityMax = 100f;

    private BasePanelInteractable _owner;
    private Key_NPC _currentNpcKey = Key_NPC.None;
    private TabKind _currentTab = TabKind.Affinity;
    private bool _isPanelVisible;
    private bool _isSuspendedByStackPanel;
    private bool _suppressOwnerNotification;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        CloseAllTabs();

        if (affinityTabButton != null)
            affinityTabButton.onClick.AddListener(() => OpenTab(TabKind.Affinity));
        if (roomTabButton != null)
            roomTabButton.onClick.AddListener(() => OpenTab(TabKind.Room));
        if (profileTabButton != null)
            profileTabButton.onClick.AddListener(() => OpenTab(TabKind.Profile));
        if (closeButton != null)
            closeButton.onClick.AddListener(RequestClose);

        if (affinityTab != null)
            affinityTab.OnDailyInteractRequested += HandleDailyInteractRequested;
        if (roomTab != null)
            roomTab.OnSelectRoomRequested += HandleSelectRoomRequested;
    }

    private void OnEnable()
    {
        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnNewDayStarted += HandleNewDayStarted;

        if (GridRoomManager.Instance != null)
            GridRoomManager.Instance.OnRoomFurnitureChanged += HandleRoomFurnitureChanged;

        if (NPCRoomAssignmentManager.Instance != null)
        {
            NPCRoomAssignmentManager.Instance.OnRoomAssigned += HandleRoomAssigned;
            NPCRoomAssignmentManager.Instance.OnRoomUnassigned += HandleRoomUnassigned;
        }
    }

    private void OnDisable()
    {
        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnNewDayStarted -= HandleNewDayStarted;

        if (GridRoomManager.Instance != null)
            GridRoomManager.Instance.OnRoomFurnitureChanged -= HandleRoomFurnitureChanged;

        if (NPCRoomAssignmentManager.Instance != null)
        {
            NPCRoomAssignmentManager.Instance.OnRoomAssigned -= HandleRoomAssigned;
            NPCRoomAssignmentManager.Instance.OnRoomUnassigned -= HandleRoomUnassigned;
        }
    }

    private void OnDestroy()
    {
        if (affinityTabButton != null)
            affinityTabButton.onClick.RemoveAllListeners();
        if (roomTabButton != null)
            roomTabButton.onClick.RemoveAllListeners();
        if (profileTabButton != null)
            profileTabButton.onClick.RemoveAllListeners();
        if (closeButton != null)
            closeButton.onClick.RemoveListener(RequestClose);

        if (affinityTab != null)
            affinityTab.OnDailyInteractRequested -= HandleDailyInteractRequested;
        if (roomTab != null)
            roomTab.OnSelectRoomRequested -= HandleSelectRoomRequested;

        if (Instance == this)
            Instance = null;
    }

    public void OpenPanel(BasePanelInteractable owner)
    {
        _owner = owner;
        _currentNpcKey = owner is NPCInteractable npcInteractable
            ? npcInteractable.NpcKey
            : Key_NPC.None;
        _currentTab = TabKind.Affinity;
        _isSuspendedByStackPanel = false;

        bool wasVisible = _isPanelVisible;

        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestOpen(this, PanelOpenType.Override);
        else
            OnPanelOpenRequested();

        if (wasVisible)
        {
            RefreshAllTabData();
            OpenTab(TabKind.Affinity);
        }
    }

    public void ClosePanel()
    {
        _suppressOwnerNotification = true;
        _isSuspendedByStackPanel = false;

        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestClose(this);

        if (_isPanelVisible || _currentNpcKey != Key_NPC.None || _owner != null)
            ForceCloseWithoutOwnerNotification();

        _suppressOwnerNotification = false;
    }

    public void OnPanelOpenRequested()
    {
        _isSuspendedByStackPanel = false;
        _isPanelVisible = true;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        RefreshAllTabData();
        OpenTab(_currentTab);
    }

    public void OnPanelCloseRequested()
    {
        HidePanelVisuals();

        if (_isSuspendedByStackPanel)
            return;

        BasePanelInteractable owner = _owner;
        ClearCurrentContext();

        if (!_suppressOwnerNotification)
            owner?.NotifyPanelClosed();
    }

    public void RequestClose()
    {
        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestClose(this);
        else
            OnPanelCloseRequested();
    }

    private void OpenTab(TabKind tab)
    {
        _currentTab = tab;

        if (affinityTab != null)
        {
            if (tab == TabKind.Affinity) affinityTab.Open();
            else affinityTab.Close();
        }

        if (roomTab != null)
        {
            if (tab == TabKind.Room) roomTab.Open();
            else roomTab.Close();
        }

        if (profileTab != null)
        {
            if (tab == TabKind.Profile) profileTab.Open();
            else profileTab.Close();
        }
    }

    private void CloseAllTabs()
    {
        affinityTab?.Close();
        roomTab?.Close();
        profileTab?.Close();
    }

    private void RefreshAllTabData()
    {
        NPCProperty property = ResolveProperty(_currentNpcKey);
        NPCBehaviour behaviour = ResolveCurrentBehaviour();
        NPCRuntimeData runtimeData = behaviour != null ? behaviour.RuntimeData : null;

        string displayName = property != null ? property.displayName : string.Empty;
        Sprite portrait = property != null ? property.portrait : null;

        if (npcNameLabel != null)
            npcNameLabel.text = displayName;

        if (npcPortraitImage != null)
            npcPortraitImage.sprite = portrait;

        if (affinityTab != null)
        {
            affinityTab.SetData(new NPCAffinityTabData
            {
                EnvironmentValue = runtimeData != null ? runtimeData.LivingEnvironmentAffinity : 0f,
                EnvironmentMax = property != null ? property.maxEnvAffinity : defaultOtherAffinityMax,
                DailyInteractionValue = runtimeData != null ? runtimeData.DailyInteractionAffinity : 0f,
                DailyInteractionMax = defaultOtherAffinityMax,
                FamiliarityValue = runtimeData != null ? runtimeData.FamiliarityAffinity : 0f,
                FamiliarityMax = defaultOtherAffinityMax,
                DailyInteractable = behaviour != null
                    && runtimeData != null
                    && !runtimeData.InteractedToday
            });
            affinityTab.Refresh();
        }

        if (roomTab != null)
        {
            Vector3Int stableId = Vector3Int.zero;
            bool hasRoom = NPCRoomAssignmentManager.Instance != null
                && NPCRoomAssignmentManager.Instance.TryGetAssignedRoom(_currentNpcKey, out stableId);

            roomTab.SetData(new NPCRoomTabData
            {
                HasRoom = hasRoom,
                StableId = hasRoom ? stableId : Vector3Int.zero
            });
            roomTab.Refresh();
        }

        if (profileTab != null)
        {
            profileTab.SetData(new NPCProfileTabData
            {
                DisplayName = displayName,
                Portrait = portrait
            });
            profileTab.Refresh();
        }
    }

    private void HandleDailyInteractRequested()
    {
        NPCBehaviour behaviour = ResolveCurrentBehaviour();
        if (behaviour == null)
        {
            Debug.LogWarning($"[NPCPanelUI] Could not find NPCBehaviour for {_currentNpcKey}.");
            return;
        }

        if (behaviour.RuntimeData.InteractedToday)
            return;

        behaviour.AddDailyInteractionAffinity();
        RefreshAllTabData();
    }

    private void HandleSelectRoomRequested()
    {
        if (_currentNpcKey == Key_NPC.None)
            return;

        IGeneralPanelOwner selectionOwner = roomSelectionPanelOwner as IGeneralPanelOwner;
        if (selectionOwner == null)
        {
            Debug.LogWarning("[NPCPanelUI] Room selection panel owner is not assigned or does not implement IGeneralPanelOwner.");
            return;
        }

        _isSuspendedByStackPanel = true;
        AllUIManager.Instance?.RequestOpen(selectionOwner, PanelOpenType.Stack);
    }

    private void HandleNewDayStarted(int newDay)
    {
        if (_isPanelVisible)
            RefreshAllTabData();
    }

    private void HandleRoomFurnitureChanged(RoomData room)
    {
        if (!_isPanelVisible)
            return;

        NPCBehaviour behaviour = ResolveCurrentBehaviour();
        if (behaviour == null || !behaviour.HasRoom)
            return;

        if (room.StableId == behaviour.AssignedRoomStableId)
            RefreshAllTabData();
    }

    private void HandleRoomAssigned(Key_NPC npcKey, Vector3Int stableId)
    {
        if (_isPanelVisible && npcKey == _currentNpcKey)
            RefreshAllTabData();
    }

    private void HandleRoomUnassigned(Key_NPC npcKey)
    {
        if (_isPanelVisible && npcKey == _currentNpcKey)
            RefreshAllTabData();
    }

    private void HidePanelVisuals()
    {
        _isPanelVisible = false;
        CloseAllTabs();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void ForceCloseWithoutOwnerNotification()
    {
        HidePanelVisuals();
        ClearCurrentContext();
    }

    private void ClearCurrentContext()
    {
        _owner = null;
        _currentNpcKey = Key_NPC.None;
        _currentTab = TabKind.Affinity;
        _isSuspendedByStackPanel = false;
    }

    private NPCBehaviour ResolveCurrentBehaviour()
    {
        if (_owner is NPCInteractable interactable && interactable.Behaviour != null)
            return interactable.Behaviour;

        if (NPCManager.Instance == null || _currentNpcKey == Key_NPC.None)
            return null;

        GameObject npcGo = NPCManager.Instance.GetSpawnedNPC(_currentNpcKey);
        return npcGo != null ? npcGo.GetComponent<NPCBehaviour>() : null;
    }

    private static NPCProperty ResolveProperty(Key_NPC key)
    {
        if (key == Key_NPC.None)
            return null;

        var dbMgr = PropertyDatabaseManager.Instance;
        if (dbMgr == null)
            return null;

        var db = dbMgr.GetDatabase<NPCDatabase>();
        return db?.GetByEnum(key);
    }
}

/// <summary>
/// Singleton NPC interaction panel.
/// Pre-placed in the scene Canvas. Opened by <see cref="NPCInteractable"/> and
/// implements <see cref="IInteractablePanel"/>.
///
/// Internal states:
///   MainMenu      — panel is visible and all buttons are available; ESC closes the panel.
///   InSubOperation — room-selection is active; ESC cancels room-selection but keeps panel open.
/// </summary>
#if false
internal class NPCPanelUILegacy : MonoBehaviour, IInteractablePanel
{
    // ── Singleton ─────────────────────────────────────────────────

    private static NPCPanelUILegacy LegacyInstance { get; set; }

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
        if (LegacyInstance != null && LegacyInstance != this)
        {
            Destroy(gameObject);
            return;
        }
        LegacyInstance = this;

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
        Debug.Log("NPC KEY Existed");
        if (NPCRoomAssignmentManager.Instance == null) return;
        Debug.Log("RoomAssignment Manager Exist");
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
#endif
