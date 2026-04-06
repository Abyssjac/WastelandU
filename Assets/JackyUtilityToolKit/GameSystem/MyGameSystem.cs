using UnityEngine;


public class MyGameSystem : MonoBehaviour
{
    public static MyGameSystem Instance { get; private set; }
    public GameObject[] allSystemPrefabSingletons;

    private void Awake()
    {
        Debug.Log("<color=yellow>MyGameSystem Awake: Instantiating all system prefab singletons. </color>");

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        foreach (var prefab in allSystemPrefabSingletons)
        {
            if (prefab != null)
            {
                Instantiate(prefab);
            }
            else
            {
                Debug.LogWarning("One of the Prefab Singletons in the array is not assigned in MyGameSystem.");
            }
        }
    }

    private void OnDisable()
    {
        Debug.LogError("<color=red>Severe Error; MyGameSystem is Disabled.</color>");
    }
    private void OnDestroy()
    {
        Debug.LogError("<color=red>Severe Error; MyGameSystem is Destroyed.</color>");
    }
}
