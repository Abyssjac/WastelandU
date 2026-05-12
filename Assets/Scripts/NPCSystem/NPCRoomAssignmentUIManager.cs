using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the NPC room-assignment panel: toggling visibility,
/// dynamically generating <see cref="NPCInfoSlotUI"/> entries, and
/// showing/hiding the Confirm button during room-selection mode.
/// </summary>
public class NPCRoomAssignmentUIManager : MonoBehaviour
{
    // ©¤©¤ Inspector ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Header("References")]
    [SerializeField] private NPCRoomAssignmentManager assignmentManager;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform  npcSlotContainer;
    [SerializeField] private GameObject npcSlotPrefab;

    [Header("Buttons")]
    [Tooltip("Confirm button ¡ª visible only while in room-selection mode.")]
    [SerializeField] private Button confirmButton;

    // ©¤©¤ State ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private readonly List<NPCInfoSlotUI> _slots = new List<NPCInfoSlotUI>();
    private bool _isPanelOpen;

    // ©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void Awake()
    {
        panelRoot.SetActive(false);
        confirmButton.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (assignmentManager == null) return;
        assignmentManager.OnSelectionModeEntered += HandleSelectionModeEntered;
        assignmentManager.OnSelectionModeExited  += HandleSelectionModeExited;
        assignmentManager.OnRoomAssigned         += HandleRoomAssigned;
        assignmentManager.OnRoomUnassigned       += HandleRoomUnassigned;
    }

    private void OnDisable()
    {
        if (assignmentManager == null) return;
        assignmentManager.OnSelectionModeEntered -= HandleSelectionModeEntered;
        assignmentManager.OnSelectionModeExited  -= HandleSelectionModeExited;
        assignmentManager.OnRoomAssigned         -= HandleRoomAssigned;
        assignmentManager.OnRoomUnassigned       -= HandleRoomUnassigned;
    }

    // ©¤©¤ Public ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>Toggle the NPC panel open/closed. Wire this to your panel toggle Button.</summary>
    public void TogglePanel()
    {
        if (_isPanelOpen) ClosePanel();
        else              OpenPanel();
    }

    /// <summary>Wire this to the Confirm Button's OnClick.</summary>
    public void OnConfirmButtonClicked()
    {
        assignmentManager?.ExitSelectRoomMode();
    }

    // ©¤©¤ Private ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void OpenPanel()
    {
        _isPanelOpen = true;
        panelRoot.SetActive(true);
        RefreshSlots();
    }

    private void ClosePanel()
    {
        _isPanelOpen = false;
        panelRoot.SetActive(false);

        // If we close the panel while in selection mode, gracefully exit.
        assignmentManager?.ExitSelectRoomMode();
    }

    private void RefreshSlots()
    {
        ClearSlots();
        if (NPCManager.Instance == null) return;

        foreach (var kvp in NPCManager.Instance.SpawnedNPCs)
        {
            GameObject    slotGO = Instantiate(npcSlotPrefab, npcSlotContainer);
            NPCInfoSlotUI slot   = slotGO.GetComponent<NPCInfoSlotUI>();
            if (slot != null)
            {
                slot.Initialize(kvp.Key, assignmentManager);
                _slots.Add(slot);
            }
        }
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i] != null) Destroy(_slots[i].gameObject);
        _slots.Clear();
    }

    // ©¤©¤ Event handlers ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void HandleSelectionModeEntered(Key_NPC npcKey)
    {
        confirmButton.gameObject.SetActive(true);

        // Disable the "Assign Room" button on every slot EXCEPT the one being assigned.
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].SetAssignButtonInteractable(_slots[i].NpcKey == npcKey);
    }

    private void HandleSelectionModeExited()
    {
        confirmButton.gameObject.SetActive(false);

        // Re-enable all slots and refresh their displayed room status.
        for (int i = 0; i < _slots.Count; i++)
        {
            _slots[i].SetAssignButtonInteractable(true);
            _slots[i].RefreshDisplay();
        }
    }

    private void HandleRoomAssigned(Key_NPC npcKey, Vector3Int stableId)
    {
        RefreshSlotForNPC(npcKey);
    }

    private void HandleRoomUnassigned(Key_NPC npcKey)
    {
        RefreshSlotForNPC(npcKey);
    }

    private void RefreshSlotForNPC(Key_NPC npcKey)
    {
        for (int i = 0; i < _slots.Count; i++)
        {
            if (_slots[i].NpcKey == npcKey)
            {
                _slots[i].RefreshDisplay();
                return;
            }
        }
    }
}
