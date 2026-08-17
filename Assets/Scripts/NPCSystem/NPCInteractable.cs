using System;
using System.Collections.Generic;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// World interaction entry point for one NPC.
/// A single Interact session can move from the option menu to Talk, Store, or
/// NPC Panel, but only the final endpoint exit releases the Interacting state.
/// </summary>
[DisallowMultipleComponent]
public class NPCInteractable : BasePanelInteractable
{
    [Header("NPC Identity")]
    [Tooltip("Automatically read from NPCBehaviour on this GameObject.")]
    [SerializeField] private Key_NPC _npcKey;

    [Header("Session References")]
    [Tooltip("Optional scene reference. If empty, NPCInteractionMenuUI.Instance is used.")]
    [SerializeField] private NPCInteractionMenuUI _interactionMenuUI;

    [Tooltip("Optional scene reference. If empty, the first active DialogueRunner is used.")]
    [SerializeField] private DialogueRunner _dialogueRunner;

    private NPCBehaviour _behaviour;
    private StoreManager _activeStoreManager;
    private NPCInteractionType _activeInteractionType = NPCInteractionType.None;
    private bool _hasActiveSession;
    private bool _isEndingSession;
    private bool _isMenuOpen;
    private bool _isListeningToDialogueComplete;
    private bool _isListeningToStoreClosed;

    /// <summary>
    /// NPCs with no visible actions are not targetable, so their Interact prompt
    /// is hidden before the player can press the interact key.
    /// </summary>
    public override bool CanInteract
    {
        get
        {
            NPCManager manager = NPCManager.Instance;
            return _npcKey != Key_NPC.None
                && manager != null
                && manager.HasAnyAvailableInteraction(_npcKey);
        }
    }

    protected override void Awake()
    {
        base.Awake();
        _behaviour = GetComponent<NPCBehaviour>();

        if (_behaviour == null)
        {
            Debug.LogWarning($"[{nameof(NPCInteractable)}] No {nameof(NPCBehaviour)} found on '{gameObject.name}'.", this);
            _npcKey = Key_NPC.None;
            return;
        }

        _npcKey = _behaviour.NpcKey;
    }

    protected override void OnDisable()
    {
        AbortSessionBecauseInteractableDisabled();
        base.OnDisable();
    }

    protected override void OnOpenPanel()
    {
        BeginInteractionSession();
    }

    /// <summary>
    /// Called by <see cref="NPCInteractionMenuUI"/> when the player chooses
    /// a visible option. It validates again because another system may have
    /// changed NPC progress while the menu was open.
    /// </summary>
    public void SelectNPCInteraction(NPCInteractionType interactionType)
    {
        if (!_hasActiveSession || _isEndingSession)
            return;

        NPCManager manager = NPCManager.Instance;
        if (manager == null || !manager.IsInteractionAvailable(_npcKey, interactionType))
        {
            Debug.LogWarning($"[{nameof(NPCInteractable)}] Interaction '{interactionType}' is no longer available for '{_npcKey}'.", this);
            FinishSessionNormally();
            return;
        }

        CloseMenuForTransition();
        _activeInteractionType = interactionType;

        switch (interactionType)
        {
            case NPCInteractionType.Talk:
                StartTalk(manager.GetInteractionProperty(_npcKey));
                break;

            case NPCInteractionType.OpenStore:
                StartStore(manager.GetInteractionProperty(_npcKey));
                break;

            case NPCInteractionType.OpenNPCPanel:
                StartNPCPanel();
                break;

            default:
                FinishSessionNormally();
                break;
        }
    }

    /// <summary>
    /// Called by the menu only when it cannot be opened or when the player
    /// intentionally closes it without selecting an action.
    /// </summary>
    public void CancelNPCInteraction()
    {
        CloseMenuForTransition();
        FinishSessionNormally();
    }

    /// <summary>
    /// Cleans transient subscriptions when the detector ends this interaction
    /// from any path, including NPC Panel's existing NotifyPanelClosed callback.
    /// </summary>
    public override void OnInteractEnd()
    {
        if (_hasActiveSession && !_isEndingSession)
        {
            _isEndingSession = true;
            DetachEndpointListeners();
            CloseNonPanelEndpointSilently();
            ClearSessionState();
        }

        base.OnInteractEnd();
    }

