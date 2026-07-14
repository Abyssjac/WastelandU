using System;
using System.Collections.Generic;
using UnityEngine;

public class FlightAmbientPropController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlightVisualObjectPool objectPool;
    [SerializeField] private Transform spawnRoot;

    [Header("Cloud Prefabs")]
    [SerializeField] private List<FlightCloudPropBehaviour> cloudPrefabs = new List<FlightCloudPropBehaviour>();

    [Header("Spawn")]
    [SerializeField, Min(1)] private int maxActiveCloudCount = 18;
    [SerializeField, Min(0.01f)] private float spawnInterval = 0.35f;
    [SerializeField] private Vector2 forwardSpawnDistance = new Vector2(60f, 180f);
    [SerializeField, Min(0f)] private float lateralSpawnDistance = 90f;
    [SerializeField] private Vector2 verticalSpawnDistance = new Vector2(-20f, 60f);
    [SerializeField] private Vector2 uniformScaleRange = new Vector2(0.8f, 1.35f);
    [SerializeField, Min(1f)] private float recycleRadius = 260f;

    private readonly List<FlightAmbientPropBehaviour> activeProps = new List<FlightAmbientPropBehaviour>();
    private System.Random random = new System.Random();
    private float spawnTimer;

    private Transform SpawnRoot => spawnRoot != null ? spawnRoot : transform;

    public void BeginSegment(FlightVisualSegmentContext segmentContext)
    {
        random = new System.Random(segmentContext.RandomSeed);
        spawnTimer = 0f;
    }

    public void Tick(FlightVisualFrameContext frameContext)
    {
        TickActiveProps(frameContext);

        if (!frameContext.AllowsAmbientSpawning || frameContext.ScrollDelta <= 0f)
            return;

        spawnTimer += frameContext.DeltaTime;
        while (spawnTimer >= spawnInterval && activeProps.Count < maxActiveCloudCount)
        {
            spawnTimer -= spawnInterval;
            SpawnCloud(frameContext);
        }
    }

    public void ReleaseAll()
    {
        for (int i = activeProps.Count - 1; i >= 0; i--)
            Recycle(activeProps[i]);

        spawnTimer = 0f;
    }

    private void TickActiveProps(FlightVisualFrameContext frameContext)
    {
        for (int i = activeProps.Count - 1; i >= 0; i--)
        {
            FlightAmbientPropBehaviour prop = activeProps[i];
            if (prop == null)
            {
                activeProps.RemoveAt(i);
                continue;
            }

            prop.Simulate(frameContext);
            if (prop.IsBeyondRecycleRadius(frameContext.ShipAnchorPosition, recycleRadius))
                Recycle(prop);
        }
    }

    private void SpawnCloud(FlightVisualFrameContext frameContext)
    {
        FlightCloudPropBehaviour prefab = GetRandomCloudPrefab();
        if (prefab == null || objectPool == null)
            return;

        float forwardDistance = RandomRange(forwardSpawnDistance.x, forwardSpawnDistance.y);
        float lateralDistance = RandomRange(-lateralSpawnDistance, lateralSpawnDistance);
        float verticalDistance = RandomRange(verticalSpawnDistance.x, verticalSpawnDistance.y);
        Vector3 position = frameContext.ShipAnchorPosition
            + frameContext.Forward * forwardDistance
            + frameContext.Right * lateralDistance
            + Vector3.up * verticalDistance;

        float uniformScale = RandomRange(uniformScaleRange.x, uniformScaleRange.y);
        FlightAmbientSpawnData spawnData = new FlightAmbientSpawnData(
            position,
            Quaternion.Euler(0f, RandomRange(0f, 360f), 0f),
            Vector3.one * uniformScale,
            random.Next());

        FlightCloudPropBehaviour cloud = objectPool.Acquire(prefab, SpawnRoot);
        if (cloud == null)
            return;

        cloud.Spawn(spawnData);
        activeProps.Add(cloud);
    }

    private void Recycle(FlightAmbientPropBehaviour prop)
    {
        if (prop == null)
            return;

        activeProps.Remove(prop);
        prop.ResetForPool();
        objectPool?.Release(prop);
    }

    private FlightCloudPropBehaviour GetRandomCloudPrefab()
    {
        if (cloudPrefabs == null || cloudPrefabs.Count == 0)
            return null;

        int startIndex = random.Next(0, cloudPrefabs.Count);
        for (int offset = 0; offset < cloudPrefabs.Count; offset++)
        {
            FlightCloudPropBehaviour prefab = cloudPrefabs[(startIndex + offset) % cloudPrefabs.Count];
            if (prefab != null)
                return prefab;
        }

        return null;
    }

    private float RandomRange(float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
