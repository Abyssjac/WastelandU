using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the current in-game day and session on the UI.
/// Subscribes to <see cref="DayNightManager.OnSessionChanged"/> and
/// <see cref="DayNightManager.OnNewDayStarted"/> to stay up to date.
/// Uses OnEnable/OnDisable so that subscription is re-established
/// whenever the GameObject is activated, regardless of script execution order.
/// </summary>
public class DayNightManagerUI : MonoBehaviour
{
    // ©¤©¤ Inspector ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Header("Display")]
    [Tooltip("Shows the current day number, e.g. \"Day 1\".")]
    [SerializeField] private TextMeshProUGUI dayCountText;

    [Tooltip("Shows the current session, e.g. \"Morning\".")]
    [SerializeField] private TextMeshProUGUI daySessionText;

    [Header("Button Bind")]
    [Tooltip("Button to advance to the next session (for testing).")]
    [SerializeField] private Button advanceSessionButton;

    // ©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void Start()
    {
        if (DayNightManager.Instance == null)
        {
            Debug.LogWarning("[DayNightManagerUI] DayNightManager instance not found.");
            return;
        }

        DayNightManager.Instance.OnNewDayStarted += HandleNewDay;
        DayNightManager.Instance.OnSessionChanged += HandleSessionChanged;

        // Populate initial values
        RefreshDayCount(DayNightManager.Instance.CurrentDay);
        RefreshSession(DayNightManager.Instance.CurrentSession);

        if (advanceSessionButton != null)
            advanceSessionButton.onClick.AddListener(() => DayNightManager.Instance.AdvanceSession());
    }

    private void OnDestroy()
    {
        if (DayNightManager.Instance != null)
        {
            DayNightManager.Instance.OnNewDayStarted -= HandleNewDay;
            DayNightManager.Instance.OnSessionChanged -= HandleSessionChanged;
        }
    }
    // ©¤©¤ Private helpers ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void HandleNewDay(int day)
    {
        RefreshDayCount(day);
    }

    private void HandleSessionChanged(DaySession session)
    {
        RefreshSession(session);
    }

    private void RefreshDayCount(int day)
    {
        if (dayCountText != null)
            dayCountText.text = $"{day}";
    }

    private void RefreshSession(DaySession session)
    {
        if (daySessionText != null)
            daySessionText.text = session.ToString();
    }
}
