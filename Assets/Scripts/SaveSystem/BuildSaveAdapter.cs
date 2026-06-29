using UnityEngine;

/// <summary>
/// Save adapter for the base-building system. It converts BuildManager runtime state
/// to and from the build save section without making BuildManager depend on the save system.
/// </summary>
public class BuildSaveAdapter : MonoBehaviour, IGameSavable
{
    [SerializeField] private BuildManager buildManager;

    private bool registered;

    public string SaveKey => "build";
    public int SaveVersion => 1;
    public int LoadOrder => 0;

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
        if (!registered || GameSaveManager.Instance == null) return;

        GameSaveManager.Instance.UnregisterSavable(this);
        registered = false;
    }

    public string CaptureSaveJson()
    {
        var bm = ResolveBuildManager();
        if (bm == null)
        {
            Debug.LogWarning("[BuildSaveAdapter] BuildManager not found. Saving an empty build section.");
            return JsonUtility.ToJson(new BuildSaveData(), prettyPrint: true);
        }

        var data = new BuildSaveData
        {
            instanceCounterSnapshot = bm.InstanceCounter
        };

        foreach (var kvp in bm.Grid.AllPlaced)
        {
            var placed = kvp.Value;
            if (placed == null || placed.Property == null) continue;

            data.entries.Add(new BuildableSaveEntry(
                placed.InstanceId,
                placed.Property.EnumKey,
                placed.AnchorCell,
                placed.RotationStep
            ));
        }

        return JsonUtility.ToJson(data, prettyPrint: true);
    }

    public void RestoreSaveJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            OnNoSaveData();
            return;
        }

        var bm = ResolveBuildManager();
        if (bm == null)
        {
            Debug.LogWarning("[BuildSaveAdapter] BuildManager not found. Cannot restore build section.");
            return;
        }

        BuildSaveData data = JsonUtility.FromJson<BuildSaveData>(json);
        if (data == null)
        {
            Debug.LogError("[BuildSaveAdapter] Failed to deserialize build save data.");
            return;
        }

        bm.RestoreFromSaveData(data);
    }

    public void OnNoSaveData()
    {
        var bm = ResolveBuildManager();
        if (bm == null)
        {
            Debug.LogWarning("[BuildSaveAdapter] BuildManager not found. Cannot load preset.");
            return;
        }

        bm.LoadPreset();
    }

    private void TryRegister()
    {
        if (registered || GameSaveManager.Instance == null) return;

        GameSaveManager.Instance.RegisterSavable(this);
        registered = true;
    }

    private BuildManager ResolveBuildManager()
    {
        if (buildManager != null) return buildManager;

        buildManager = GetComponent<BuildManager>();
        if (buildManager != null) return buildManager;

        buildManager = BuildManager.Instance;
        return buildManager;
    }
}