    public override string GetInteractPrompt()
    {
        return CanInteract ? base.GetInteractPrompt() : string.Empty;
    }

    /// <summary>The NPC this interactable represents.</summary>
    public Key_NPC NpcKey => _npcKey;

    /// <summary>The NPCBehaviour on the same GameObject, or null if not found.</summary>
    public NPCBehaviour Behaviour => _behaviour;

    private void BeginInteractionSession()
    {
        if (_hasActiveSession)
        {
            Debug.LogWarning($"[{nameof(NPCInteractable)}] '{_npcKey}' already has an active interaction session.", this);
            return;
        }

        NPCManager manager = NPCManager.Instance;
        if (manager == null)
        {
            Debug.LogWarning($"[{nameof(NPCInteractable)}] Cannot begin interaction because {nameof(NPCManager)} is unavailable.", this);
            NotifyPanelClosed();
            return;
        }

        List<NPCInteractionType> availableInteractions = manager.GetAvailableInteractions(_npcKey);
        if (availableInteractions.Count == 0)
        {
            NotifyPanelClosed();
            return;
        }

        _hasActiveSession = true;
        _isEndingSession = false;
        _activeInteractionType = NPCInteractionType.None;

        NPCInteractionProperty interactionProperty = manager.GetInteractionProperty(_npcKey);
        bool shouldDirectExecute = availableInteractions.Count == 1
            && (interactionProperty == null || interactionProperty.DirectExecuteWhenSingleOption);

        if (shouldDirectExecute)
        {
            SelectNPCInteraction(availableInteractions[0]);
            return;
        }

        NPCInteractionMenuUI menu = ResolveInteractionMenu();
        if (menu == null)
        {
            Debug.LogWarning($"[{nameof(NPCInteractable)}] No {nameof(NPCInteractionMenuUI)} is available for '{_npcKey}'.", this);
            FinishSessionNormally();
            return;
        }

        _interactionMenuUI = menu;
        _isMenuOpen = true;
        _panel = menu;
        menu.OpenMenu(this, availableInteractions);
    }

    private void StartTalk(NPCInteractionProperty interactionProperty)
    {
        if (interactionProperty == null || !interactionProperty.TalkEnabled)
        {
            Debug.LogWarning($"[{nameof(NPCInteractable)}] Talk is missing a valid Yarn node for '{_npcKey}'.", this);
            FinishSessionNormally();
            return;
        }

        DialogueRunner runner = ResolveDialogueRunner();
        if (runner == null)
        {
            Debug.LogWarning($"[{nameof(NPCInteractable)}] No {nameof(DialogueRunner)} is available for '{_npcKey}'.", this);
            FinishSessionNormally();
            return;
        }

        if (runner.IsDialogueRunning)
        {
            Debug.LogWarning($"[{nameof(NPCInteractable)}] Cannot start NPC dialogue for '{_npcKey}' because the shared DialogueRunner is already running.", this);
            FinishSessionNormally();
            return;
        }

        _dialogueRunner = runner;
        _dialogueRunner.onDialogueComplete.AddListener(HandleDialogueComplete);
        _isListeningToDialogueComplete = true;
        StartDialogueAsync(interactionProperty.YarnStartNode);
    }

    private async void StartDialogueAsync(string yarnStartNode)
    {
        try
        {
            await _dialogueRunner.StartDialogue(yarnStartNode);

            // Normal completion is handled by HandleDialogueComplete. This
            // fallback releases the interaction if Yarn exits without firing it.
            if (_hasActiveSession
                && !_isEndingSession
                && _activeInteractionType == NPCInteractionType.Talk
                && !_dialogueRunner.IsDialogueRunning)
            {
                FinishSessionNormally();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);

            if (_hasActiveSession
                && !_isEndingSession
                && _activeInteractionType == NPCInteractionType.Talk)
            {
                FinishSessionNormally();
            }
        }
    }

