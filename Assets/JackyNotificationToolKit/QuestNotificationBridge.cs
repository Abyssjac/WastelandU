using UnityEngine;

/// <summary>Converts QuestManager events into normal-priority QuestPaper notifications.</summary>
[DisallowMultipleComponent]
public class QuestNotificationBridge : MonoBehaviour
{
    private QuestManager boundQuestManager;

    private void OnEnable()
    {
        TryBindQuestManager();
    }

    private void Start()
    {
        TryBindQuestManager();
    }

    private void Update()
    {
        if (boundQuestManager == null)
            TryBindQuestManager();
    }

    private void OnDisable()
    {
        UnbindQuestManager();
    }

    private void HandleQuestAccepted(Key_Quest questKey)
    {
        ShowQuestPaper(questKey, "任务已接取");
    }

    private void HandleQuestStateChanged(
        Key_Quest questKey,
        QuestProperty.QuestState previousState,
        QuestProperty.QuestState currentState)
    {
        if (previousState == QuestProperty.QuestState.Ongoing
            && currentState == QuestProperty.QuestState.Completed)
        {
            ShowQuestPaper(questKey, "任务目标已完成");
        }
    }

    private void HandleQuestSubmitted(Key_Quest questKey)
    {
        ShowQuestPaper(questKey, "任务已交付");
    }

    private void ShowQuestPaper(Key_Quest questKey, string title)
    {
        if (NotificationManager.Instance == null || boundQuestManager == null)
            return;

        string detail = questKey.ToString();
        if (boundQuestManager.TryGetQuestProperty(questKey, out QuestProperty property)
            && property != null
            && !string.IsNullOrWhiteSpace(property.displayName))
        {
            detail = property.displayName;
        }

        NotificationManager.Instance.Show(NotificationRequest.CreateQuestPaper(title, detail));
    }

    private void TryBindQuestManager()
    {
        if (boundQuestManager != null || QuestManager.Instance == null)
            return;

        boundQuestManager = QuestManager.Instance;
        boundQuestManager.OnQuestAccepted += HandleQuestAccepted;
        boundQuestManager.OnQuestStateChanged += HandleQuestStateChanged;
        boundQuestManager.OnQuestSubmitted += HandleQuestSubmitted;
    }

    private void UnbindQuestManager()
    {
        if (boundQuestManager == null)
            return;

        boundQuestManager.OnQuestAccepted -= HandleQuestAccepted;
        boundQuestManager.OnQuestStateChanged -= HandleQuestStateChanged;
        boundQuestManager.OnQuestSubmitted -= HandleQuestSubmitted;
        boundQuestManager = null;
    }
}
