using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays in-game currency balances on the UI.
/// Subscribes to <see cref="EconomyManager.OnCurrencyChanged"/> and refreshes the
/// relevant Text component whenever a balance changes.
/// </summary>
public class EconomyManagerUI : MonoBehaviour
{
    // ©¤©¤ Nested type ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Serializable]
    private class CurrencyDisplay
    {
        [Tooltip("Which currency this display element represents.")]
        public CurrencyType type;

        [Tooltip("Text component that shows the balance.")]
        public TextMeshProUGUI amountText;
    }

    // ©¤©¤ Inspector ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Header("Currency Displays")]
    [SerializeField] private List<CurrencyDisplay> displays = new List<CurrencyDisplay>();

    // ©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void Start()
    {
        if (EconomyManager.Instance == null)
        {
            Debug.LogWarning("[EconomyUI] EconomyManager instance not found.");
            return;
        }

        EconomyManager.Instance.OnCurrencyChanged += HandleCurrencyChanged;

        // Populate initial values
        foreach (CurrencyDisplay d in displays)
            RefreshDisplay(d.type, EconomyManager.Instance.GetAmount(d.type));
    }

    private void OnDestroy()
    {
        if (EconomyManager.Instance != null)
            EconomyManager.Instance.OnCurrencyChanged -= HandleCurrencyChanged;
    }

    // ©¤©¤ Private helpers ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void HandleCurrencyChanged(CurrencyType type, float newAmount)
    {
        RefreshDisplay(type, newAmount);
    }

    private void RefreshDisplay(CurrencyType type, float amount)
    {
        foreach (CurrencyDisplay d in displays)
        {
            if (d.type == type && d.amountText != null)
                d.amountText.text = Mathf.FloorToInt(amount).ToString();
        }
    }
}
