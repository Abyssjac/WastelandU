using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scene-level singleton registry for all active <see cref="BaseInteractable"/> objects.
/// Each BaseInteractable registers itself on OnEnable and unregisters on OnDisable.
/// </summary>
public class InteractableManager : MonoBehaviour
{
    public static InteractableManager Instance { get; private set; }

    private readonly List<BaseInteractable> _interactables = new List<BaseInteractable>();

    // ─────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ─────────────────────────────────────────────────────────────
    // Registration
    // ─────────────────────────────────────────────────────────────

    /// <summary>Called by <see cref="BaseInteractable"/> on OnEnable.</summary>
    public void Register(BaseInteractable interactable)
    {
        if (interactable == null) return;
        if (!_interactables.Contains(interactable))
            _interactables.Add(interactable);
    }

    /// <summary>Called by <see cref="BaseInteractable"/> on OnDisable.</summary>
    public void Unregister(BaseInteractable interactable)
    {
        _interactables.Remove(interactable);
    }

    // ─────────────────────────────────────────────────────────────
    // Query
    // ─────────────────────────────────────────────────────────────

    /// <summary>Returns a read-only view of all currently registered interactables.</summary>
    public IReadOnlyList<BaseInteractable> GetAllInteractables() => _interactables;
}
