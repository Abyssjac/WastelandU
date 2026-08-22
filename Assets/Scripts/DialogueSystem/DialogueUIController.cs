using UnityEngine;

/// <summary>
/// Controls the active state of the Yarn dialogue presentation hierarchy.
///
/// Keep this component on an always-active object such as MyDialogueSystem,
/// and assign only the presentation root (Line Presenter, Options Presenter,
/// and Portrait Presenter). Do not assign the shared Canvas, because it also
/// contains other UI such as the NPC interaction menu.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueUIController : MonoBehaviour
{
    [Header("Dialogue Presentation")]
    [Tooltip("The root that contains only Yarn dialogue visuals. Do not assign the shared Canvas or the NPC interaction menu.")]
    [SerializeField] private GameObject _dialoguePresentationRoot;

    [Tooltip("Ensures no dialogue visual or UI raycast remains active before the first dialogue starts.")]
    [SerializeField] private bool _hideOnAwake = true;

    public bool IsDialoguePresentationVisible => _dialoguePresentationRoot != null
                                                && _dialoguePresentationRoot.activeSelf;

    private void Awake()
    {
        if (_hideOnAwake)
            HideDialoguePresentation();
    }

    private void OnDisable()
    {
        HideDialoguePresentation();
    }

    /// <summary>
    /// Assign this method to DialogueRunner.On Dialogue Start.
    /// </summary>
    public void ShowDialoguePresentation()
    {
        SetDialoguePresentationVisible(true);
    }

    /// <summary>
    /// Assign this method to DialogueRunner.On Dialogue Complete.
    /// </summary>
    public void HideDialoguePresentation()
    {
        SetDialoguePresentationVisible(false);
    }

    private void SetDialoguePresentationVisible(bool isVisible)
    {
        if (_dialoguePresentationRoot == null)
        {
            Debug.LogWarning($"[{nameof(DialogueUIController)}] Dialogue Presentation Root is not assigned.", this);
            return;
        }

        if (_dialoguePresentationRoot.activeSelf != isVisible)
            _dialoguePresentationRoot.SetActive(isVisible);
    }

    private void OnValidate()
    {
        if (_dialoguePresentationRoot == gameObject)
        {
            Debug.LogError($"[{nameof(DialogueUIController)}] Dialogue Presentation Root cannot be the same GameObject as this controller.", this);
            _dialoguePresentationRoot = null;
        }
    }
}
