using System;
using UnityEngine;

/// <summary>
/// Controls the in-game time progression.
/// Each day is split into three sessions: Morning ¡ú Noon ¡ú Night ¡ú (next day) Morning.
/// Advancing is driven externally (e.g. a UI button) via <see cref="AdvanceSession"/>.
///
/// Singleton ¡ª place on a DontDestroyOnLoad GameObject.
/// </summary>
public class DayNightManager : MonoBehaviour
{
    // ©¤©¤ Singleton ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    public static DayNightManager Instance { get; private set; }

    // ©¤©¤ Inspector ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Header("Start State")]
    [SerializeField] private int startDay = 1;

    //[Header("UI")]
    //[SerializeField] private GameObject dayNightManagerUIPrefab;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled;

    // ©¤©¤ State ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    /// <summary>The current in-game day number (starts at <see cref="startDay"/>).</summary>
    public int        CurrentDay     { get; private set; }

    /// <summary>The current session within the day.</summary>
    public DaySession CurrentSession { get; private set; }

    // ©¤©¤ Events ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    /// <summary>Fired every time the session changes, including when a new day begins.</summary>
    public event Action<DaySession> OnSessionChanged;

    /// <summary>
    /// Fired at the start of every new day (after Night rolls over to Morning).
    /// Provides the new day number.
    /// </summary>
    public event Action<int> OnNewDayStarted;

    // ©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CurrentDay     = startDay;
        CurrentSession = DaySession.Morning;

        //if (dayNightManagerUIPrefab != null)
        //{
        //    Canvas canvas = FindFirstObjectByType<Canvas>();
        //    if (canvas != null)
        //    {
        //        Instantiate(dayNightManagerUIPrefab, canvas.transform);
        //    }
        //    else
        //    {
        //        Instantiate(dayNightManagerUIPrefab);
        //    }   
        //}
    }

    // ©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Advances the session by one step.
    /// Cycle: Morning ¡ú Noon ¡ú Night ¡ú Morning (new day).
    /// When a new day begins, <see cref="OnNewDayStarted"/> fires before <see cref="OnSessionChanged"/>.
    /// </summary>
    public void AdvanceSession()
    {
        if (CurrentSession == DaySession.Night)
        {
            CurrentDay++;
            CurrentSession = DaySession.Morning;

            if (debugEnabled)
                Debug.Log($"[DayNightManager] New day started: Day {CurrentDay}");

            OnNewDayStarted?.Invoke(CurrentDay);
        }
        else
        {
            CurrentSession = (DaySession)((int)CurrentSession + 1);

            if (debugEnabled)
                Debug.Log($"[DayNightManager] Session advanced to: {CurrentSession} (Day {CurrentDay})");
        }

        OnSessionChanged?.Invoke(CurrentSession);
    }
}
