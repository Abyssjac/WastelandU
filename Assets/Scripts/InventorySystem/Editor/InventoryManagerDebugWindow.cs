using System.Collections.Generic;
using JackyUtility;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Play-mode debug window for inspecting and mutating the runtime inventory through its normal
/// container APIs. Successful operations notify InventoryManager listeners.
/// </summary>
public class InventoryManagerDebugWindow : DebugEditorWindow<InventoryManager>
{
    private sealed class SlotDraft
    {
        public Key_ItemDefinitionPP itemKey;
        public int count;
        public bool isDirty;

        public void ResetFrom(InventorySlot slot)
        {
            itemKey = slot != null ? slot.ItemEnum : Key_ItemDefinitionPP.None;
            count = slot != null ? Mathf.Max(0, slot.ItemCount) : 0;
            isDirty = false;
        }
    }

    private readonly Dictionary<int, SlotDraft> slotDrafts = new Dictionary<int, SlotDraft>();
    private readonly Dictionary<Key_ItemDefinitionPP, int> itemTotals =
        new Dictionary<Key_ItemDefinitionPP, int>();

    private Key_ItemDefinitionPP quickItemKey = Key_ItemDefinitionPP.None;
    private int quickAmount = 1;
    private bool showEmptySlots = true;
    private Vector2 slotScrollPosition;
    private string lastResult = "No operation has been run.";
    private MessageType lastResultType = MessageType.None;

    [MenuItem("Jacky Tools/Inventory Manager")]
    public static void ShowWindow()
    {
        GetWindow<InventoryManagerDebugWindow>("Inventory Manager Debug").Show();
    }

