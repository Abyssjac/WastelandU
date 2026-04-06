using JackyUtility;
using UnityEngine;

[CreateAssetMenu(fileName = "BuildablePP_", menuName = "AllProperties/ BuildableProperty")]
public class BuildableProperty : ScriptableObject, IEnumStringKeyedEntry<Key_BuildablePP>
{
    [Header("Keys")]
    [SerializeField] private Key_BuildablePP enumKey;
    [SerializeField] private string stringKey;

    public Key_BuildablePP EnumKey => enumKey;
    public string StringKey => stringKey;

    [Header("Visuals")]
    public GameObject prefab;
    public GameObject previewPrefab;

    [Header("Grid Footprint")]
    [Tooltip("Each entry is one occupied cell offset relative to the anchor (0,0,0). " +
             "e.g. a 2x1x3 table: (0,0,0),(1,0,0),(0,0,1),(1,0,1),(0,0,2),(1,0,2)")]
    public Vector3Int[] footprint = new Vector3Int[] { Vector3Int.zero };

    [Header("Placement Rules")]
    public bool canRotate = true;
    public bool canMove = true;
    [Tooltip("If true, this buildable must be placed on the ground (y=0 layer only).")]
    public bool groundOnly = true;

    [Header("UI Display")]
    public Sprite iconSprite;
    public string displayName;

    /// <summary>
    /// Returns the footprint cells rotated by 90бу steps around Y axis.
    /// rotationStep: 0=0бу, 1=90бу, 2=180бу, 3=270бу
    /// </summary>
    public Vector3Int[] GetRotatedFootprint(int rotationStep)
    {
        rotationStep = ((rotationStep % 4) + 4) % 4;
        if (rotationStep == 0)
            return footprint;

        Vector3Int[] rotated = new Vector3Int[footprint.Length];
        for (int i = 0; i < footprint.Length; i++)
        {
            rotated[i] = RotateCellY(footprint[i], rotationStep);
        }
        return rotated;
    }

    /// <summary>
    /// Returns the Y-axis rotation in degrees for a given rotation step.
    /// </summary>
    public float GetRotationDegrees(int rotationStep)
    {
        return ((rotationStep % 4 + 4) % 4) * 90f;
    }

    /// <summary>
    /// Rotate a single cell offset around Y axis by 90бу * steps.
    /// Y component is preserved (height unchanged).
    /// </summary>
    private static Vector3Int RotateCellY(Vector3Int cell, int steps)
    {
        int x = cell.x;
        int z = cell.z;

        for (int s = 0; s < steps; s++)
        {
            // 90бу clockwise around Y: (x, z) -> (z, -x)
            int newX = z;
            int newZ = -x;
            x = newX;
            z = newZ;
        }

        return new Vector3Int(x, cell.y, z);
    }
}

public enum Key_BuildablePP
{
    None = 0,

    // ---- Test ----
    Build_Cube_11 = 1,
    Build_Cube_12 = 2,
    Build_Cube_22 = 3,
}