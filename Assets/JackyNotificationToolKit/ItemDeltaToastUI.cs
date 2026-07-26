using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Renders an item gain or loss notification.</summary>
public class ItemDeltaToastUI : NotificationToastUI
{
    [Header("Display")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI itemNameLabel;
    [SerializeField] private TextMeshProUGUI amountLabel;

    [Header("Colors")]
    [SerializeField] private Color gainedAmountColor = new Color(0.5f, 1f, 0.6f, 1f);
    [SerializeField] private Color removedAmountColor = new Color(1f, 0.5f, 0.5f, 1f);

    public override void Bind(NotificationRequest request)
    {
        base.Bind(request);

        if (iconImage != null)
        {
            iconImage.sprite = request.icon;
            iconImage.enabled = request.icon != null;
        }

        if (itemNameLabel != null)
            itemNameLabel.text = request.title ?? string.Empty;

        if (amountLabel != null)
        {
            amountLabel.text = request.amount > 0
                ? $"+{request.amount}"
                : request.amount.ToString();
            amountLabel.color = request.amount >= 0 ? gainedAmountColor : removedAmountColor;
        }
    }
}
