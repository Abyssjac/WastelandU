using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stack panel used while the player chooses an NPC room in the world.
/// Highlight spawning and pending room state remain owned by NPCRoomAssignmentManager.
/// </summary>
public class NPCRoomSelectionPanelUI : MonoBehaviour, IGeneralPanelOwner
{
    public static NPCRoomSelectionPanelUI Instance { get; private set; }

    [Header("Panel Root")]
    [SerializeField] private GameObject panelRoot;

    [Header("Display")]
    [SerializeField] private TextMeshProUGUI npcNameLabel;

    [Header("Buttons")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    private Key_NPC _targetNpcKey = Key_NPC.None;
    private string _targetNpcName = string.Empty;
    private bool _selectionActive;
    private bool _confirmed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(HandleConfirmClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(HandleCancelClicked);
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(HandleConfirmClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(HandleCancelClicked);

        if (Instance == this)
            Instance = null;
    }

    public void Prepare(Key_NPC npcKey, string npcName)
    {
        _targetNpcKey = npcKey;
        _targetNpcName = npcName;
        _confirmed = false;
    }

    public void OnPanelOpenRequested()
    {
        if (panelRoot != null)
            panelRoot.SetActive(true);

        if (npcNameLabel != null)
            npcNameLabel.text = _targetNpcName;

        _confirmed = false;
        _selectionActive = false;

        if (_targetNpcKey == Key_NPC.None)
        {
            Debug.LogWarning("[NPCRoomSelectionPanelUI] Cannot open room selection without a valid NPC key.");
            return;
        }

        if (NPCRoomAssignmentManager.Instance == null)
        {
            Debug.LogWarning("[NPCRoomSelectionPanelUI] NPCRoomAssignmentManager.Instance is null.");
            return;
        }

        NPCRoomAssignmentManager.Instance.EnterSelectRoomMode(_targetNpcKey);
        _selectionActive = true;
    }

    public void OnPanelCloseRequested()
    {
        if (_selectionActive && !_confirmed)
            NPCRoomAssignmentManager.Instance?.CancelSelectRoomMode();

        _selectionActive = false;
        _confirmed = false;
        _targetNpcKey = Key_NPC.None;
        _targetNpcName = string.Empty;

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void HandleConfirmClicked()
    {
        if (_selectionActive)
        {
            _confirmed = true;
            NPCRoomAssignmentManager.Instance?.ExitSelectRoomMode();
            _selectionActive = false;
        }

        RequestClose();
    }

    private void HandleCancelClicked()
    {
        if (_selectionActive)
        {
            NPCRoomAssignmentManager.Instance?.CancelSelectRoomMode();
            _selectionActive = false;
        }

        RequestClose();
    }

    private void RequestClose()
    {
        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestClose(this);
        else
            OnPanelCloseRequested();
    }
}
