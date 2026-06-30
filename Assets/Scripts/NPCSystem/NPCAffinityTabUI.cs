using System;
using JackyUtility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public struct NPCAffinityTabData
{
    public float EnvironmentValue;
    public float EnvironmentMax;
    public float DailyInteractionValue;
    public float DailyInteractionMax;
    public float FamiliarityValue;
    public float FamiliarityMax;
    public bool DailyInteractable;
}

/// <summary>
/// Draws the NPC affinity tab. Data is prepared by NPCPanelUI or a legacy owner.
/// </summary>
public class NPCAffinityTabUI : MonoBehaviour, INPCPanelTab
{
    [Header("Root")]
    [SerializeField] private GameObject tabRoot;

    [Header("Affinity Sliders")]
    [SerializeField] private Slider environmentAffinitySlider;
    [SerializeField] private Slider dailyInteractionSlider;
    [SerializeField] private Slider familiaritySlider;

    [Header("Affinity Labels")]
    [SerializeField] private TextMeshProUGUI environmentAffinityLabel;
    [SerializeField] private TextMeshProUGUI dailyInteractionLabel;
    [SerializeField] private TextMeshProUGUI familiarityLabel;

    [Header("Buttons")]
    [SerializeField] private Button dailyInteractButton;

    public event Action OnDailyInteractRequested;

    private NPCAffinityTabData _data;

    private void Awake()
    {
        if (tabRoot == null)
            tabRoot = gameObject;

        if (dailyInteractButton != null)
            dailyInteractButton.onClick.AddListener(HandleDailyInteractClicked);
    }

    private void OnDestroy()
    {
        if (dailyInteractButton != null)
            dailyInteractButton.onClick.RemoveListener(HandleDailyInteractClicked);
    }

    public void SetData(NPCAffinityTabData data)
    {
        _data = data;
    }

    public void Open()
    {
        if (tabRoot != null)
            tabRoot.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        if (tabRoot != null)
            tabRoot.SetActive(false);
    }

    public void Refresh()
    {
        SetSlider(
            environmentAffinitySlider,
            environmentAffinityLabel,
            0f,
            _data.EnvironmentMax,
            _data.EnvironmentValue);

        SetSlider(
            dailyInteractionSlider,
            dailyInteractionLabel,
            0f,
            _data.DailyInteractionMax,
            _data.DailyInteractionValue);

        SetSlider(
            familiaritySlider,
            familiarityLabel,
            0f,
            _data.FamiliarityMax,
            _data.FamiliarityValue);

        if (dailyInteractButton != null)
            dailyInteractButton.interactable = _data.DailyInteractable;
    }

    private void HandleDailyInteractClicked()
    {
        OnDailyInteractRequested?.Invoke();
    }

    private static void SetSlider(Slider slider, TextMeshProUGUI label, float min, float max, float value)
    {
        max = Mathf.Max(min, max);
        value = Mathf.Clamp(value, min, max);

        if (slider != null)
        {
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
        }

        if (label != null)
            label.text = $"{value:0.#}/{max:0.#}";
    }
}

/// <summary>
/// Controls a single NPC entry in the room-assignment panel.
/// Spawned and initialised by <see cref="NPCRoomAssignmentUIManager"/>.
/// </summary>
#if false
internal class NPCAffinityTabUILegacy : MonoBehaviour
{
    // ���� Inspector ��������������������������������������������������������������������������������������������������������������������������
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private Image           portraitImage;
    [SerializeField] private TextMeshProUGUI roomStatusText;
    [SerializeField] private Button          assignRoomButton;
    [SerializeField] private Button          dailyInteractButton;

    [Header("Affinity Sliders")]
    [SerializeField] private Slider envAffinitySlider;
    [SerializeField] private Slider dailyInteractionSlider;
    [SerializeField] private Slider familiaritySlider;

    [Header("Affinity Labels")]
    [SerializeField] private TextMeshProUGUI envAffinityLabel;
    [SerializeField] private TextMeshProUGUI dailyInteractionLabel;
    [SerializeField] private TextMeshProUGUI familiarityLabel;

    [Tooltip("Max value used for Daily Interaction and Familiarity sliders.")]
    [SerializeField] private float maxOtherAffinity = 100f;

    // ���� State ����������������������������������������������������������������������������������������������������������������������������������
    public Key_NPC NpcKey { get; private set; }

    private NPCRoomAssignmentManager _manager;

    // ���� Init ������������������������������������������������������������������������������������������������������������������������������������

    /// <summary>Called by <see cref="NPCRoomAssignmentUIManager"/> after instantiation.</summary>
    public void Initialize(Key_NPC key, NPCRoomAssignmentManager manager)
    {
        NpcKey   = key;
        _manager = manager;

        assignRoomButton.onClick.AddListener(OnAssignRoomClicked);

        if (dailyInteractButton != null)
            dailyInteractButton.onClick.AddListener(OnDailyInteractClicked);

        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnNewDayStarted += HandleNewDayStarted;

        if (GridRoomManager.Instance != null)
            GridRoomManager.Instance.OnRoomFurnitureChanged += HandleRoomFurnitureChanged;

        RefreshDisplay();
        RefreshDailyInteractButton();
    }

