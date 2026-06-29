using System.Collections.Generic;

/// <summary>
/// Top-level container for one build save slot.
/// Stored inside the "build" section of GameSaveData.
/// </summary>
[System.Serializable]
public class BuildSaveData
{
    /// <summary>
    /// All placed buildables at the time of saving.
    /// Order is not significant for loading (ForcePlaceImmediate is used).
    /// </summary>
    public List<BuildableSaveEntry> entries = new List<BuildableSaveEntry>();

    /// <summary>
    /// Snapshot of BuildManager.instanceCounter at save time.
    /// Restored on load so subsequent placements do not produce IDs that
    /// collide with the restored instances.
    /// </summary>
    public int instanceCounterSnapshot;

}
