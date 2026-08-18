using UnityEngine;

/// <summary>
/// Bridges <see cref="FlightManager"/> progress to the project's unified IGameSavable pipeline.
/// Attach beside FlightManager on the persistent Flight manager object.
/// </summary>
[DisallowMultipleComponent]
public class FlightSaveAdapter : MonoBehaviour, IGameSavable
{
    [SerializeField] private FlightManager flightManager;

    private bool registered;

    public string SaveKey => "flight";
    public int SaveVersion => 1;
    public int LoadOrder => 120;

    private void OnEnable()
    {
        TryRegister();
    }

    private void Start()
    {
        TryRegister();
    }

    private void OnDisable()
    {
        if (!registered || GameSaveManager.Instance == null)
            return;

        GameSaveManager.Instance.UnregisterSavable(this);
        registered = false;
    }

    public string CaptureSaveJson()
    {
        FlightManager manager = ResolveFlightManager();
        FlightSaveData data = manager != null ? manager.CaptureSaveData() : new FlightSaveData();
        return JsonUtility.ToJson(data, prettyPrint: true);
    }

    public void RestoreSaveJson(string json)
    {
        FlightManager manager = ResolveFlightManager();
        if (manager == null)
        {
            Debug.LogWarning($"[{nameof(FlightSaveAdapter)}] {nameof(FlightManager)} is unavailable during restore.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            OnNoSaveData();
            return;
        }

        FlightSaveData data = JsonUtility.FromJson<FlightSaveData>(json);
        if (data == null || !manager.RestoreFromSaveData(data))
        {
            Debug.LogWarning($"[{nameof(FlightSaveAdapter)}] Flight save could not be restored; using the authored initial island.", this);
            manager.RestoreDefaultState();
        }
    }

    public void OnNoSaveData()
    {
        ResolveFlightManager()?.RestoreDefaultState();
    }

    private void TryRegister()
    {
        if (registered || GameSaveManager.Instance == null)
            return;

        GameSaveManager.Instance.RegisterSavable(this);
        registered = true;
    }

    private FlightManager ResolveFlightManager()
    {
        if (flightManager == null)
            flightManager = GetComponent<FlightManager>();

        if (flightManager == null)
            flightManager = FlightManager.Instance;

        return flightManager;
    }
}
