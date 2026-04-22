using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A serializable region definition composed of FootprintBoxes and individual cells,
/// together with a cell size and local origin. Mirrors the shape of OccupancyZone /
/// SurfaceZone but without any layer or surface semantics ¡ª purely for visualization.
/// </summary>
[System.Serializable]
public struct BoxVisualizeRegion
{
    [Tooltip("Rectangular box regions that form the visual area.")]
    public FootprintBox[] boxes;

    [Tooltip("Additional individual cells to include.")]
    public Vector3Int[] cells;

    [Tooltip("Size of one cell in local units.")]
    public Vector3 cellSize;

    [Tooltip("Local-space offset of the grid origin relative to the object pivot.")]
    public Vector3 gridOriginLocal;

    /// <summary>
    /// Collects and deduplicates all cells from boxes and individual cell entries.
    /// </summary>
    public Vector3Int[] GatherAllCells()
    {
        HashSet<Vector3Int> set = new HashSet<Vector3Int>();
        List<Vector3Int> tmp = new List<Vector3Int>();

        if (boxes != null)
        {
            for (int b = 0; b < boxes.Length; b++)
            {
                tmp.Clear();
                boxes[b].GenerateCells(tmp);
                for (int i = 0; i < tmp.Count; i++)
                    set.Add(tmp[i]);
            }
        }

        if (cells != null)
        {
            for (int i = 0; i < cells.Length; i++)
                set.Add(cells[i]);
        }

        Vector3Int[] result = new Vector3Int[set.Count];
        set.CopyTo(result);
        return result;
    }
}

/// <summary>
/// Visualizes a grid region by spawning one LineRenderer per cell drawing a cube wireframe.
/// Can source grid data from an EnemyGridBehaviour (runtime grid) or a standalone
/// BoxVisualizeRegion defined in the Inspector when no EnemyGridBehaviour is available.
/// </summary>
public class EnemyGridVisual : MonoBehaviour
{
    [Header("References (optional ¡ª leave null to use standalone region below)")]
    [SerializeField] private EnemyGridBehaviour gridBehaviour;

    [Header("Standalone Region (used when gridBehaviour is null)")]
    [SerializeField] private BoxVisualizeRegion standaloneRegion;

    [Tooltip("Parent transform for all spawned LineRenderer objects.")]
    [SerializeField] private Transform lineRendererContainer;

    [Header("Outline Settings")]
    [SerializeField] private Material outlineMaterial;
    [SerializeField] private float lineWidth = 0.02f;
    [SerializeField] private Color outlineColor = Color.cyan;
    [SerializeField] private bool showOnStart = true;

    // Runtime resolved data
    private Vector3Int[] resolvedCells;
    private Vector3 resolvedCellSize;
    private Vector3 resolvedOrigin;

    private List<LineRenderer> cellLineRenderers = new List<LineRenderer>();
    private bool outlineVisible;

    // Unit cube wireframe ¡ª every edge drawn exactly once
    private static readonly Vector3[] s_cubeLoop = new Vector3[]
    {
        new Vector3(0, 0, 0),
        new Vector3(1, 0, 0),
        new Vector3(1, 0, 1),
        new Vector3(0, 0, 1),
        new Vector3(0, 0, 0),
        new Vector3(0, 1, 0),
        new Vector3(1, 1, 0),
        new Vector3(1, 0, 0),
        new Vector3(1, 1, 0),
        new Vector3(1, 1, 1),
        new Vector3(1, 0, 1),
        new Vector3(1, 1, 1),
        new Vector3(0, 1, 1),
        new Vector3(0, 0, 1),
        new Vector3(0, 1, 1),
        new Vector3(0, 1, 0),
    };

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public bool IsOutlineVisible => outlineVisible;

    public void ShowOutline()
    {
        if (outlineVisible) return;
        outlineVisible = true;
        RebuildOutline();
        SetLineRenderersEnabled(true);
    }

    public void HideOutline()
    {
        outlineVisible = false;
        SetLineRenderersEnabled(false);
    }

