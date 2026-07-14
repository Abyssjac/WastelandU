using System.Collections.Generic;
using UnityEngine;

public class FlightVisualObjectPool : MonoBehaviour
{
    [SerializeField] private Transform poolRoot;

    private readonly Dictionary<GameObject, Queue<GameObject>> availableByPrefab = new Dictionary<GameObject, Queue<GameObject>>();
    private readonly Dictionary<GameObject, GameObject> prefabByInstance = new Dictionary<GameObject, GameObject>();
    private readonly List<GameObject> activeInstances = new List<GameObject>();

    private Transform PoolRoot => poolRoot != null ? poolRoot : transform;

    public T Acquire<T>(T prefab, Transform parent) where T : Component
    {
        if (prefab == null)
            return null;

        GameObject prefabObject = prefab.gameObject;
        GameObject instance = GetAvailableInstance(prefabObject);
        if (instance == null)
        {
            instance = Instantiate(prefabObject, parent);
            prefabByInstance[instance] = prefabObject;
        }
        else
        {
            instance.transform.SetParent(parent, false);
        }

        if (!activeInstances.Contains(instance))
            activeInstances.Add(instance);

        instance.SetActive(true);
        return instance.GetComponent<T>();
    }

    public void Release(Component instance)
    {
        if (instance == null)
            return;

        GameObject instanceObject = instance.gameObject;
        if (!prefabByInstance.TryGetValue(instanceObject, out GameObject prefabObject))
        {
            Debug.LogWarning($"[FlightVisualObjectPool] Instance {instanceObject.name} was not acquired from this pool.", instanceObject);
            return;
        }

        activeInstances.Remove(instanceObject);
        instanceObject.transform.SetParent(PoolRoot, false);
        instanceObject.SetActive(false);

        if (!availableByPrefab.TryGetValue(prefabObject, out Queue<GameObject> queue))
        {
            queue = new Queue<GameObject>();
            availableByPrefab.Add(prefabObject, queue);
        }

        queue.Enqueue(instanceObject);
    }

    public void Prewarm<T>(T prefab, int count) where T : Component
    {
        if (prefab == null || count <= 0)
            return;

        for (int i = 0; i < count; i++)
        {
            T instance = Acquire(prefab, PoolRoot);
            Release(instance);
        }
    }

    public void ReleaseAll()
    {
        for (int i = activeInstances.Count - 1; i >= 0; i--)
        {
            GameObject instance = activeInstances[i];
            if (instance == null)
            {
                activeInstances.RemoveAt(i);
                continue;
            }

            Component component = instance.transform;
            Release(component);
        }
    }

    private GameObject GetAvailableInstance(GameObject prefabObject)
    {
        if (!availableByPrefab.TryGetValue(prefabObject, out Queue<GameObject> queue))
            return null;

        while (queue.Count > 0)
        {
            GameObject instance = queue.Dequeue();
            if (instance != null)
                return instance;
        }

        return null;
    }
}
