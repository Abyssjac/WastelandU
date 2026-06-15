using System.Collections.Generic;
using UnityEngine;

namespace JackyUtility
{
    public class PropertyDatabaseManager : MonoBehaviour
    {
        public static PropertyDatabaseManager Instance { get; private set; }
        [SerializeField] private ScriptableObject[] allDatabases;
        private Dictionary<System.Type, ScriptableObject> databaseMap = new Dictionary<System.Type, ScriptableObject>();
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitDatabaseMap();
        }

        private void InitDatabaseMap()
        {
            for (int i = 0; i < allDatabases.Length; i++)
            {
                var db = allDatabases[i];
                if (db == null) continue;
                var type = db.GetType();
                if (!databaseMap.ContainsKey(type))
                    databaseMap.Add(type, db);
            }
        }

        public T GetDatabase<T>() where T : ScriptableObject
        {
            if (databaseMap != null && databaseMap.TryGetValue(typeof(T), out var so))
                return so as T;

            Debug.LogError($"[PropertyDatabaseManager] No database found for type {typeof(T).Name}");
            return null;
        }
    }
}