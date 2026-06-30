using System.Collections.Generic;
using JackyUtility;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the NPC room-assignment panel: toggling visibility,
/// dynamically generating <see cref="NPCAffinityTabUI"/> entries, and
/// showing/hiding the Confirm button during room-selection mode.
/// </summary>
public class NPCRoomAssignmentUIManager : MonoBehaviour
{
    // ���� Inspector ��������������������������������������������������������������������������������������������������������������������������
    [Header("References")]
    [SerializeField] private NPCRoomAssignmentManager assignmentManager;

    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform  npcSlotContainer;
    [SerializeField] private GameObject npcSlotPrefab;

    [Header("Buttons")]
    [Tooltip("Confirm button �� visible only while in room-selection mode.")]
    [SerializeField] private Button confirmButton;

    // ���� State ����������������������������������������������������������������������������������������������������������������������������������
    private class SlotBinding
    {
        public Key_NPC Key;
        public NPCAffinityTabUI Tab;
    }

    private readonly List<SlotBinding> _slots = new List<SlotBinding>();
    private bool _isPanelOpen;

    // ���� Lifecycle ��������������������������������������������������������������������������������������������������������������������������
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

    // ���� Public ��������������������������������������������������������������������������������������������������������������������������������

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

    // ���� Private ������������������������������������������������������������������������������������������������������������������������������

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
            NPCAffinityTabUI slot = slotGO.GetComponent<NPCAffinityTabUI>();
            if (slot != null)
            {
                slot.SetData(BuildAffinityData(kvp.Key));
                slot.Open();
                _slots.Add(new SlotBinding { Key = kvp.Key, Tab = slot });
            }
        }
    }

    private void ClearSlots()
    {
        for (int i = 0; i < _slots.Count; i++)
            if (_slots[i]?.Tab != null) Destroy(_slots[i].Tab.gameObject);
        _slots.Clear();
    }

    // ���� Event handlers ����������������������������������������������������������������������������������������������������������������

    private void HandleSelectionModeEntered(Key_NPC npcKey)
    {
        confirmButton.gameObject.SetActive(true);

        // Legacy manager only refreshes visible affinity slots now.
        RefreshSlotForNPC(npcKey);
    }

    private void HandleSelectionModeExited()
    {
        confirmButton.gameObject.SetActive(false);

        // Re-enable all slots and refresh their displayed room status.
        for (int i = 0; i < _slots.Count; i++)
        {
            RefreshSlot(_slots[i]);
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
            if (_slots[i].Key == npcKey)
            {
                RefreshSlot(_slots[i]);
                return;
            }
        }
    }

    private void RefreshSlot(SlotBinding slot)
    {
        if (slot?.Tab == null) return;

        slot.Tab.SetData(BuildAffinityData(slot.Key));
        slot.Tab.Refresh();
    }

    private NPCAffinityTabData BuildAffinityData(Key_NPC npcKey)
    {
        NPCProperty property = ResolveProperty(npcKey);
        NPCRuntimeData runtimeData = null;

        if (NPCManager.Instance != null)
        {
            GameObject npcGo = NPCManager.Instance.GetSpawnedNPC(npcKey);
            if (npcGo != null)
                runtimeData = npcGo.GetComponent<NPCBehaviour>()?.RuntimeData;
        }

        return new NPCAffinityTabData
        {
            EnvironmentValue = runtimeData != null ? runtimeData.LivingEnvironmentAffinity : 0f,
            EnvironmentMax = property != null ? property.maxEnvAffinity : 100f,
            DailyInteractionValue = runtimeData != null ? runtimeData.DailyInteractionAffinity : 0f,
            DailyInteractionMax = 100f,
            FamiliarityValue = runtimeData != null ? runtimeData.FamiliarityAffinity : 0f,
            FamiliarityMax = 100f,
            DailyInteractable = false
        };
    }

    private static NPCProperty ResolveProperty(Key_NPC npcKey)
    {
        var dbMgr = PropertyDatabaseManager.Instance;
        if (dbMgr == null) return null;

        var db = dbMgr.GetDatabase<NPCDatabase>();
        return db?.GetByEnum(npcKey);
    }
}
