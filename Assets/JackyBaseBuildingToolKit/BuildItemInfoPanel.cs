using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI panel that displays item information when the player selects a container slot
/// in build mode. The player must confirm via the confirm button before entering
/// placement mode. This panel blocks build-related hotkeys while open.
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
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    /// <summary>True while the info panel is visible and awaiting player input.</summary>
    public bool IsOpen { get; private set; }

    // Cached data for the pending build
    private int pendingSlotIndex = -1;
    private BuildManager buildManager;

    private void Awake()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmClicked);
        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelClicked);

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void Update()
    {
        if (!IsOpen) return;

        // Escape closes the panel (consumes the key so BuildManager doesn't also react)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Close();
        }
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
    /// <param name="slotIndex">The container slot index that was selected.</param>
    /// <param name="itemProp">The container item property.</param>
    /// <param name="buildAction">The build action on the item.</param>
    /// <param name="displayInfo">Optional display info from the database (may be null).</param>
    public void Show(int slotIndex, ContainerItemProperty itemProp,
                     ContainerItemBuildAction buildAction, BuildActionDisplayInfo displayInfo)
    {
        Debug.Log("Showing build item info panel for slot: " + slotIndex);
        pendingSlotIndex = slotIndex;

        // Populate UI fields
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
    /// Close the panel without confirming.
    /// </summary>
    public void Close()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        IsOpen = false;
        pendingSlotIndex = -1;
    }

    private void OnConfirmClicked()
    {
        if (buildManager != null && pendingSlotIndex >= 0)
        {
            int slot = pendingSlotIndex;
            Close();
            buildManager.SelectSlotForBuild(slot);
        }
    }

    private void OnCancelClicked()
    {
        Close();
    }
}
