using UnityEngine;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class GridGizmoDrawer : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private bool drawGrid = true;
    [SerializeField] private bool drawOnlyWhenSelected = false;

    [Header("Cell Size")]
    [SerializeField] private float cellSize = 1f;

    [Header("Grid Cells ¡ª Boxes (additive)")]
    [Tooltip("Each box defines a rectangular region of cells to draw.\n" +
             "e.g. cornerA=(-2,0,-2) cornerB=(2,0,2) ¡ú 5¡Á1¡Á5 grid centered on origin.")]
    [SerializeField] private FootprintBox[] boxes = new FootprintBox[]
    {
        new FootprintBox(new Vector3Int(-2, 0, -2), new Vector3Int(2, 0, 2))
    };

    [Header("Grid Cells ¡ª Manual (additive)")]
    [Tooltip("Individual cell offsets added on top of boxes.")]
    [SerializeField] private Vector3Int[] manualCells = new Vector3Int[0];

    [Header("Origin / Center")]
    [SerializeField] private bool useTransformAsOrigin = true;
    [SerializeField] private Vector3 manualOriginOffset = Vector3.zero;

    [Header("Center Cell")]
    [Tooltip("Which cell coordinate is considered the 'center' for the marker.\n" +
             "Default (0,0,0) means origin cell.")]
    [SerializeField] private Vector3Int centerCell = Vector3Int.zero;

    [Header("Colors")]
    [SerializeField] private Color cellWireColor = new Color(0f, 1f, 1f, 0.6f);
    [SerializeField] private Color cellFillColor = new Color(0f, 1f, 1f, 0.08f);
    [SerializeField] private Color originColor = Color.red;
    [SerializeField] private Color centerCellColor = Color.yellow;

    [Header("Markers")]
    [SerializeField] private bool drawOriginMarker = true;
    [SerializeField] private float originSphereRadius = 0.08f;
    [SerializeField] private bool drawCenterCellMarker = true;
    [SerializeField] private float centerCellSphereRadius = 0.06f;

    // ©¤©¤©¤ Cache ©¤©¤©¤
    private HashSet<Vector3Int> resolvedCells;
    private bool dirty = true;

    private void OnValidate() { dirty = true; }

    private void OnDrawGizmos()
    {
        if (drawOnlyWhenSelected) return;
        DrawGridGizmos();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawOnlyWhenSelected) return;
        DrawGridGizmos();
    }

    private void RebuildCells()
    {
        if (!dirty && resolvedCells != null) return;

        resolvedCells = new HashSet<Vector3Int>();
        List<Vector3Int> tmp = new List<Vector3Int>();

        if (boxes != null)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                tmp.Clear();
                boxes[i].GenerateCells(tmp);
                for (int j = 0; j < tmp.Count; j++)
                    resolvedCells.Add(tmp[j]);
            }
        }

        if (manualCells != null)
        {
            for (int i = 0; i < manualCells.Length; i++)
                resolvedCells.Add(manualCells[i]);
        }

        dirty = false;
    }

    private void DrawGridGizmos()
    {
        if (!drawGrid) return;
        if (cellSize <= 0f) return;

        RebuildCells();
        if (resolvedCells == null || resolvedCells.Count == 0) return;

        Vector3 origin = useTransformAsOrigin
            ? transform.position + manualOriginOffset
            : manualOriginOffset;

        Vector3 cubeSize = Vector3.one * cellSize;
        float half = cellSize * 0.5f;

        // Draw each cell as a wireframe + subtle fill
        foreach (Vector3Int cell in resolvedCells)
        {
            Vector3 worldCenter = origin + new Vector3(
                cell.x * cellSize + half,
                cell.y * cellSize + half,
                cell.z * cellSize + half
            );

            Gizmos.color = cellFillColor;
            Gizmos.DrawCube(worldCenter, cubeSize);
            Gizmos.color = cellWireColor;
            Gizmos.DrawWireCube(worldCenter, cubeSize);
        }

        // Origin marker (world origin of the grid)
        if (drawOriginMarker)
        {
            Gizmos.color = originColor;
            Gizmos.DrawSphere(origin, originSphereRadius * cellSize);
        }

        // Center cell marker
        if (drawCenterCellMarker)
        {
            Vector3 centerWorld = origin + new Vector3(
                centerCell.x * cellSize + half,
                centerCell.y * cellSize + half,
                centerCell.z * cellSize + half
            );
            Gizmos.color = centerCellColor;
            Gizmos.DrawSphere(centerWorld, centerCellSphereRadius * cellSize);
        }
    }
}