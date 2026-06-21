using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ContainerSlot : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemCountText;
    [SerializeField] private TextMeshProUGUI itemLabel;
    [SerializeField] private Button button;

    [Header("Highlight")]
    [SerializeField] private GameObject highlightOverlay;

    [Header("State Overlays")]
    [Tooltip("Map each SlotState to its overlay GameObject. Default state needs no entry.")]
    [SerializeField] private List<SlotStateEntry> stateOverlays = new List<SlotStateEntry>();

    private int slotIndex = -1;
    private Action<int> onClicked;

    public int SlotIndex => slotIndex;

    /// <summary>The state currently displayed on this slot.</summary>
    public SlotState CurrentState { get; private set; } = SlotState.Empty;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(() => onClicked?.Invoke(slotIndex));

        SetHighlight(false);
    }

    /// <summary>
    /// Register a callback that fires when this slot is clicked.
    /// </summary>
    public void SetClickCallback(Action<int> callback)
    {
        onClicked = callback;
    }

    /// <summary>
    /// Show or hide the selection highlight on this slot.
    /// </summary>
    public void SetHighlight(bool on)
    {
        if (highlightOverlay != null)
            highlightOverlay.SetActive(on);
    }

    /// <summary>
    /// Bind this UI element to a specific slot index and display the given data.
    /// The <see cref="SlotDisplayData.state"/> field determines which overlay is shown.
    /// <see cref="SlotState.Default"/> shows the icon and count; all other states clear the content area.
    /// </summary>
    public virtual void SetSlot(int index, SlotDisplayData data)
    {
        slotIndex = index;

        bool showContent = data != null && data.state == SlotState.Default;

        if (itemIcon != null)
        {
            itemIcon.sprite = showContent ? data.icon : null;
            itemIcon.color  = showContent ? data.iconColor : Color.clear;
            itemIcon.enabled = showContent;
        }

        if (itemCountText != null)
        {
            itemCountText.text    = showContent ? data.count.ToString() : "";
            itemCountText.enabled = showContent;
        }

        if (itemLabel != null)
        {
            itemLabel.text    = showContent ? data.labelText : "";
            itemLabel.enabled = showContent && !string.IsNullOrEmpty(data.labelText);
        }

        ApplyStateOverlays(data != null ? data.state : SlotState.Empty);
    }

    /// <summary>
    /// Display this slot as empty (<see cref="SlotState.Empty"/>).
    /// Called by <see cref="UI_Container.InitSlots"/> and trailing-slot cleanup in Refresh.
    /// </summary>
    public virtual void SetEmpty(int index)
    {
        SetSlot(index, SlotDisplayData.Empty);
    }

    /// <summary>
    /// Activates the overlay matching <paramref name="state"/> and deactivates all others.
    /// Updates <see cref="CurrentState"/>.
    /// </summary>
    protected void ApplyStateOverlays(SlotState state)
    {
        CurrentState = state;

        for (int i = 0; i < stateOverlays.Count; i++)
        {
            var entry = stateOverlays[i];
            if (entry.overlay != null)
                entry.overlay.SetActive(entry.state == state);
        }
    }
}