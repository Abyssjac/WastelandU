using System;
using System.Collections;
using JackyUtility;
using UnityEngine;

/// <summary>
/// Singleton that tracks the player's hit points.
///
/// <list type="bullet">
///   <item>Call <see cref="TakeDamage"/> to subtract HP.</item>
///   <item>Subscribe to <see cref="OnHealthChanged"/> for UI updates.</item>
///   <item>When HP reaches 0 the scene is reloaded via
///         <see cref="AllLevelManager.ReloadCurrentScene"/> after
///         an optional <see cref="deathDelay"/>.</item>
/// </list>
/// </summary>
public class PlayerHealthManager : MonoBehaviour
{
    public static PlayerHealthManager Instance { get; private set; }

    [Header("Health")]
    [Tooltip("Maximum hit points. Also used as the starting value.")]
    [SerializeField] private int maxHp = 3;

    [Header("Death")]
    [Tooltip("Seconds to wait after HP reaches 0 before reloading the scene. 0 = immediate.")]
    [SerializeField] private float deathDelay = 0f;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Runtime ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private int currentHp;
    private bool isDead = false;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public int CurrentHp => currentHp;
    public int MaxHp => maxHp;

    /// <summary>
    /// Fired whenever HP changes.
    /// Parameters: (currentHp, maxHp).
    /// Subscribe here to drive health bar UI.
    /// </summary>
    public event Action<int, int> OnHealthChanged;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        currentHp = maxHp;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Damage ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Subtract <paramref name="amount"/> HP from the player.
    /// Clamps to 0 and triggers death if HP reaches 0.
    /// No-op if the player is already dead or amount is not positive.
    /// </summary>
    public void TakeDamage(int amount)
    {
        if (isDead) return;
        if (amount <= 0) return;

        currentHp = Mathf.Max(0, currentHp - amount);

        if (enableDebug)
            Debug.Log($"[PlayerHealthManager] TakeDamage({amount}). HP: {currentHp}/{maxHp}", this);

        OnHealthChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
            StartCoroutine(HandleDeath());
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Death ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private IEnumerator HandleDeath()
    {
        isDead = true;

        if (enableDebug)
            Debug.Log($"[PlayerHealthManager] Player died. Reloading in {deathDelay}s.", this);

        if (deathDelay > 0f)
            yield return new WaitForSeconds(deathDelay);

        if (AllLevelManager.Instance != null)
            AllLevelManager.Instance.ReloadCurrentScene();
        else
            Debug.LogError("[PlayerHealthManager] AllLevelManager.Instance is null ¡ª cannot reload scene.", this);
    }
}