    private void OnDestroy()
    {
        if (assignRoomButton != null)
            assignRoomButton.onClick.RemoveListener(OnAssignRoomClicked);

        if (dailyInteractButton != null)
            dailyInteractButton.onClick.RemoveListener(OnDailyInteractClicked);

        if (DayNightManager.Instance != null)
            DayNightManager.Instance.OnNewDayStarted -= HandleNewDayStarted;

        if (GridRoomManager.Instance != null)
            GridRoomManager.Instance.OnRoomFurnitureChanged -= HandleRoomFurnitureChanged;
    }

    // ���� Public ��������������������������������������������������������������������������������������������������������������������������������

    /// <summary>Refresh name, portrait, and room-status text from current runtime state.</summary>
    public void RefreshDisplay()
    {
        NPCProperty prop = ResolveProperty();

        if (npcNameText != null)
            npcNameText.text = prop != null && !string.IsNullOrEmpty(prop.displayName)
                ? prop.displayName
                : NpcKey.ToString();

        if (portraitImage != null)
            portraitImage.sprite = prop != null ? prop.portrait : null;

        if (roomStatusText != null)
        {
            if (_manager != null && _manager.TryGetAssignedRoom(NpcKey, out Vector3Int stableId))
                roomStatusText.text = $"Room {stableId}";
            else
                roomStatusText.text = "Unassigned";
        }

        RefreshAffinitySliders(prop);
    }

    private void RefreshAffinitySliders(NPCProperty prop)
    {
        NPCRuntimeData data = null;
        if (NPCManager.Instance != null)
        {
            GameObject npcGo = NPCManager.Instance.GetSpawnedNPC(NpcKey);
            if (npcGo != null)
                data = npcGo.GetComponent<NPCBehaviour>()?.RuntimeData;
        }

        float envMax = prop != null ? prop.maxEnvAffinity : maxOtherAffinity;

        SetSlider(envAffinitySlider,      envAffinityLabel,      0f, envMax,           data?.LivingEnvironmentAffinity ?? 0f);
        SetSlider(dailyInteractionSlider, dailyInteractionLabel, 0f, maxOtherAffinity, data?.DailyInteractionAffinity  ?? 0f);
        SetSlider(familiaritySlider,      familiarityLabel,      0f, maxOtherAffinity, data?.FamiliarityAffinity        ?? 0f);
    }

    private static void SetSlider(Slider slider, TextMeshProUGUI label, float min, float max, float value)
    {
        if (slider != null)
        {
            slider.minValue = min;
            slider.maxValue = max;
            slider.value    = value;
        }
        if (label != null)
            label.text = $"{value}/{max}";
    }

    /// <summary>Enable or disable the Assign Room button.</summary>
    public void SetAssignButtonInteractable(bool interactable)
    {
        if (assignRoomButton != null)
            assignRoomButton.interactable = interactable;
    }

    // ���� Private ������������������������������������������������������������������������������������������������������������������������������

    private void OnDailyInteractClicked()
    {
        NPCBehaviour behaviour = null;
        if (NPCManager.Instance != null)
        {
            GameObject npcGo = NPCManager.Instance.GetSpawnedNPC(NpcKey);
            if (npcGo != null)
                behaviour = npcGo.GetComponent<NPCBehaviour>();
        }

        if (behaviour == null)
        {
            Debug.LogWarning($"[NPCInfoSlotUI] Could not find NPCBehaviour for {NpcKey}.");
            return;
        }

        if (behaviour.RuntimeData.InteractedToday) return;

        behaviour.AddDailyInteractionAffinity();
        RefreshDailyInteractButton();
        RefreshDisplay();
    }

    private void HandleNewDayStarted(int newDay)
    {
        RefreshDailyInteractButton();
        RefreshDisplay();
    }

    private void HandleRoomFurnitureChanged(RoomData room)
    {
        // Only refresh when the changed room belongs to this NPC
        if (NPCManager.Instance == null) return;
        GameObject npcGo = NPCManager.Instance.GetSpawnedNPC(NpcKey);
        if (npcGo == null) return;
        NPCBehaviour behaviour = npcGo.GetComponent<NPCBehaviour>();
        if (behaviour == null || !behaviour.HasRoom) return;
        if (room.StableId != behaviour.AssignedRoomStableId) return;

        RefreshDisplay();
    }

    private void RefreshDailyInteractButton()
    {
        if (dailyInteractButton == null) return;

        bool interactedToday = false;
        if (NPCManager.Instance != null)
        {
            GameObject npcGo = NPCManager.Instance.GetSpawnedNPC(NpcKey);
            if (npcGo != null)
                interactedToday = npcGo.GetComponent<NPCBehaviour>()?.RuntimeData.InteractedToday ?? false;
        }
        dailyInteractButton.interactable = !interactedToday;
    }

    private void OnAssignRoomClicked()
    {
        _manager?.EnterSelectRoomMode(NpcKey);
    }

    private NPCProperty ResolveProperty()
    {
        var dbMgr = PropertyDatabaseManager.Instance;
        if (dbMgr == null) return null;

        var db = dbMgr.GetDatabase<NPCDatabase>();
        return db?.GetByEnum(NpcKey);
    }
}
#endif
