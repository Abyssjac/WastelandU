using TMPro;
using UnityEngine;

/// <summary>
/// Store-specific presentation data for a slot.
/// <see cref="_priceMultiplier"/> is only the multiplier configured on this
/// individual <see cref="StoreSlot"/>. It deliberately does not represent a
/// store-wide default multiplier or a fixed-price override.
/// </summary>
public class StoreSlotDisplayData : SlotDisplayData
{
    public float _priceMultiplier;

    public StoreSlotDisplayData(
        Sprite icon,
        Color iconColor,
        int count,
        string labelText = "",
        SlotState state = SlotState.Default,
        float priceMultiplier = 1f) : base(icon, iconColor, count, labelText, state)
    {
        _priceMultiplier = priceMultiplier;
    }
}

/// <summary>
/// Store-specific slot that extends <see cref="UI_ContainerSlot"/>.
/// Locked-slot selection rules remain owned by <see cref="UI_StoreContainer"/>.
///
/// All state overlay logic is inherited from the base class. This class adds
/// the optional per-slot price-multiplier label.
/// </summary>
public class UI_StoreSlot : UI_ContainerSlot
{
    [Header("Price Multiplier")]
    [SerializeField] private GameObject _priceMultiplierPanel;
    [SerializeField] private TextMeshProUGUI _priceMultiplierText;

    public override void SetSlot(int index, SlotDisplayData data)
    {
        base.SetSlot(index, data);

        if (data is not StoreSlotDisplayData storeData)
        {
            if (_priceMultiplierPanel != null)
                _priceMultiplierPanel.SetActive(false);

            return;
        }

        bool shouldShowPriceMultiplier = (storeData.state == SlotState.Default || storeData.state == SlotState.SoldOut)
            && storeData._priceMultiplier != 1f;

        if (_priceMultiplierPanel != null)
            _priceMultiplierPanel.SetActive(shouldShowPriceMultiplier);

        if (!shouldShowPriceMultiplier || _priceMultiplierText == null)
            return;

        int percentage = Mathf.RoundToInt((storeData._priceMultiplier - 1f) * 100f);
        string sign = percentage > 0 ? "+" : "";
        _priceMultiplierText.text = sign + percentage + "%";
    }
}
