using JackyUtility;
using UnityEditor;
using UnityEngine;

/// <summary>Play-mode debug window for testing notification ordering, limits, and lifetime.</summary>
public class NotificationManagerDebugWindow : DebugEditorWindow<NotificationManager>
{
    private Sprite itemTestIcon;
    private string itemTestName = "Test Item";
    private int itemTestAmount = 1;
    private NotificationPriority itemTestPriority = NotificationPriority.Low;

    private string questTestTitle = "任务已接取";
    private string questTestDetail = "测试任务";
    private NotificationPriority questTestPriority = NotificationPriority.Normal;

    [MenuItem("Wasteland Debug/Notification Manager")]
    public static void ShowWindow()
    {
        GetWindow<NotificationManagerDebugWindow>("Notification Manager Debug").Show();
    }

    protected override void DrawContent()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to test runtime notifications.", MessageType.Info);
            return;
        }

        NotificationManager manager = Target;
        DrawRuntimeSummary(manager);
        DrawItemDeltaTest(manager);
        DrawQuestPaperTest(manager);
        DrawActiveNotifications(manager);
    }

    private void DrawRuntimeSummary(NotificationManager manager)
    {
        Header("Runtime Summary");
        Row("Channel", NotificationChannel.SideToast.ToString());
        Row("Max Visible Count", manager.MaxVisibleCount.ToString());
        Row("Active Count", $"{manager.ActiveNotificationCount} / {manager.MaxVisibleCount}");
        Row("Side Toast Root", manager.SideToastRoot != null ? manager.SideToastRoot.name : "<missing>");
        Row("ItemDelta Prefab", manager.HasItemDeltaToastPrefab ? "Assigned" : "<missing>");
        Row("QuestPaper Prefab", manager.HasQuestPaperToastPrefab ? "Assigned" : "<missing>");
        Row("Default Duration", $"{NotificationRequest.DefaultDuration:0.0}s");
    }

    private void DrawItemDeltaTest(NotificationManager manager)
    {
        EditorGUILayout.Space(6);
        Header("Send ItemDelta Test");

        itemTestIcon = (Sprite)EditorGUILayout.ObjectField("Icon", itemTestIcon, typeof(Sprite), false);
        itemTestName = EditorGUILayout.TextField("Item Name", itemTestName);
        itemTestAmount = EditorGUILayout.IntField("Delta", itemTestAmount);
        itemTestPriority = (NotificationPriority)EditorGUILayout.EnumPopup("Priority", itemTestPriority);

        using (new EditorGUI.DisabledScope(itemTestAmount == 0))
        {
            if (GUILayout.Button("Send ItemDelta", GUILayout.Height(22)))
            {
                manager.Show(NotificationRequest.CreateItemDelta(
                    itemTestIcon,
                    itemTestName,
                    itemTestAmount,
                    itemTestPriority));
            }
        }
    }

    private void DrawQuestPaperTest(NotificationManager manager)
    {
        EditorGUILayout.Space(6);
        Header("Send QuestPaper Test");

        questTestTitle = EditorGUILayout.TextField("Title", questTestTitle);
        questTestDetail = EditorGUILayout.TextField("Detail", questTestDetail);
        questTestPriority = (NotificationPriority)EditorGUILayout.EnumPopup("Priority", questTestPriority);

        if (GUILayout.Button("Send QuestPaper", GUILayout.Height(22)))
        {
            manager.Show(NotificationRequest.CreateQuestPaper(
                questTestTitle,
                questTestDetail,
                questTestPriority));
        }
    }

    private void DrawActiveNotifications(NotificationManager manager)
    {
        EditorGUILayout.Space(6);
        Header("Active Notifications (Top To Bottom)");

        if (GUILayout.Button("Clear All Active Notifications", GUILayout.Height(22)))
            manager.ClearAllNotificationsForDebug();

        var active = manager.GetActiveNotifications();
        if (active.Count == 0)
        {
            EditorGUILayout.HelpBox("No active notifications.", MessageType.None);
            return;
        }

        for (int i = 0; i < active.Count; i++)
        {
            NotificationActiveInfo info = active[i];
            NotificationRequest request = info.request;
            string title = string.IsNullOrWhiteSpace(request.title) ? "<untitled>" : request.title;
            string detail = request.style == NotificationStyle.ItemDelta
                ? $"{title} {(request.amount > 0 ? "+" : string.Empty)}{request.amount}"
                : $"{title} | {request.detail}";

            ColoredRow(
                $"[{i}] {request.priority} | {request.style}",
                $"{detail} | {info.remainingTime:0.0}s",
                GetPriorityColor(request.priority));
        }
    }

    private static Color GetPriorityColor(NotificationPriority priority)
    {
        switch (priority)
        {
            case NotificationPriority.High:
                return Color.yellow;

            case NotificationPriority.Normal:
                return Color.cyan;

            default:
                return Color.white;
        }
    }
}
