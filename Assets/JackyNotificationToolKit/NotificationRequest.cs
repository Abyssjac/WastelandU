using UnityEngine;

/// <summary>Identifies the visual delivery area for a notification.</summary>
public enum NotificationChannel
{
    SideToast = 0,
}

/// <summary>Identifies which toast prefab renders a notification.</summary>
public enum NotificationStyle
{
    ItemDelta = 0,
    QuestPaper = 1,
}

/// <summary>Controls a notification's vertical ordering inside its channel.</summary>
public enum NotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
}

/// <summary>
/// UI-ready data for one notification. Producers provide display data only; they never choose or
/// instantiate a toast prefab.
/// </summary>
public class NotificationRequest
{
    public const float DefaultDuration = 3f;

    public NotificationChannel channel = NotificationChannel.SideToast;
    public NotificationStyle style;
    public NotificationPriority priority;
    public float duration = DefaultDuration;

    public string title;
    public string detail;
    public Sprite icon;
    public int amount;

    public static NotificationRequest CreateItemDelta(
        Sprite icon,
        string itemName,
        int delta,
        NotificationPriority priority = NotificationPriority.Low)
    {
        return new NotificationRequest
        {
            channel = NotificationChannel.SideToast,
            style = NotificationStyle.ItemDelta,
            priority = priority,
            title = itemName,
            icon = icon,
            amount = delta,
        };
    }

    public static NotificationRequest CreateQuestPaper(
        string title,
        string detail,
        NotificationPriority priority = NotificationPriority.Normal)
    {
        return new NotificationRequest
        {
            channel = NotificationChannel.SideToast,
            style = NotificationStyle.QuestPaper,
            priority = priority,
            title = title,
            detail = detail,
        };
    }
}

/// <summary>Read-only snapshot of one active notification for debug tooling.</summary>
public struct NotificationActiveInfo
{
    public NotificationRequest request;
    public float remainingTime;
}
