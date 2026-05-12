using JackyUtility;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls a single NPC entry in the room-assignment panel.
/// Spawned and initialised by <see cref="NPCRoomAssignmentUIManager"/>.
/// </summary>
public class NPCRoomSlotUI : MonoBehaviour
{
    // ©¤©¤ Inspector ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI npcNameText;
    [SerializeField] private Image           portraitImage;
    [SerializeField] private TextMeshProUGUI roomStatusText;
    [SerializeField] private Button          assignRoomButton;

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

    // ©¤©¤ State ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    public Key_NPC NpcKey { get; private set; }

    private NPCRoomAssignmentManager _manager;

    // ©¤©¤ Init ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>Called by <see cref="NPCRoomAssignmentUIManager"/> after instantiation.</summary>
    public void Initialize(Key_NPC key, NPCRoomAssignmentManager manager)
    {
        NpcKey   = key;
        _manager = manager;

        assignRoomButton.onClick.AddListener(OnAssignRoomClicked);
        RefreshDisplay();
    }

    private void OnDestroy()
    {
        if (assignRoomButton != null)
            assignRoomButton.onClick.RemoveListener(OnAssignRoomClicked);
    }

    // ©¤©¤ Public ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

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

    // ©¤©¤ Private ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

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
