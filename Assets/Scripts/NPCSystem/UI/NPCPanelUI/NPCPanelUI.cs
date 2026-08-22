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
    [SerializeField] private NPCRoomSelectionPanelUI roomSelectionPanel;

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

        if (roomSelectionPanel == null)
        {
            Debug.LogWarning("[NPCPanelUI] Room selection panel is not assigned.");
            return;
        }

        roomSelectionPanel.Prepare(_currentNpcKey, ResolveDisplayName(_currentNpcKey));
        _isSuspendedByStackPanel = true;

        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestOpen(roomSelectionPanel, PanelOpenType.Stack);
        else
            roomSelectionPanel.OnPanelOpenRequested();
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

        GameObject npcGo = NPCManager.Instance.GetRegisteredNPC(_currentNpcKey);
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

    private static string ResolveDisplayName(Key_NPC key)
    {
        NPCProperty property = ResolveProperty(key);
        return property != null && !string.IsNullOrEmpty(property.displayName)
            ? property.displayName
            : key.ToString();
    }
}
