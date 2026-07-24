using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Read-only quest log. It displays only Ongoing and Completed quests; accepting and submitting
/// remain external actions handled through QuestManager.
/// </summary>
public class QuestPanelUI : MonoBehaviour, IGeneralPanelOwner
{
    [Header("Panel")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Button openQuestButton;
    [SerializeField] private Button closeButton;

    [Header("Quest List")]
    [SerializeField] private Transform listRoot;
    [SerializeField] private QuestListItemUI listItemPrefab;

    [Header("Quest Detail")]
    [SerializeField] private QuestDetailUI detailUI;

    private readonly List<QuestListItemUI> spawnedListItems = new List<QuestListItemUI>();
    private QuestManager boundManager;
    private Key_Quest selectedQuestKey = Key_Quest.None;
    private bool isOpen;

    private void Awake()
    {
        if (panelRoot != null)
            panelRoot.SetActive(false);

        if (openQuestButton != null)
            openQuestButton.onClick.AddListener(RequestOpenPanel);

        if (closeButton != null)
            closeButton.onClick.AddListener(RequestClose);
    }

    private void Start()
    {
        TryBindManager();
    }

    private void OnEnable()
    {
        TryBindManager();
    }

    private void OnDisable()
    {
        UnbindManager();
    }

    private void OnDestroy()
    {
        if (openQuestButton != null)
            openQuestButton.onClick.RemoveListener(RequestOpenPanel);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(RequestClose);
    }

    /// <summary>Opens the quest log through the common panel stack when it is available.</summary>
    public void RequestOpenPanel()
    {
        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestOpen(this, PanelOpenType.Override);
        else
            OnPanelOpenRequested();
    }

    public void OnPanelOpenRequested()
    {
        isOpen = true;
        if (panelRoot != null)
            panelRoot.SetActive(true);

        TryBindManager();
        Refresh();
    }

    public void OnPanelCloseRequested()
    {
        isOpen = false;
        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    public void Refresh()
    {
        if (!isOpen)
            return;

        TryBindManager();
        if (boundManager == null)
        {
            detailUI?.Clear();
            return;
        }

        RebuildList();

        if (selectedQuestKey != Key_Quest.None
            && boundManager.GetVisibleQuestKeys().Contains(selectedQuestKey))
        {
            detailUI?.Show(boundManager, selectedQuestKey);
        }
        else
        {
            selectedQuestKey = Key_Quest.None;
            detailUI?.Clear();
        }
    }

    private void RebuildList()
    {
        ClearListItems();
        if (listRoot == null || listItemPrefab == null)
            return;

        List<Key_Quest> visibleKeys = boundManager.GetVisibleQuestKeys();
        for (int i = 0; i < visibleKeys.Count; i++)
        {
            Key_Quest questKey = visibleKeys[i];
            if (!boundManager.TryGetQuestProperty(questKey, out QuestProperty property)
                || !boundManager.TryGetQuestState(questKey, out QuestProperty.QuestState state))
            {
                continue;
            }

            QuestListItemUI listItem = Instantiate(listItemPrefab, listRoot);
            listItem.Bind(questKey, property, state, SelectQuest);
            spawnedListItems.Add(listItem);
        }
    }

    private void SelectQuest(Key_Quest questKey)
    {
        selectedQuestKey = questKey;
        detailUI?.Show(boundManager, selectedQuestKey);
    }

    private void RequestClose()
    {
        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestClose(this);
        else
            OnPanelCloseRequested();
    }

    private void TryBindManager()
    {
        if (boundManager != null || QuestManager.Instance == null)
            return;

        boundManager = QuestManager.Instance;
        boundManager.OnQuestAccepted += HandleQuestChanged;
        boundManager.OnQuestProgressChanged += HandleQuestChanged;
        boundManager.OnQuestStateChanged += HandleQuestStateChanged;
        boundManager.OnQuestSubmitted += HandleQuestChanged;
    }

    private void UnbindManager()
    {
        if (boundManager == null)
            return;

        boundManager.OnQuestAccepted -= HandleQuestChanged;
        boundManager.OnQuestProgressChanged -= HandleQuestChanged;
        boundManager.OnQuestStateChanged -= HandleQuestStateChanged;
        boundManager.OnQuestSubmitted -= HandleQuestChanged;
        boundManager = null;
    }

    private void HandleQuestChanged(Key_Quest _)
    {
        Refresh();
    }

    private void HandleQuestStateChanged(
        Key_Quest _,
        QuestProperty.QuestState __,
        QuestProperty.QuestState ___)
    {
        Refresh();
    }

    private void ClearListItems()
    {
        for (int i = 0; i < spawnedListItems.Count; i++)
        {
            if (spawnedListItems[i] != null)
                Destroy(spawnedListItems[i].gameObject);
        }

        spawnedListItems.Clear();
    }
}
