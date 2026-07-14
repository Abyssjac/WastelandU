using System.Collections.Generic;
using UnityEngine;

public class FlightTargetIslandController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private FlightVisualObjectPool objectPool;
    [SerializeField] private Transform islandRoot;
    [SerializeField] private IslandBehaviour greyboxIslandPrefab;

    [Header("Target Island")]
    [SerializeField, Min(1f)] private float islandSpawnDistance = 200f;
    [SerializeField, Min(0f)] private float arrivalOffset = 30f;
    [SerializeField, Min(1f)] private float retiredIslandRecycleRadius = 360f;
    [SerializeField] private bool allowIslandYawRotation = true;

    private readonly List<IslandBehaviour> retiredIslands = new List<IslandBehaviour>();
    private FlightVisualSegmentContext currentSegment;
    private IslandBehaviour currentIsland;
    private bool currentIslandArrived;

    private Transform IslandRoot => islandRoot != null ? islandRoot : transform;

    public void BeginSegment(FlightVisualSegmentContext segmentContext)
    {
        RetireCurrentIsland();
        currentSegment = segmentContext;
        currentIslandArrived = false;
    }

    public void Tick(FlightVisualFrameContext frameContext)
    {
        TickRetiredIslands(frameContext);

        if (currentSegment == null || currentIslandArrived)
            return;

        if (currentIsland == null)
            TrySpawnCurrentIsland(frameContext);
        else
            currentIsland.Move(frameContext.FlowDirection * frameContext.ScrollDelta);
    }

    public void OnArrived(FlightVisualFrameContext frameContext)
    {
        if (currentSegment == null)
            return;

        if (currentIsland == null)
            TrySpawnCurrentIsland(frameContext);

        if (currentIsland == null)
            return;

        Vector3 finalAnchorPosition = frameContext.ShipAnchorPosition + frameContext.Forward * arrivalOffset;
        currentIsland.SetApproachAnchorPosition(finalAnchorPosition);
        currentIslandArrived = true;
    }

    public void ReleaseAll()
    {
        if (currentIsland != null)
            ReleaseIsland(currentIsland);

        currentIsland = null;
        currentIslandArrived = false;
        currentSegment = null;

        for (int i = retiredIslands.Count - 1; i >= 0; i--)
            ReleaseIsland(retiredIslands[i]);

        retiredIslands.Clear();
    }

    private void TrySpawnCurrentIsland(FlightVisualFrameContext frameContext)
    {
        if (greyboxIslandPrefab == null || objectPool == null)
            return;

        float islandDistance = arrivalOffset + frameContext.RemainingVisualDistance;
        if (islandDistance > islandSpawnDistance)
            return;

        currentIsland = objectPool.Acquire(greyboxIslandPrefab, IslandRoot);
        if (currentIsland == null)
            return;

        currentIsland.PrepareForSpawn();
        if (allowIslandYawRotation)
            currentIsland.AlignApproachOutward(-frameContext.Forward);

        Vector3 anchorPosition = frameContext.ShipAnchorPosition + frameContext.Forward * islandDistance;
        currentIsland.SetApproachAnchorPosition(anchorPosition);
    }

    private void RetireCurrentIsland()
    {
        if (currentIsland == null)
            return;

        retiredIslands.Add(currentIsland);
        currentIsland = null;
        currentIslandArrived = false;
    }

    private void TickRetiredIslands(FlightVisualFrameContext frameContext)
    {
        for (int i = retiredIslands.Count - 1; i >= 0; i--)
        {
            IslandBehaviour island = retiredIslands[i];
            if (island == null)
            {
                retiredIslands.RemoveAt(i);
                continue;
            }

            island.Move(frameContext.FlowDirection * frameContext.ScrollDelta);
            if (!island.IsBeyondRecycleRadius(frameContext.ShipAnchorPosition, retiredIslandRecycleRadius))
                continue;

            retiredIslands.RemoveAt(i);
            ReleaseIsland(island);
        }
    }

    private void ReleaseIsland(IslandBehaviour island)
    {
        if (island == null)
            return;

        island.ResetForPool();
        objectPool?.Release(island);
    }
}
