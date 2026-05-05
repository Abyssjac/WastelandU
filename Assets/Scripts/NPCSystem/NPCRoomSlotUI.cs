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
    [SerializeField] private TextMeshProUGUI   npcNameText;
    [SerializeField] private Image  portraitImage;
    [SerializeField] private TextMeshProUGUI   roomStatusText;
    [SerializeField] private Button assignRoomButton;

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
