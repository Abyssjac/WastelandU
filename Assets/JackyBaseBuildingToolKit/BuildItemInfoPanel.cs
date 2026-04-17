using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI panel that displays item detail information when the player selects a container slot
/// in build mode. This is purely informational ¡ª placement starts immediately.
/// The panel auto-hides when no item is selected.
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
    /// Show the panel with information about the selected container item.
    /// </summary>
    public void Show(int slotIndex, ContainerItemProperty itemProp,
                     ContainerItemBuildAction buildAction, BuildActionDisplayInfo displayInfo)
    {
        if (itemNameText != null)
            itemNameText.text = itemProp != null ? itemProp.StringKey : "Unknown";

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
    /// Close / hide the panel.
    /// </summary>
    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        IsOpen = false;
    }
}
