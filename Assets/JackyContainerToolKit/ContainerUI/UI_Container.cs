using System.Collections.Generic;
using System;
using UnityEngine;

/// <summary>
/// Pure-data struct describing what a single slot should look like.
/// Prepared by the caller ¡ª this UI knows nothing about databases or properties.
/// </summary>
[Serializable]
public struct SlotDisplayData
{
    public Sprite icon;
    public Color iconColor;
    public int count;

    //public bool IsEmpty => icon == null || count <= 0;
    public bool IsEmpty => count <= 0;

    public SlotDisplayData(Sprite icon, Color iconColor, int count)
    {
        this.icon = icon;
        this.iconColor = iconColor;
        this.count = count;
    }

    public static SlotDisplayData Empty => new SlotDisplayData(null, Color.clear, 0);

    public override string ToString()
    {
        return IsEmpty ? "Empty" : $"Icon={icon.name}, Color={iconColor}, Count={count}";
    }
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
/// Receives an array of <see cref="SlotDisplayData"/> ¡ª no generics, no database references.
/// </summary>
public class UI_Container : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform slotParent;
    [SerializeField] private UI_ContainerSlot slotPrefab;

    [Header("Selection")]
    [Tooltip("When true, clicking a slot selects it (highlight + event). When false, clicks are ignored.")]
    [SerializeField] private bool selectable = false;

    private readonly List<UI_ContainerSlot> slotUIs = new List<UI_ContainerSlot>();
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

    // ©¤©¤©¤ Init ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Create (or trim) slot UI elements to match <paramref name="slotCount"/>.
    /// Call once when the container size is known.
    /// </summary>
    public void InitSlots(int slotCount)
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

    private void HandleSlotClicked(int slotIndex)
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

    // ©¤©¤©¤ Selection ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

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

    // ©¤©¤©¤ Refresh ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Refresh every slot using pre-resolved display data.
    /// The array length should match the slot count set in <see cref="InitSlots"/>.
    /// </summary>
    public void Refresh(SlotDisplayData[] displayData)
    {
        if (displayData == null) return;

        int count = Mathf.Min(slotUIs.Count, displayData.Length);
        for (int i = 0; i < count; i++)
        {
            var data = displayData[i];
            if (data.IsEmpty)
                slotUIs[i].SetEmpty(i);
            else
                slotUIs[i].SetSlot(i, data.icon, data.iconColor, data.count);

            //Debug.Log($"Slot {i}: {(data.IsEmpty ? "Empty" : $"Icon={data.icon.name}, Color={data.iconColor}, Count={data.count}")}");
        }

        // Any remaining slots beyond displayData length ¡ú empty
        for (int i = count; i < slotUIs.Count; i++)
            slotUIs[i].SetEmpty(i);
    }

    /// <summary>
    /// Refresh a single slot at <paramref name="index"/>.
    /// </summary>
    public void RefreshSlot(int index, SlotDisplayData data)
    {
        if (index < 0 || index >= slotUIs.Count) return;

        if (data.IsEmpty)
            slotUIs[index].SetEmpty(index);
        else
            slotUIs[index].SetSlot(index, data.icon, data.iconColor, data.count);
    }
}
