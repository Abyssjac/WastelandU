using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;

public enum Key_MapDataPP
{
    None = 0,
    TestMap = 1,
    SynthesizeMap = 2,
    TutorialMap = 3,
}

[CreateAssetMenu(fileName = "MapDataPP_", menuName = "AllProperties/MapDataProperty")]
public class MapDataProperty : EnumStringKeyedProperty<Key_MapDataPP>
{
    [Header("Map")]
    [SerializeField, Min(1)] private int mapNodeCount = 1;
    [SerializeField] private Key_MapNodePP[] nodeKeys = new Key_MapNodePP[1];
    [SerializeField] private Vector2Int[] spawnPositions = new[] { new Vector2Int(1, 0) };

    public int MapNodeCount => mapNodeCount;
    public IReadOnlyList<Key_MapNodePP> NodeKeys => nodeKeys;
    public IReadOnlyList<Vector2Int> SpawnPositions => spawnPositions;

    public MapDataRuntime CreateRuntimeMapData(MapNodeDatabase nodeDatabase)
    {
        EnsureValidData();

        Key_MapNodePP[] shuffledKeys = new Key_MapNodePP[nodeKeys.Length];
        Array.Copy(nodeKeys, shuffledKeys, nodeKeys.Length);
        Shuffle(shuffledKeys);

        List<MapNodeRuntime> runtimeNodes = new List<MapNodeRuntime>(mapNodeCount);
        for (int i = 0; i < mapNodeCount; i++)
        {
            Key_MapNodePP nodeKey = shuffledKeys[i];
            MapNodeProperty property = null;

            if (nodeDatabase != null && nodeKey != Key_MapNodePP.None)
                property = nodeDatabase.GetByEnum(nodeKey);

            runtimeNodes.Add(new MapNodeRuntime(i, nodeKey, property, spawnPositions[i]));
        }

        return new MapDataRuntime(EnumKey, runtimeNodes);
    }

    private void OnValidate()
    {
        EnsureValidData();
    }

    private void EnsureValidData()
    {
        if (mapNodeCount < 1)
            mapNodeCount = 1;

        ResizeArray(ref nodeKeys, mapNodeCount);
        ResizeArray(ref spawnPositions, mapNodeCount);
        EnsureSpawnPositions();
    }

    private static void ResizeArray<T>(ref T[] array, int size)
    {
        if (array == null)
        {
            array = new T[size];
            return;
        }

        if (array.Length == size)
            return;

        Array.Resize(ref array, size);
    }

    private void EnsureSpawnPositions()
    {
        HashSet<Vector2Int> used = new HashSet<Vector2Int>();

        for (int i = 0; i < spawnPositions.Length; i++)
        {
            Vector2Int position = spawnPositions[i];

            if (position == Vector2Int.zero || used.Contains(position))
                position = FindAvailablePosition(used, i);

            spawnPositions[i] = position;
            used.Add(position);
        }
    }

    private static Vector2Int FindAvailablePosition(HashSet<Vector2Int> used, int index)
    {
        Vector2Int candidate = new Vector2Int(index + 1, 0);
        int guard = 0;

        while ((candidate == Vector2Int.zero || used.Contains(candidate)) && guard < 10000)
        {
            candidate.x++;
            guard++;
        }

        return candidate;
    }

    private static void Shuffle<T>(T[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = array[i];
            array[i] = array[j];
            array[j] = temp;
        }
    }
}
