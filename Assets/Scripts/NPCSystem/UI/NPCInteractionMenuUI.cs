using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dynamic NPC option menu. A normal close ends the NPC interaction; selecting
/// an option uses <see cref="CloseForTransition"/> and keeps it alive.
/// </summary>
[DisallowMultipleComponent]
public class NPCInteractionMenuUI : MonoBehaviour, IInteractablePanel, IGeneralPanelOwner
{
    public static NPCInteractionMenuUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TextMeshProUGUI _titleLabel;
    [SerializeField] private TextMeshProUGUI _subtitleLabel;
    [SerializeField] private string _subtitleText = "Choose an interaction";

    [Header("Dynamic Options")]
    [SerializeField] private Transform _optionRoot;
    [SerializeField] private NPCInteractionOptionButton _optionButtonPrefab;

    [Header("Labels")]
    [SerializeField] private string _talkLabel = "Talk";
    [SerializeField] private string _storeLabel = "Store";
    [SerializeField] private string _npcPanelLabel = "NPC Panel";

    private readonly List<NPCInteractionType> _interactionTypes = new List<NPCInteractionType>(3);
    private readonly List<NPCInteractionOptionButton> _spawnedOptionButtons = new List<NPCInteractionOptionButton>();
    private NPCInteractable _owner;
    private bool _isVisible;
    private bool _suppressOwnerNotification;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(RequestUserClose);
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(RequestUserClose);

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (_isVisible && Input.GetKeyDown(KeyCode.Escape))
            RequestUserClose();
    }

    /// <summary>
    /// Opens this menu for one already-active NPC interaction session.
    /// </summary>
    public void OpenMenu(NPCInteractable owner, IReadOnlyList<NPCInteractionType> interactionTypes)
    {
        if (owner == null)
        {
            Debug.LogWarning($"[{nameof(NPCInteractionMenuUI)}] Cannot open without an {nameof(NPCInteractable)} owner.", this);
            return;
        }

        _owner = owner;
        _interactionTypes.Clear();

        if (interactionTypes != null)
        {
            for (int i = 0; i < interactionTypes.Count; i++)
            {
                NPCInteractionType interactionType = interactionTypes[i];
                if (interactionType != NPCInteractionType.None)
                    _interactionTypes.Add(interactionType);
            }
        }

        _suppressOwnerNotification = false;
        RefreshHeader();

        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestOpen(this, PanelOpenType.Override);
        else
            OnPanelOpenRequested();
    }

    /// <summary>
    /// Generic interface implementation. NPCInteractable normally calls
    /// <see cref="OpenMenu"/> so it can pass its already-resolved option list.
    /// </summary>
    public void OpenPanel(BasePanelInteractable owner)
    {
        NPCInteractable npcOwner = owner as NPCInteractable;
        NPCManager manager = NPCManager.Instance;

        if (npcOwner == null || manager == null)
        {
            Debug.LogWarning($"[{nameof(NPCInteractionMenuUI)}] OpenPanel requires an {nameof(NPCInteractable)} and {nameof(NPCManager)}.", this);
            owner?.NotifyPanelClosed();
            return;
        }

        OpenMenu(npcOwner, manager.GetAvailableInteractions(npcOwner.NpcKey));
    }

    /// <summary>
    /// Closes the menu only as a visual transition to a selected endpoint.
    /// It deliberately never notifies the interaction owner.
    /// </summary>
    public void CloseForTransition()
    {
        CloseWithoutOwnerNotification();
    }

    /// <summary>
    /// Called by BasePanelInteractable when the interaction ends externally.
    /// </summary>
    public void ClosePanel()
    {
        CloseWithoutOwnerNotification();
    }

    public void OnPanelOpenRequested()
    {
        _isVisible = true;

        if (_panelRoot != null)
            _panelRoot.SetActive(true);

        if (!BuildOptions())
        {
            NPCInteractable owner = _owner;
            CloseWithoutOwnerNotification();
            owner?.CancelNPCInteraction();
        }
    }

    public void OnPanelCloseRequested()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        _isVisible = false;

        NPCInteractable owner = _owner;
        ClearContext();

        if (!_suppressOwnerNotification)
            owner?.NotifyPanelClosed();
    }

    private void RequestUserClose()
    {
        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestClose(this);
        else
            OnPanelCloseRequested();
    }

    private void HandleOptionSelected(NPCInteractionType interactionType)
    {
        NPCInteractable owner = _owner;
        owner?.SelectNPCInteraction(interactionType);
    }

    private void RefreshHeader()
    {
        if (_titleLabel != null)
        {
            NPCProperty property = _owner != null && _owner.Behaviour != null
                ? _owner.Behaviour.Property
                : null;

            _titleLabel.text = property != null && !string.IsNullOrWhiteSpace(property.displayName)
                ? property.displayName
                : _owner != null ? _owner.NpcKey.ToString() : string.Empty;
        }

        if (_subtitleLabel != null)
            _subtitleLabel.text = _subtitleText;
    }

    private bool BuildOptions()
    {
        ClearOptionButtons();

        if (_interactionTypes.Count == 0)
        {
            Debug.LogWarning($"[{nameof(NPCInteractionMenuUI)}] Cannot show an empty interaction menu.", this);
            return false;
        }

        if (_optionRoot == null || _optionButtonPrefab == null)
        {
            Debug.LogError($"[{nameof(NPCInteractionMenuUI)}] Assign both Option Root and Option Button Prefab in the Inspector.", this);
            return false;
        }

        for (int i = 0; i < _interactionTypes.Count; i++)
        {
            NPCInteractionType interactionType = _interactionTypes[i];
            NPCInteractionOptionButton optionButton = Instantiate(_optionButtonPrefab, _optionRoot);
            optionButton.Bind(interactionType, GetLabel(interactionType), HandleOptionSelected);
            _spawnedOptionButtons.Add(optionButton);
        }

        return true;
    }

    private string GetLabel(NPCInteractionType interactionType)
    {
        switch (interactionType)
        {
            case NPCInteractionType.Talk:
                return _talkLabel;

            case NPCInteractionType.OpenStore:
                return _storeLabel;

            case NPCInteractionType.OpenNPCPanel:
                return _npcPanelLabel;

            default:
                return interactionType.ToString();
        }
    }

    private void CloseWithoutOwnerNotification()
    {
        if (!_isVisible && _owner == null)
            return;

        _suppressOwnerNotification = true;

        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestClose(this);
        else
            OnPanelCloseRequested();

        if (_isVisible || _owner != null)
            ForceCloseWithoutOwnerNotification();

        _suppressOwnerNotification = false;
    }

    private void ForceCloseWithoutOwnerNotification()
    {
        if (_panelRoot != null)
            _panelRoot.SetActive(false);

        _isVisible = false;
        ClearContext();
    }

    private void ClearContext()
    {
        _owner = null;
        _interactionTypes.Clear();
        ClearOptionButtons();
    }

    private void ClearOptionButtons()
    {
        for (int i = 0; i < _spawnedOptionButtons.Count; i++)
        {
            NPCInteractionOptionButton optionButton = _spawnedOptionButtons[i];
            if (optionButton != null)
                Destroy(optionButton.gameObject);
        }

        _spawnedOptionButtons.Clear();
    }
}
