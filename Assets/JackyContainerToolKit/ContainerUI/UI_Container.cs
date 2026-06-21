using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// Describes the visual state of a slot.
/// <see cref="Default"/> is the normal "has item" state.
/// All other values correspond to a dedicated overlay GameObject configured on <see cref="UI_ContainerSlot"/>.
/// </summary>
public enum SlotState
{
    Default = 0,
    Empty   = 1,
    SoldOut = 2,
    Locked  = 3,
}

/// <summary>
/// Maps a <see cref="SlotState"/> value to the overlay GameObject that should be shown for it.
/// Configure these on the <see cref="UI_ContainerSlot"/> prefab in the Inspector.
/// </summary>
[Serializable]
public struct SlotStateEntry
{
    public SlotState state;
    public GameObject overlay;
}

/// <summary>
/// Pure-data class describing what a single slot should look like.
/// Prepared by the caller -- this UI knows nothing about databases or properties.
/// The <see cref="state"/> field drives which overlay is shown on the slot.
/// Set it explicitly; the UI layer does not infer state from other fields.
/// </summary>
[Serializable]
public class SlotDisplayData
{
    public Sprite icon;
    public Color iconColor;
    public int count;
    public string labelText;
    public SlotState state;

    public bool IsEmpty => state == SlotState.Empty;

    public SlotDisplayData(Sprite icon, Color iconColor, int count, string labelText = "", SlotState state = SlotState.Default)
    {
        this.icon = icon;
        this.iconColor = iconColor;
        this.count = count;
        this.labelText = labelText;
        this.state = state;
    }

    public static SlotDisplayData Empty => new SlotDisplayData(null, Color.clear, 0, "", SlotState.Empty);
}

/// <summary>
/// Implement this on any property ScriptableObject that can be displayed in a slot UI.
/// The container system will call <see cref="ToSlotDisplayData"/> to convert the property
/// into a <see cref="SlotDisplayData"/> without needing an external delegate.
/// </summary>
public interface ISlotDisplayableProperty
{
    SlotDisplayData ToSlotDisplayData(int itemCount);
}

/// <summary>
/// Manages a grid of <see cref="UI_ContainerSlot"/> elements.
/// Receives an array of <see cref="SlotDisplayData"/> -- no generics, no database references.
/// </summary>
public class UI_Container : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("Root GameObject of the entire container panel. Shown/hidden by Open and Close.")]
    [SerializeField] private GameObject containerPanelRoot;

    [Header("References")]
    [SerializeField] protected Transform slotParent;
    [SerializeField] private UI_ContainerSlot slotPrefab;

    [Header("Selection")]
    [Tooltip("When true, clicking a slot selects it (highlight + event). When false, clicks are ignored.")]
    [SerializeField] private bool selectable = false;

    [Header("Settings")]
    [SerializeField] private bool hideWhenAwake = true;

    protected readonly List<UI_ContainerSlot> slotUIs = new List<UI_ContainerSlot>();
    private int selectedSlotIndex = -1;

    public IReadOnlyList<UI_ContainerSlot> SlotUIs => slotUIs;

    /// <summary>Index of the currently selected slot, or -1 if none.</summary>
    public int SelectedSlotIndex => selectedSlotIndex;
    public bool HasSelection => selectedSlotIndex >= 0;

    /// <summary>
    /// Fired when the selected slot changes.
    /// Parameter: new selected index (-1 = deselected).
    /// </summary>
    public event Action<int> OnSelectionChanged;

    private void Awake()
    {
        if (hideWhenAwake && containerPanelRoot != null)
            Close();
    }

    // --- Init ---

    /// <summary>
    /// Create (or trim) slot UI elements to match <paramref name="slotCount"/>.
    /// Call once when the container size is known.
    /// </summary>
    public virtual void InitSlots(int slotCount)
    {
        // Remove excess
        while (slotUIs.Count > slotCount)
        {
            int last = slotUIs.Count - 1;
            Destroy(slotUIs[last].gameObject);
            slotUIs.RemoveAt(last);
        }

        // Add missing
        while (slotUIs.Count < slotCount)
        {
            var go = Instantiate(slotPrefab, slotParent);
            go.SetClickCallback(HandleSlotClicked);
            slotUIs.Add(go);
        }

        // Mark all empty initially
        for (int i = 0; i < slotUIs.Count; i++)
            slotUIs[i].SetEmpty(i);

        ClearSelection();
    }

    protected virtual void HandleSlotClicked(int slotIndex)
    {
        if (!selectable) return;

        // Toggle: click the same slot again to deselect
        if (slotIndex == selectedSlotIndex)
        {
            ClearSelection();
            return;
        }

        SetSelection(slotIndex);
    }

    // --- Selection ---

    /// <summary>
    /// Programmatically select a slot by index. Pass -1 to clear.
    /// </summary>
    public void SetSelection(int index)
    {
        if (index == selectedSlotIndex) return;

        // Un-highlight previous
        if (selectedSlotIndex >= 0 && selectedSlotIndex < slotUIs.Count)
            slotUIs[selectedSlotIndex].SetHighlight(false);

        selectedSlotIndex = index;

        // Highlight new
        if (selectedSlotIndex >= 0 && selectedSlotIndex < slotUIs.Count)
            slotUIs[selectedSlotIndex].SetHighlight(true);

        OnSelectionChanged?.Invoke(selectedSlotIndex);
    }

    /// <summary>
    /// Clear the current selection (deselect all).
    /// </summary>
    public void ClearSelection()
    {
        SetSelection(-1);
    }

    /// <summary>
    /// Show the container panel.
    /// </summary>
    public void Open()
    {
        if (containerPanelRoot != null)
            containerPanelRoot.SetActive(true);
    }

    /// <summary>
    /// Hide the container panel and clear any active selection.
    /// </summary>
    public void Close()
    {
        ClearSelection();

        if (containerPanelRoot != null)
            containerPanelRoot.SetActive(false);
    }

    // --- Refresh ---

    /// <summary>
    /// Refresh every slot using pre-resolved display data.
    /// The array length should match the slot count set in <see cref="InitSlots"/>.
    /// </summary>
    public virtual void Refresh(SlotDisplayData[] displayData)
    {
        if (displayData == null) return;

        int count = Mathf.Min(slotUIs.Count, displayData.Length);
        for (int i = 0; i < count; i++)
            slotUIs[i].SetSlot(i, displayData[i]);

        // Any remaining slots beyond displayData length -> empty
        for (int i = count; i < slotUIs.Count; i++)
            slotUIs[i].SetEmpty(i);
    }

    /// <summary>
    /// Refresh a single slot at <paramref name="index"/>.
    /// </summary>
    public virtual void RefreshSlot(int index, SlotDisplayData data)
    {
        if (index < 0 || index >= slotUIs.Count) return;

        slotUIs[index].SetSlot(index, data);
    }
}