    public void ToggleOutline()
    {
        if (outlineVisible) HideOutline();
        else ShowOutline();
    }

    public void RebuildOutline()
    {
        ClearLineRenderers();
        BuildCellWireframes();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Awake()
    {
        if (gridBehaviour == null)
            gridBehaviour = GetComponent<EnemyGridBehaviour>();
    }

    private void Start()
    {
        ResolveSourceData();

        if (showOnStart)
            ShowOutline();
    }

    private void OnEnable()
    {
        if (gridBehaviour != null)
            gridBehaviour.OnGridChanged += OnGridChanged;
    }

    private void OnDisable()
    {
        if (gridBehaviour != null)
            gridBehaviour.OnGridChanged -= OnGridChanged;
    }

    private void OnGridChanged()
    {
        // Re-resolve in case the grid shape changed
        ResolveSourceData();
        if (outlineVisible)
            RebuildOutline();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Data Resolution ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void ResolveSourceData()
    {
        if (gridBehaviour != null)
        {
            // Source from EnemyGridBehaviour
            IReadOnlyList<Vector3Int> cells = gridBehaviour.AllValidCells;
            if (cells != null)
            {
                resolvedCells = new Vector3Int[cells.Count];
                for (int i = 0; i < cells.Count; i++)
                    resolvedCells[i] = cells[i];
            }
            else
            {
                resolvedCells = new Vector3Int[0];
            }

            resolvedCellSize = gridBehaviour.CellSize;
            resolvedOrigin = gridBehaviour.GridOriginLocal;
        }
        else
        {
            // Source from standalone BoxVisualizeRegion
            resolvedCells = standaloneRegion.GatherAllCells();
            resolvedCellSize = standaloneRegion.cellSize;
            resolvedOrigin = standaloneRegion.gridOriginLocal;

            if (resolvedCells.Length == 0)
                Debug.LogWarning($"[EnemyGridVisual] '{name}': no gridBehaviour and standaloneRegion has no cells.", this);
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Build ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void BuildCellWireframes()
    {
        if (resolvedCells == null || resolvedCells.Length == 0) return;

        Transform parent = lineRendererContainer != null ? lineRendererContainer : transform;
        Vector3 cs = resolvedCellSize;
        Vector3 origin = resolvedOrigin;

        for (int i = 0; i < resolvedCells.Length; i++)
        {
            Vector3Int cell = resolvedCells[i];
            Vector3 cellCorner = origin + new Vector3(cell.x * cs.x, cell.y * cs.y, cell.z * cs.z);

            GameObject lrObj = new GameObject($"[GridLine_{cell.x}_{cell.y}_{cell.z}]");
            lrObj.transform.SetParent(parent, false);
            lrObj.transform.localPosition = Vector3.zero;
            lrObj.transform.localRotation = Quaternion.identity;
            lrObj.transform.localScale = Vector3.one;

            LineRenderer lr = lrObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startColor = outlineColor;
            lr.endColor = outlineColor;

            if (outlineMaterial != null)
                lr.material = outlineMaterial;

            lr.positionCount = s_cubeLoop.Length;
            Vector3[] positions = new Vector3[s_cubeLoop.Length];
            for (int p = 0; p < s_cubeLoop.Length; p++)
                positions[p] = cellCorner + Vector3.Scale(s_cubeLoop[p], cs);

            lr.SetPositions(positions);
            cellLineRenderers.Add(lr);
        }
    }

    private void ClearLineRenderers()
    {
        for (int i = 0; i < cellLineRenderers.Count; i++)
        {
            if (cellLineRenderers[i] != null)
                Destroy(cellLineRenderers[i].gameObject);
        }
        cellLineRenderers.Clear();
    }

    private void SetLineRenderersEnabled(bool enabled)
    {
        for (int i = 0; i < cellLineRenderers.Count; i++)
        {
            if (cellLineRenderers[i] != null)
                cellLineRenderers[i].enabled = enabled;
        }
    }
}

