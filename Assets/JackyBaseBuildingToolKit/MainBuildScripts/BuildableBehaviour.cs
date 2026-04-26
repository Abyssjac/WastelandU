using UnityEngine;

/// <summary>
/// Attached to every spawned buildable GameObject at runtime.
/// Provides a direct link from the scene object back to its grid data,
/// allowing raycast-based selection without dictionary lookups.
/// </summary>
public class BuildableBehaviour : MonoBehaviour
{
    /// <summary>Unique instance ID matching <see cref="PlacedBuildableData.InstanceId"/>.</summary>
    public string InstanceId { get; private set; }

    /// <summary>Reference to the runtime placement data (anchor, rotation, hierarchy, etc.).</summary>
    public PlacedBuildableData Data { get; private set; }

    /// <summary>The property definition (SO) for this buildable.</summary>
    public BuildableProperty Property => Data?.Property;

    /// <summary>
    /// When true, this buildable has been detached from its EnemyGridBehaviour by the boss steal
    /// skill. The grid slot has already been freed; recycling should add to weapon container and
    /// destroy this GameObject directly without touching the grid.
    /// </summary>
    public bool IsDetached { get; private set; }

    /// <summary>
    /// Called by BuildManager immediately after Instantiate.
    /// </summary>
    public void Initialize(PlacedBuildableData data)
    {
        Data = data;
        InstanceId = data.InstanceId;
        IsDetached = false;
    }

    /// <summary>
    /// Mark this buildable as detached from the grid (called by DetachFromGrid).
    /// </summary>
    public void MarkDetached()
    {
        IsDetached = true;
    }
}