    private void StartStore(NPCInteractionProperty interactionProperty)
    {
        if (interactionProperty == null || interactionProperty.StoreInventoryProperty == null)
        {
            Debug.LogWarning($"[{nameof(NPCInteractable)}] Store is missing a {nameof(StoreInventoryProperty)} for '{_npcKey}'.", this);
            FinishSessionNormally();
            return;
        }

        _activeStoreManager = StoreManager.Instance;
        if (_activeStoreManager == null)
        {
            Debug.LogWarning($"[{nameof(NPCInteractable)}] No {nameof(StoreManager)} is available for '{_npcKey}'.", this);
            FinishSessionNormally();
            return;
        }

        _activeStoreManager.OnStoreClosed += HandleStoreClosed;
        _isListeningToStoreClosed = true;

        if (!_activeStoreManager.OpenStore(interactionProperty.StoreInventoryProperty))
        {
            DetachStoreClosedListener();
            FinishSessionNormally();
        }
    }

    private void StartNPCPanel()
    {
        NPCPanelUI panel = NPCPanelUI.Instance;
        if (panel == null)
        {
            Debug.LogWarning($"[{nameof(NPCInteractable)}] {nameof(NPCPanelUI)} is unavailable for '{_npcKey}'.", this);
            FinishSessionNormally();
            return;
        }

        _panel = panel;
        panel.OpenPanel(this);
    }

    private void HandleDialogueComplete()
    {
        if (_activeInteractionType == NPCInteractionType.Talk && !_isEndingSession)
            FinishSessionNormally();
    }

    private void HandleStoreClosed()
    {
        if (_activeInteractionType == NPCInteractionType.OpenStore && !_isEndingSession)
            FinishSessionNormally();
    }

    private void FinishSessionNormally()
    {
        if (!_hasActiveSession || _isEndingSession)
            return;

        _isEndingSession = true;
        DetachEndpointListeners();
        ClearSessionState();
        NotifyPanelClosed();
        _isEndingSession = false;
    }

    private void CloseMenuForTransition()
    {
        if (!_isMenuOpen)
            return;

        _isMenuOpen = false;
        _interactionMenuUI?.CloseForTransition();

        if (object.ReferenceEquals(_panel, _interactionMenuUI))
            _panel = null;
    }

    private void DetachEndpointListeners()
    {
        DetachDialogueCompleteListener();
        DetachStoreClosedListener();
    }

    private void DetachDialogueCompleteListener()
    {
        if (!_isListeningToDialogueComplete || _dialogueRunner == null)
            return;

        _dialogueRunner.onDialogueComplete.RemoveListener(HandleDialogueComplete);
        _isListeningToDialogueComplete = false;
    }

    private void DetachStoreClosedListener()
    {
        if (!_isListeningToStoreClosed || _activeStoreManager == null)
            return;

        _activeStoreManager.OnStoreClosed -= HandleStoreClosed;
        _isListeningToStoreClosed = false;
    }

    private void CloseNonPanelEndpointSilently()
    {
        if (_activeInteractionType == NPCInteractionType.Talk
            && _dialogueRunner != null
            && _dialogueRunner.IsDialogueRunning)
        {
            StopDialogueSilently(_dialogueRunner);
        }
        else if (_activeInteractionType == NPCInteractionType.OpenStore)
        {
            _activeStoreManager?.CloseStore();
        }
    }

    private async void StopDialogueSilently(DialogueRunner runner)
    {
        try
        {
            await runner.Stop();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void ClearSessionState()
    {
        _activeInteractionType = NPCInteractionType.None;
        _hasActiveSession = false;
        _isMenuOpen = false;
        _activeStoreManager = null;
    }

    private void AbortSessionBecauseInteractableDisabled()
    {
        if (!_hasActiveSession || _isEndingSession)
            return;

        _isEndingSession = true;
        DetachEndpointListeners();
        CloseNonPanelEndpointSilently();

        IInteractablePanel activePanel = _panel;
        _panel = null;
        activePanel?.ClosePanel();

        ClearSessionState();
        NotifyPanelClosed();
    }

    private NPCInteractionMenuUI ResolveInteractionMenu()
    {
        return _interactionMenuUI != null
            ? _interactionMenuUI
            : NPCInteractionMenuUI.Instance;
    }

    private DialogueRunner ResolveDialogueRunner()
    {
        if (_dialogueRunner != null)
            return _dialogueRunner;

        return FindFirstObjectByType<DialogueRunner>();
    }
}
