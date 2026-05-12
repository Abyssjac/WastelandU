using UnityEngine;

/// <summary>
/// Abstract base class for all interactable objects in the scene.
/// Subclasses must implement <see cref="Interact"/>.
/// Attach this (or a subclass) to any GameObject that should be detectable
/// by <see cref="InteractorTargetDetector"/>.
/// </summary>
[DisallowMultipleComponent]
public abstract class BaseInteractable : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────

    [Header("Interact Prompt")]
    [Tooltip("Text shown in the prompt UI, e.g. \"[E] Talk\".")]
    [SerializeField] private string interactPrompt = "[E] Interact";

    [Header("Highlight")]
    [Tooltip("Material applied to all renderers while this object is focused.\n" +
             "Leave empty to skip visual highlight.")]
    [SerializeField] private Material highlightMaterial;

    // ── Runtime ───────────────────────────────────────────────────

    private BaseVisualController _visualController;

    // ─────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────

    protected virtual void Awake()
    {
        _visualController = GetComponent<BaseVisualController>();
    }

    protected virtual void OnEnable()
    {
        if (InteractableManager.Instance != null)
            InteractableManager.Instance.Register(this);
    }

    protected virtual void OnDisable()
    {
        if (InteractableManager.Instance != null)
            InteractableManager.Instance.Unregister(this);
    }

    // ─────────────────────────────────────────────────────────────
    // Interact System Interface
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by <see cref="InteractorTargetDetector"/> when this object enters focus range.
    /// Default implementation applies the highlight material via <see cref="BaseVisualController"/>.
    /// </summary>
    public virtual void OnFocused()
    {
        if (_visualController == null || highlightMaterial == null) return;
        _visualController.SetMaterialAllTemp(highlightMaterial);
    }

    /// <summary>
    /// Called by <see cref="InteractorTargetDetector"/> when this object leaves focus.
    /// Default implementation resets all materials via <see cref="BaseVisualController"/>.
    /// </summary>
    public virtual void OnUnfocused()
    {
        if (_visualController == null) return;
        _visualController.ResetMaterials();
    }

    /// <summary>
    /// Called when the player presses the interact key while this object is focused.
    /// Subclasses must implement their interaction logic here.
    /// Call <paramref name="caller"/>.EndInteraction() when the interaction finishes.
    /// </summary>
    /// <param name="caller">The detector driving this interaction; use it to signal end-of-interaction.</param>
    public abstract void Interact(InteractorTargetDetector caller);

    /// <summary>
    /// Called after <see cref="InteractorTargetDetector.EndInteraction"/> is invoked.
    /// Override for any cleanup needed when an interaction ends.
    /// </summary>
    public virtual void OnInteractEnd() { }

    /// <summary>
    /// Returns the prompt string shown in the UI while this object is focused.
    /// Override to provide dynamic text (e.g. based on item name).
    /// </summary>
    public virtual string GetInteractPrompt() => interactPrompt;
}
