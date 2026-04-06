using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Databases")]
    [SerializeField] private AllModuleDatabase moduleDatabase;

    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GameManager>();
                if (_instance == null)
                {
                    var go = new GameObject("GameManager");
                    _instance = go.AddComponent<GameManager>();
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Centralized lookup for ModuleData by id.
    /// Future-proof entry point if you later add more databases.
    /// </summary>
    public ModuleData GetModuleData(string moduleDataId)
    {
        if (string.IsNullOrEmpty(moduleDataId)) return null;

        if (moduleDatabase == null)
        {
            Debug.LogError("[GameManager] moduleDatabase is not assigned.", this);
            return null;
        }

        var data = moduleDatabase.GetById(moduleDataId);
        if (data == null)
            Debug.LogWarning($"[GameManager] ModuleData not found for id='{moduleDataId}'.", this);

        return data;
    }

    public bool TryGetModuleData(string moduleDataId, out ModuleData moduleData)
    {
        moduleData = null;

        if (string.IsNullOrEmpty(moduleDataId)) return false;

        if (moduleDatabase == null)
        {
            Debug.LogError("[GameManager] moduleDatabase is not assigned.", this);
            return false;
        }

        return moduleDatabase.TryGetById(moduleDataId, out moduleData);
    }

    public AllModuleDatabase ModuleDatabase => moduleDatabase;
}