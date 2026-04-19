using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visualizes the enemy grid outline by spawning one <see cref="LineRenderer"/> per valid cell,
/// each drawing a cube wireframe. All LineRenderers are parented under a configurable container object
/// which should be a child of the enemy, so they automatically follow movement and rotation.
/// </summary>
[RequireComponent(typeof(EnemyGridBehaviour))]
public class EnemyGridVisual : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyGridBehaviour gridBehaviour;

    [Tooltip("Parent transform for all spawned LineRenderer objects.\n" +
             "Should be a child of the enemy so wireframes follow movement/rotation.")]
    [SerializeField] private Transform lineRendererContainer;

    [Header("Outline Settings")]
    [Tooltip("Material for the wireframe lines.")]
    [SerializeField] private Material outlineMaterial;

    [Tooltip("Width of the wireframe lines.")]
    [SerializeField] private float lineWidth = 0.02f;

    [Tooltip("Color of the wireframe lines.")]
    [SerializeField] private Color outlineColor = Color.cyan;

    [Tooltip("Show outline on start.")]
    [SerializeField] private bool showOnStart = true;

    // Runtime
    private List<LineRenderer> cellLineRenderers = new List<LineRenderer>();
    private bool outlineVisible;

    // A single cube wireframe loop visits all 8 vertices via 16 points (Hamiltonian-ish path)
    // so every edge of the cube is drawn exactly once.
    private static readonly Vector3[] s_cubeLoop = new Vector3[]
    {
        // Bottom face loop
        new Vector3(0, 0, 0),
        new Vector3(1, 0, 0),
        new Vector3(1, 0, 1),
        new Vector3(0, 0, 1),
        new Vector3(0, 0, 0),
        // Up to top face
        new Vector3(0, 1, 0),
        // Top face loop
        new Vector3(1, 1, 0),
        new Vector3(1, 0, 0), // down pillar
        new Vector3(1, 1, 0), // back up
        new Vector3(1, 1, 1),
        new Vector3(1, 0, 1), // down pillar
        new Vector3(1, 1, 1), // back up
        new Vector3(0, 1, 1),
        new Vector3(0, 0, 1), // down pillar
        new Vector3(0, 1, 1), // back up
        new Vector3(0, 1, 0), // close top face
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
        if (outlineVisible)
            RebuildOutline();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Internal ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void BuildCellWireframes()
    {
        if (gridBehaviour == null || gridBehaviour.Grid == null) return;

        IReadOnlyList<Vector3Int> cells = gridBehaviour.AllValidCells;
        if (cells == null || cells.Count == 0) return;

        Transform parent = lineRendererContainer != null ? lineRendererContainer : transform;
        Vector3 cs = gridBehaviour.CellSize;
        Vector3 origin = gridBehaviour.GridOriginLocal;

        for (int i = 0; i < cells.Count; i++)
        {
            Vector3Int cell = cells[i];

            // Local-space corner of this cell
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

            // Build positions: scale the unit-cube loop by cell size and offset by cell corner
            lr.positionCount = s_cubeLoop.Length;
            Vector3[] positions = new Vector3[s_cubeLoop.Length];
            for (int p = 0; p < s_cubeLoop.Length; p++)
            {
                positions[p] = cellCorner + Vector3.Scale(s_cubeLoop[p], cs);
            }
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
