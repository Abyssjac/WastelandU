using UnityEngine;

/// <summary>
/// Intermediate abstract layer for interactables that open a UI panel when triggered.
/// Subclasses provide the panel reference and any data the panel needs.
/// The panel calls <see cref="NotifyPanelClosed"/> when the player exits,
/// which in turn calls <see cref="InteractorTargetDetector.EndInteraction"/>.
/// </summary>
public abstract class BasePanelInteractable : BaseInteractable
{
    // ── Runtime ───────────────────────────────────────────────────

    private InteractorTargetDetector _caller;

    // ─────────────────────────────────────────────────────────────
    // BaseInteractable
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Stores the caller and delegates to <see cref="OnOpenPanel"/>.
    /// Do not override this in subclasses; override <see cref="OnOpenPanel"/> instead.
    /// </summary>
    public sealed override void Interact(InteractorTargetDetector caller)
    {
        _caller = caller;
        OnOpenPanel();
    }

    // ─────────────────────────────────────────────────────────────
    // Abstract / Virtual
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="Interact"/> after the caller is stored.
    /// Subclasses open their specific panel here.
    /// </summary>
    protected abstract void OnOpenPanel();

    // ─────────────────────────────────────────────────────────────
    // Public API for panels
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this from the panel when the player finishes the interaction (e.g. presses ESC).
    /// Releases the interact state on the detector.
    /// </summary>
    public void NotifyPanelClosed()
    {
        var caller = _caller;
        _caller = null;

        if (caller != null)
            caller.EndInteraction();
    }
}
