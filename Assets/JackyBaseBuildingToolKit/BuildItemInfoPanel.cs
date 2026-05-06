using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI panel that displays item detail information when the player selects a container slot
/// in build mode.
/// Open/Close are driven by EnterBuildMode/ExitBuildMode.
/// Show fills the panel with concrete item data when a slot is selected.
/// ShowEmpty resets the panel to its placeholder state.
/// </summary>
public class BuildItemInfoPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private Image itemIconImage;
    [SerializeField] private Image categoryIconImage;
    [SerializeField] private TextMeshProUGUI costText;

    [Header("Empty State")]
    [Tooltip("Text shown in itemNameText when no item is selected.")]
    [SerializeField] private string emptyNameHint = "---";
    [Tooltip("Text shown in descriptionText when no item is selected.")]
    [SerializeField] private string emptyDescriptionHint = "Select an item to build.";
    [Tooltip("Text shown in costText when no item is selected.")]
    [SerializeField] private string emptyCostHint = "";

    /// <summary>True while the info panel is visible.</summary>
    public bool IsOpen { get; private set; }

    private BuildManager buildManager;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    /// <summary>
    /// Initialize the panel with a reference to the BuildManager.
    /// Called once during setup.
    /// </summary>
    public void Initialize(BuildManager manager)
    {
        buildManager = manager;
    }

    /// <summary>
    /// Open the panel and reset it to the empty placeholder state.
    /// Called when entering build mode.
    /// </summary>
    public void Open()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        IsOpen = true;
        ShowEmpty();
    }

    /// <summary>
    /// Reset panel contents to the placeholder / no-selection state.
    /// Called internally by Open and Close, and externally when a slot is deselected.
    /// </summary>
    internal void ShowEmpty()
    {
        if (itemNameText != null)
            itemNameText.text = emptyNameHint;

        if (descriptionText != null)
            descriptionText.text = emptyDescriptionHint;

        if (costText != null)
            costText.text = emptyCostHint;

        if (itemIconImage != null)
            itemIconImage.sprite = null;

        if (categoryIconImage != null)
            categoryIconImage.sprite = null;
    }

    /// <summary>
    /// Show the panel with information about the selected container item.
    /// </summary>
    public void Show(int slotIndex, ContainerItemProperty itemProp,
                     ContainerItemBuildAction buildAction, BuildActionDisplayInfo displayInfo)
    {
        if (itemNameText != null)
            itemNameText.text = displayInfo != null && !string.IsNullOrEmpty(displayInfo.displayName)
                ? displayInfo.displayName
                : (itemProp != null ? itemProp.StringKey : "Unknown");

        if (descriptionText != null)
            descriptionText.text = displayInfo != null ? displayInfo.description : "";

        if (itemIconImage != null && itemProp != null)
            itemIconImage.sprite = itemProp.icon;

        if (categoryIconImage != null && displayInfo != null)
            categoryIconImage.sprite = displayInfo.icon;

        if (costText != null && buildAction != null)
            costText.text = $"Cost: {Mathf.Max(1, buildAction.costPerBuild)}";

        if (panelRoot != null)
            panelRoot.SetActive(true);

        IsOpen = true;
    }

    /// <summary>
    /// Show the panel with information from a <see cref="BuildableProperty"/> directly.
    /// Used by <see cref="BuildManagerUI"/> when the player selects a buildable slot.
    /// </summary>
    public void Show(BuildableProperty prop)
    {
        if (itemNameText != null)
            itemNameText.text = prop != null && !string.IsNullOrEmpty(prop.displayName)
                ? prop.displayName
                : (prop != null ? prop.EnumKey.ToString() : "Unknown");

        if (descriptionText != null)
            descriptionText.text = "";

        if (itemIconImage != null)
            itemIconImage.sprite = prop != null ? prop.iconSprite : null;

        if (categoryIconImage != null)
            categoryIconImage.sprite = null;

        if (costText != null)
            costText.text = "Cost: 1";

        if (panelRoot != null)
            panelRoot.SetActive(true);

        IsOpen = true;
    }

    /// <summary>
    /// Close / hide the panel and reset contents to the empty state.
    /// Called when exiting build mode.
    /// </summary>
    public void Close()
    {
        ShowEmpty();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        IsOpen = false;
    }
}
