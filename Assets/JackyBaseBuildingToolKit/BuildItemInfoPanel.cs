using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays the selected buildable inventory item's basic presentation data.
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
    [SerializeField] private string emptyNameHint = "---";
    [SerializeField] private string emptyDescriptionHint = "Select an item to build.";
    [SerializeField] private string emptyCostHint = "";

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void Open()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        IsOpen = true;
        ShowEmpty();
    }

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

    public void Show(ItemDefinitionSO item)
    {
        if (itemNameText != null)
            itemNameText.text = item != null && !string.IsNullOrEmpty(item.DisplayName)
                ? item.DisplayName
                : "Unknown";

        if (descriptionText != null)
            descriptionText.text = "";
        if (itemIconImage != null)
            itemIconImage.sprite = item != null ? item.Icon : null;
        if (categoryIconImage != null)
            categoryIconImage.sprite = null;
        if (costText != null)
            costText.text = "";

        if (panelRoot != null)
            panelRoot.SetActive(true);

        IsOpen = true;
    }

    public void Close()
    {
        ShowEmpty();

        if (panelRoot != null)
            panelRoot.SetActive(false);

        IsOpen = false;
    }
}
