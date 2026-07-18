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

    public void Show(ItemDefinitionSO item, SellableProperty sellable, CurrencyType currency)
    {
        if (item == null || sellable == null)
        {
            ShowEmpty();
            return;
        }

        SetText(displayNameText, item.DisplayName);
        SetText(descriptionText, sellable.DetailDescription);
        SetText(priceText, $"{sellable.Price} {currency}");
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
