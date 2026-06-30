using TMPro;
using UnityEngine;

public class StoreItemDetailPanelUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI displayNameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI priceText;

    [Header("Empty State")]
    [SerializeField] private string emptyDisplayName = "";
    [SerializeField] private string emptyDescription = "";
    [SerializeField] private string emptyPrice = "";

    public void Show(BuildableProperty property, CurrencyType currency)
    {
        if (property == null)
        {
            ShowEmpty();
            return;
        }

        SetText(displayNameText, property.displayName);
        SetText(descriptionText, property.description);
        SetText(priceText, $"{property.storePrice:F0} {currency}");
    }

    public void ShowEmpty()
    {
        SetText(displayNameText, emptyDisplayName);
        SetText(descriptionText, emptyDescription);
        SetText(priceText, emptyPrice);
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value;
    }
}
