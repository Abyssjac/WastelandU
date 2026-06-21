using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central manager for all in-game currencies.
/// Currencies and their starting balances are configured via the Inspector.
///
/// Singleton ¡ª place on a DontDestroyOnLoad GameObject.
/// </summary>
public class EconomyManager : MonoBehaviour
{
    // ©¤©¤ Singleton ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    public static EconomyManager Instance { get; private set; }

    // ©¤©¤ Inspector ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Header("Currencies")]
    [Tooltip("Define every currency and its starting balance here.")]
    [SerializeField] private List<CurrencyEntry> initialCurrencies = new List<CurrencyEntry>();

    [Header("Debug")]
    [SerializeField] private bool debugEnabled;

    // ©¤©¤ State ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private readonly Dictionary<CurrencyType, float> _amounts =
        new Dictionary<CurrencyType, float>();

    // ©¤©¤ Events ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    /// <summary>
    /// Fired whenever a currency balance changes.
    /// Provides the affected <see cref="CurrencyType"/> and the new balance.
    /// </summary>
    public event Action<CurrencyType, float> OnCurrencyChanged;

    // ©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        foreach (CurrencyEntry entry in initialCurrencies)
        {
            if (!_amounts.ContainsKey(entry.type))
                _amounts[entry.type] = entry.initialAmount;
        }
    }

    // ©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>Returns the current balance of the given currency (0 if never initialised).</summary>
    public float GetAmount(CurrencyType type)
    {
        _amounts.TryGetValue(type, out float value);
        return value;
    }

    /// <summary>Adds <paramref name="amount"/> to the given currency and fires <see cref="OnCurrencyChanged"/>.</summary>
    public void AddCurrency(CurrencyType type, float amount)
    {
        if (!_amounts.ContainsKey(type))
            _amounts[type] = 0f;

        _amounts[type] += amount;

        if (debugEnabled)
            Debug.Log($"[EconomyManager] {type} +{amount:F1} ¡ú {_amounts[type]:F1}");

        OnCurrencyChanged?.Invoke(type, _amounts[type]);
    }

    /// <summary>Overwrites the balance of the given currency and fires <see cref="OnCurrencyChanged"/>.</summary>
    public void SetCurrency(CurrencyType type, float amount)
    {
        _amounts[type] = amount;

        if (debugEnabled)
            Debug.Log($"[EconomyManager] {type} set to {_amounts[type]:F1}");

        OnCurrencyChanged?.Invoke(type, _amounts[type]);
    }

    /// <summary>
    /// Attempts to deduct <paramref name="amount"/> from the given currency.
    /// Returns false without modifying the balance if funds are insufficient.
    /// </summary>
    public bool TrySpend(CurrencyType type, float amount)
    {
        if (!_amounts.TryGetValue(type, out float current) || current < amount)
        {
            if (debugEnabled)
                Debug.Log($"[EconomyManager] TrySpend failed: {type} needs {amount:F1}, has {current:F1}");
            return false;
        }

        _amounts[type] = current - amount;

        if (debugEnabled)
            Debug.Log($"[EconomyManager] {type} -{amount:F1} ¡ú {_amounts[type]:F1}");

        OnCurrencyChanged?.Invoke(type, _amounts[type]);
        return true;
    }
}