    protected override void DrawContent()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox(
                "Enter Play Mode to inspect or change the runtime InventoryManager. Edit Mode is intentionally read-only.",
                MessageType.Info);
            return;
        }

        InventoryManager manager = Target;
        InventoryContainer container = manager.Inventory;
        if (container == null)
        {
            EditorGUILayout.HelpBox("InventoryContainer is not available.", MessageType.Warning);
            return;
        }

        DrawSummary(container);
        DrawQuickOperation(manager);
        DrawSlotEditor(container);
    }

    private void DrawSummary(InventoryContainer container)
    {
        Header("Runtime Summary");
        Row("Slots", $"{container.UsedSlots} / {container.MaxSlots}");
        Row("Free Slots", container.FreeSlots.ToString());
        Row("Use Max Stack", container.UseMaxStack.ToString());
        Row("Global Max Stack", container.MaxStackCount.ToString());

        BuildItemTotals(container);
        EditorGUILayout.Space(2);
        Header("Current Item Totals");

        if (itemTotals.Count == 0)
        {
            EditorGUILayout.HelpBox("The inventory is empty.", MessageType.None);
            return;
        }

        var keys = new List<Key_ItemDefinitionPP>(itemTotals.Keys);
        keys.Sort((left, right) => left.CompareTo(right));

        for (int i = 0; i < keys.Count; i++)
        {
            Key_ItemDefinitionPP key = keys[i];
            Row(GetItemLabel(key), itemTotals[key].ToString());
        }
    }

    private void DrawQuickOperation(InventoryManager manager)
    {
        EditorGUILayout.Space(6);
        Header("Quick Add / Remove");

        quickItemKey = (Key_ItemDefinitionPP)EditorGUILayout.EnumPopup("Item Key", quickItemKey);
        quickAmount = Mathf.Max(1, EditorGUILayout.IntField("Amount", quickAmount));

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(quickItemKey == Key_ItemDefinitionPP.None))
        {
            if (GUILayout.Button("Add", GUILayout.Height(22)))
            {
                bool success = manager.TryAddItem(quickItemKey, quickAmount, out string failReason);
                SetResult(success, success
                    ? $"Added {quickItemKey} x{quickAmount}."
                    : $"Could not add {quickItemKey} x{quickAmount}: {failReason}");
            }

            if (GUILayout.Button("Remove", GUILayout.Height(22)))
            {
                bool success = manager.TryRemoveItem(quickItemKey, quickAmount, out string failReason);
                SetResult(success, success
                    ? $"Removed {quickItemKey} x{quickAmount}."
                    : $"Could not remove {quickItemKey} x{quickAmount}: {failReason}");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Quick operations use InventoryManager.TryAddItem / TryRemoveItem, so successful changes notify normal inventory listeners.",
            MessageType.None);
        EditorGUILayout.HelpBox(lastResult, lastResultType);
    }

    private void DrawSlotEditor(InventoryContainer container)
    {
        EditorGUILayout.Space(6);
        Header("Container Slots");
        showEmptySlots = EditorGUILayout.ToggleLeft("Show Empty Slots", showEmptySlots);
        EditorGUILayout.HelpBox(
            "Apply writes the selected Item Key and Count into one exact slot. Item Key None or Count 0 clears that slot. " +
            "These direct slot edits are debug-only and do not emit InventoryManager's detailed item-delta event.",
            MessageType.None);

        IReadOnlyList<InventorySlot> slots = container.Slots;
        slotScrollPosition = EditorGUILayout.BeginScrollView(slotScrollPosition, GUILayout.MinHeight(250));

        for (int index = 0; index < slots.Count; index++)
        {
            InventorySlot slot = slots[index];
            if (slot == null)
                continue;

            if (!showEmptySlots && slot.IsEmpty)
                continue;

            DrawSlot(container, index, slot);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawSlot(InventoryContainer container, int index, InventorySlot slot)
    {
        SlotDraft draft = GetDraft(index, slot);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Slot {index}", EditorStyles.boldLabel, GUILayout.Width(62));
        EditorGUILayout.LabelField(slot.IsEmpty
            ? "Empty"
            : $"Current: {GetItemLabel(slot.ItemEnum)} x{slot.ItemCount}");
        EditorGUILayout.EndHorizontal();

        EditorGUI.BeginChangeCheck();
        Key_ItemDefinitionPP editedKey = (Key_ItemDefinitionPP)EditorGUILayout.EnumPopup("Item Key", draft.itemKey);
        int editedCount = Mathf.Max(0, EditorGUILayout.IntField("Count", draft.count));
        if (EditorGUI.EndChangeCheck())
        {
            draft.itemKey = editedKey;
            draft.count = editedCount;
            draft.isDirty = true;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply", GUILayout.Height(20)))
            ApplySlot(container, index, draft);

        if (GUILayout.Button("Revert", GUILayout.Height(20)))
            draft.ResetFrom(slot);

        if (GUILayout.Button("Clear", GUILayout.Height(20)))
        {
            bool changed = container.EmptySlotAtIndex(index);
            slotDrafts.Remove(index);
            SetResult(changed, changed
                ? $"Cleared slot {index}."
                : $"Slot {index} is already empty.");
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private SlotDraft GetDraft(int index, InventorySlot slot)
    {
        if (!slotDrafts.TryGetValue(index, out SlotDraft draft))
        {
            draft = new SlotDraft();
            draft.ResetFrom(slot);
            slotDrafts.Add(index, draft);
            return draft;
        }

        if (!draft.isDirty
            && (draft.itemKey != slot.ItemEnum || draft.count != Mathf.Max(0, slot.ItemCount)))
        {
            draft.ResetFrom(slot);
        }

        return draft;
    }

    private void ApplySlot(InventoryContainer container, int index, SlotDraft draft)
    {
        if (draft.itemKey == Key_ItemDefinitionPP.None || draft.count == 0)
        {
            bool changed = container.EmptySlotAtIndex(index);
            slotDrafts.Remove(index);
            SetResult(changed, changed
                ? $"Cleared slot {index}."
                : $"Slot {index} is already empty.");
            return;
        }

        bool success = container.TrySetSlotAtIndex(index, draft.itemKey, draft.count, out string failReason);
        if (success)
            slotDrafts.Remove(index);

        SetResult(success, success
            ? $"Set slot {index} to {draft.itemKey} x{draft.count}."
            : $"Could not set slot {index}: {failReason}");
    }

    private void BuildItemTotals(InventoryContainer container)
    {
        itemTotals.Clear();

        IReadOnlyList<InventorySlot> slots = container.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.IsEmpty || slot.ItemEnum == Key_ItemDefinitionPP.None)
                continue;

            itemTotals.TryGetValue(slot.ItemEnum, out int total);
            itemTotals[slot.ItemEnum] = total + slot.ItemCount;
        }
    }

    private string GetItemLabel(Key_ItemDefinitionPP itemKey)
    {
        if (itemKey == Key_ItemDefinitionPP.None)
            return "None";

        InventoryManager manager = Target;
        ItemDefinitionSO item = manager != null && manager.ItemDatabase != null
            ? manager.ItemDatabase.GetByEnum(itemKey)
            : null;

        return item != null && !string.IsNullOrWhiteSpace(item.DisplayName)
            ? $"{item.DisplayName} ({itemKey})"
            : itemKey.ToString();
    }

    private void SetResult(bool success, string message)
    {
        lastResult = message;
        lastResultType = success ? MessageType.Info : MessageType.Warning;
        Repaint();
    }
}
