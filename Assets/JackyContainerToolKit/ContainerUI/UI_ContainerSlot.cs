using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_ContainerSlot : MonoBehaviour
{
    [SerializeField] private Image itemIcon;
    [SerializeField] private TextMeshProUGUI itemCountText;
    [SerializeField] private GameObject emptyOverlay;
    [SerializeField] private Button button;

    [Header("Highlight")]
    [SerializeField] private GameObject highlightOverlay;

    private int slotIndex = -1;
    private Action<int> onClicked;

    public int SlotIndex => slotIndex;

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
    /// Pass null sprite / 0 count to show an empty slot.
    /// </summary>
    public void SetSlot(int index, Sprite icon, Color iconColor, int count)
    {
        slotIndex = index;

        bool isEmpty = icon == null || count <= 0;

        if (itemIcon != null)
        {
            itemIcon.sprite = icon;
            itemIcon.color = isEmpty ? Color.clear : iconColor;
            itemIcon.enabled = !isEmpty;
        }

        if (itemCountText != null)
        {
            itemCountText.text = isEmpty ? "" : count.ToString();
            itemCountText.enabled = !isEmpty;
        }

        if (emptyOverlay != null)
            emptyOverlay.SetActive(isEmpty);
    }

    /// <summary>
    /// Display this slot as empty.
    /// </summary>
    public void SetEmpty(int index)
    {
        SetSlot(index, null, Color.clear, 0);
    }
}
