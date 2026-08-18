using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;
using UnityEngine.Serialization;

public enum Key_MapDataPP
{
    None = 0,
    TestMap = 1,
    SynthesizeMap = 2,
    TutorialMap = 3,
    MainMap = 4,
}

[CreateAssetMenu(fileName = "MapDataPP_", menuName = "AllProperties/MapDataProperty")]
public class MapDataProperty : EnumStringKeyedProperty<Key_MapDataPP>
{
    [Header("Map")]
    [SerializeField, Min(1)] private int mapNodeCount = 1;
    [Tooltip("When enabled, nodeKeys[i] is always placed at mapPositions[i]. Disable only for procedural maps that intentionally randomize node placement.")]
    [SerializeField] private bool isStableMap;
    [SerializeField] private Key_MapNodePP[] nodeKeys = new Key_MapNodePP[1];
    [Tooltip("Authored local coordinates inside the unscaled MapContent RectTransform. (0, 0) is the content centre.")]
    [SerializeField, FormerlySerializedAs("spawnPositions")]
    private Vector2Int[] mapPositions = new[] { new Vector2Int(1, 0) };

    public int MapNodeCount => mapNodeCount;
    public bool IsStableMap => isStableMap;
    public IReadOnlyList<Key_MapNodePP> NodeKeys => nodeKeys;
    public IReadOnlyList<Vector2Int> MapPositions => mapPositions;

    public MapDataRuntime CreateRuntimeMapData(MapNodeDatabase nodeDatabase)
    {
        EnsureValidData();

        Key_MapNodePP[] resolvedKeys = new Key_MapNodePP[nodeKeys.Length];
        Array.Copy(nodeKeys, resolvedKeys, nodeKeys.Length);

        if (!isStableMap)
            Shuffle(resolvedKeys);

        List<MapNodeRuntime> runtimeNodes = new List<MapNodeRuntime>(mapNodeCount);
        for (int i = 0; i < mapNodeCount; i++)
        {
            Key_MapNodePP nodeKey = resolvedKeys[i];
            MapNodeProperty property = null;

            if (nodeDatabase != null && nodeKey != Key_MapNodePP.None)
                property = nodeDatabase.GetByEnum(nodeKey);

            runtimeNodes.Add(new MapNodeRuntime(i, nodeKey, property, mapPositions[i]));
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
        ResizeArray(ref mapPositions, mapNodeCount);
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
