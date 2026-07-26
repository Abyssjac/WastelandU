using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent authority for side-toast notifications. It owns active lifetime, priority ordering,
/// maximum visible count, and toast prefab creation.
/// </summary>
[DisallowMultipleComponent]
public class NotificationManager : MonoBehaviour
{
    private sealed class ActiveNotification
    {
        public NotificationRequest request;
        public NotificationToastUI toastUI;
        public float remainingTime;
    }

    public static NotificationManager Instance { get; private set; }

    [Header("Side Toast")]
    [SerializeField] private RectTransform sideToastRoot;
    [SerializeField, Min(1)] private int maxVisibleCount = 3;

    [Header("Toast Prefabs")]
    [SerializeField] private ItemDeltaToastUI itemDeltaToastPrefab;
    [SerializeField] private QuestPaperToastUI questPaperToastPrefab;

    private readonly List<ActiveNotification> activeNotifications = new List<ActiveNotification>();

    public RectTransform SideToastRoot => sideToastRoot;
    public int MaxVisibleCount => maxVisibleCount;
    public int ActiveNotificationCount => activeNotifications.Count;
    public bool HasItemDeltaToastPrefab => itemDeltaToastPrefab != null;
    public bool HasQuestPaperToastPrefab => questPaperToastPrefab != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        for (int i = activeNotifications.Count - 1; i >= 0; i--)
        {
            ActiveNotification active = activeNotifications[i];
            active.remainingTime -= deltaTime;
            if (active.remainingTime <= 0f)
                RemoveAt(i);
        }

        EnforceVisibleLimit();
    }

    private void OnValidate()
    {
        maxVisibleCount = Mathf.Max(1, maxVisibleCount);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Displays a request immediately. Newer notifications are inserted above older notifications
    /// with the same priority; lower-priority notifications appear below higher-priority ones.
    /// </summary>
    public bool Show(NotificationRequest request)
    {
        if (request == null)
        {
            Debug.LogWarning($"[{nameof(NotificationManager)}] Ignored a null notification request.", this);
            return false;
        }

        if (request.channel != NotificationChannel.SideToast)
        {
            Debug.LogWarning($"[{nameof(NotificationManager)}] Unsupported notification channel '{request.channel}'.", this);
            return false;
        }

        if (request.style == NotificationStyle.ItemDelta && request.amount == 0)
        {
            Debug.LogWarning($"[{nameof(NotificationManager)}] Ignored an ItemDelta notification with amount 0.", this);
            return false;
        }

        if (sideToastRoot == null)
        {
            Debug.LogError($"[{nameof(NotificationManager)}] {nameof(sideToastRoot)} is not assigned.", this);
            return false;
        }

        NotificationToastUI prefab = GetToastPrefab(request.style);
        if (prefab == null)
        {
            Debug.LogError($"[{nameof(NotificationManager)}] No toast prefab is assigned for style '{request.style}'.", this);
            return false;
        }

        request.duration = request.duration > 0f
            ? request.duration
            : NotificationRequest.DefaultDuration;

        int insertionIndex = FindInsertionIndex(request.priority);
        NotificationToastUI toastUI = Instantiate(prefab, sideToastRoot);
        toastUI.Bind(request);

        var active = new ActiveNotification
        {
            request = request,
            toastUI = toastUI,
            remainingTime = request.duration,
        };

        activeNotifications.Insert(insertionIndex, active);
        toastUI.transform.SetSiblingIndex(insertionIndex);
        EnforceVisibleLimit();
        return true;
    }

    /// <summary>Returns a display-order snapshot: index 0 is the top-most notification.</summary>
    public List<NotificationActiveInfo> GetActiveNotifications()
    {
        var result = new List<NotificationActiveInfo>(activeNotifications.Count);
        for (int i = 0; i < activeNotifications.Count; i++)
        {
            ActiveNotification active = activeNotifications[i];
            result.Add(new NotificationActiveInfo
            {
                request = active.request,
                remainingTime = Mathf.Max(0f, active.remainingTime),
            });
        }

        return result;
    }

    /// <summary>Debug-only convenience operation that removes all currently visible notifications.</summary>
    public void ClearAllNotificationsForDebug()
    {
        for (int i = activeNotifications.Count - 1; i >= 0; i--)
            RemoveAt(i);
    }

    private NotificationToastUI GetToastPrefab(NotificationStyle style)
    {
        switch (style)
        {
            case NotificationStyle.ItemDelta:
                return itemDeltaToastPrefab;

            case NotificationStyle.QuestPaper:
                return questPaperToastPrefab;

            default:
                return null;
        }
    }

    private int FindInsertionIndex(NotificationPriority priority)
    {
        for (int i = 0; i < activeNotifications.Count; i++)
        {
            if (priority >= activeNotifications[i].request.priority)
                return i;
        }

        return activeNotifications.Count;
    }

    private void EnforceVisibleLimit()
    {
        int limit = Mathf.Max(1, maxVisibleCount);
        while (activeNotifications.Count > limit)
            RemoveAt(activeNotifications.Count - 1);
    }

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= activeNotifications.Count)
            return;

        ActiveNotification active = activeNotifications[index];
        activeNotifications.RemoveAt(index);

        if (active.toastUI == null)
            return;

        active.toastUI.gameObject.SetActive(false);
        Destroy(active.toastUI.gameObject);
    }
}
