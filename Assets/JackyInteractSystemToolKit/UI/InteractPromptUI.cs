using UnityEngine;
using TMPro;

/// <summary>
/// Attach to a UI GameObject inside the HUD Canvas.
/// Listens to <see cref="InteractorTargetDetector.OnStateChanged"/> and updates the
/// prompt text accordingly. Fully decoupled – holds no reference to any concrete
/// Interactable subclass.
/// </summary>
public class InteractPromptUI : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────

    [Header("References")]
    [Tooltip("Root GameObject to show / hide. Assign the panel or the text's parent.")]
    [SerializeField] private GameObject promptRoot;

    [Tooltip("TextMeshPro label that shows the prompt string.")]
    [SerializeField] private TextMeshProUGUI promptLabel;

    [Header("Interacting State")]
    [Tooltip("Text shown while an interaction is in progress. Leave empty to hide the prompt entirely.")]
    [SerializeField] private string interactingText = "";

    // ── Runtime ───────────────────────────────────────────────────

    private InteractorTargetDetector _detector;

    // ─────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────

    private void Start()
    {
        _detector = FindFirstObjectByType<InteractorTargetDetector>();
        if (_detector == null)
        {
            Debug.LogWarning("[InteractPromptUI] No InteractorTargetDetector found in scene.");
            return;
        }

        _detector.OnStateChanged += HandleStateChanged;

        // Initialise to current state in case the UI spawns late
        Refresh(_detector.CurrentState);
    }

    private void OnDestroy()
    {
        if (_detector != null)
            _detector.OnStateChanged -= HandleStateChanged;
    }

    // ─────────────────────────────────────────────────────────────
    // Handlers
    // ─────────────────────────────────────────────────────────────

    private void HandleStateChanged(InteractState prev, InteractState next)
    {
        Refresh(next);
    }

    private void Refresh(InteractState state)
    {
        switch (state)
        {
            case InteractState.None:
                SetVisible(false);
                break;

            case InteractState.HasTarget:
                var interactable = _detector.GetCurrentInteractable();
                string prompt = interactable != null ? interactable.GetInteractPrompt() : string.Empty;
                if (!string.IsNullOrEmpty(prompt))
                {
                    if (promptLabel != null) promptLabel.text = prompt;
                    SetVisible(true);
                }
                else
                {
                    SetVisible(false);
                }
                break;

            case InteractState.Interacting:
                if (!string.IsNullOrEmpty(interactingText))
                {
                    if (promptLabel != null) promptLabel.text = interactingText;
                    SetVisible(true);
                }
                else
                {
                    SetVisible(false);
                }
                break;
        }
    }

    private void SetVisible(bool visible)
    {
        if (promptRoot != null)
            promptRoot.SetActive(visible);
    }
}
