using UnityEngine;

/// <summary>
/// Intermediate abstract layer for interactables that open a UI panel when triggered.
/// Subclasses assign <see cref="_panel"/> inside <see cref="OnOpenPanel"/> and the base class
/// handles forced panel closure when the interaction is ended externally (e.g. player walks out of range).
/// The panel calls <see cref="NotifyPanelClosed"/> when the player exits normally,
/// which in turn calls <see cref="InteractorTargetDetector.EndInteraction"/>.
/// </summary>
public abstract class BasePanelInteractable : BaseInteractable
{
    // ── Runtime ───────────────────────────────────────────────────

    private InteractorTargetDetector _caller;

    /// <summary>
    /// Subclasses must assign this inside <see cref="OnOpenPanel"/> before returning.
    /// Used by <see cref="OnInteractEnd"/> to forcibly close the panel when the interaction
    /// is terminated externally (e.g. player walks out of range).
    /// </summary>
    protected IInteractablePanel _panel;

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

    /// <summary>
    /// Called by <see cref="InteractorTargetDetector.EndInteraction"/> when the interaction
    /// is terminated externally (e.g. player walks out of range).
    /// Forcibly closes the panel without going through <see cref="NotifyPanelClosed"/>.
    /// </summary>
    public override void OnInteractEnd()
    {
        var panel = _panel;
        _panel  = null;
        _caller = null;
        panel?.ClosePanel();
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
        _panel  = null;
        _caller = null;

        if (caller != null)
            caller.EndInteraction();
    }
}
