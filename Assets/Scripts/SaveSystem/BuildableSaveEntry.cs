using UnityEngine;

/// <summary>
/// The minimal data needed to reconstruct one placed buildable from a save file.
/// instanceId   — preserved so surface-map OwnerInstanceId references survive a reload.
/// enumKey      — resolves back to BuildableProperty via BuildableDatabase.
/// anchorCell   — grid cell of the anchor.
/// rotationStep — 0-3 (each step = 90° around Y).
/// </summary>
[System.Serializable]
public struct BuildableSaveEntry
{
    public string instanceId;
    public Key_BuildablePP enumKey;
    public Vector3Int anchorCell;
    public int rotationStep;

    public BuildableSaveEntry(string instanceId, Key_BuildablePP enumKey, Vector3Int anchorCell, int rotationStep)
    {
        this.instanceId   = instanceId;
        this.enumKey      = enumKey;
        this.anchorCell   = anchorCell;
        this.rotationStep = rotationStep;
    }
}
