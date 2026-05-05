using System;
using UnityEngine;

/// <summary>
/// Inspector-configurable entry that maps a <see cref="CurrencyType"/> to its initial amount.
/// Used by <see cref="EconomyManager"/> to seed the currency container on startup.
/// </summary>
[Serializable]
public class CurrencyEntry
{
    [Tooltip("Which currency this entry configures.")]
    public CurrencyType type;

    [Tooltip("Starting balance for this currency.")]
    public float initialAmount;
}
