using System.Collections.Generic;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the preview for the build system.
/// For single-buildable placement: spawns the actual model (no material change)
/// plus a PreviewBox made of unit cubes whose <see cref="BaseVisualController"/>
/// is switched between valid (green) and invalid (red) materials.
/// For blueprint placement: spawns all entry models + a merged PreviewBox.
/// </summary>
public class BuildPreviewController : MonoBehaviour
{
    [Header("Preview Materials")]
    [SerializeField] private Material validPreviewMaterial;    // semi-transparent green
    [SerializeField] private Material invalidPreviewMaterial;  // semi-transparent red

    [Header("Preview Unit")]
    [Tooltip("A simple cube prefab (no Collider) with a BaseVisualController component.\n" +
             "Used to build the PreviewBox outline.")]
    [SerializeField] private GameObject previewUnitPrefab;

    [Tooltip("Slight scale multiplier for preview units to avoid z-fighting with adjacent cells.")]
    [SerializeField] private float unitScaleFactor = 0.95f;

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    // Single-Buildable Preview
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    private GameObject currentPreview;                       // root "[BuildPreview]"
    private GameObject modelChild;                           // the actual prefab instance (visual only)
    private List<BaseVisualController> previewUnitVisuals;   // one per footprint cell
    private BuildableProperty cachedProperty;                // for recalculating footprint on rotation
    private int cachedRotationStep;
    private Vector3 cachedCellSize;

    /// <summary>
    /// Show a preview for a single buildable: model + PreviewBox unit cubes.
    /// </summary>
    public void ShowPreview(BuildableProperty property, int rotationStep, Vector3 cellSize)
    {
        HidePreview();

        cachedProperty = property;
        cachedRotationStep = rotationStep;
        cachedCellSize = cellSize;

        // Root
        currentPreview = new GameObject("[BuildPreview]");

        // Model child ¡ª visual only, no material change
        GameObject prefab = property.previewPrefab != null ? property.previewPrefab : property.prefab;
        modelChild = Instantiate(prefab, currentPreview.transform);
        modelChild.name = "[Model]";
        modelChild.transform.localScale = cellSize;
        float yaw = property.GetRotationDegrees(rotationStep);
        modelChild.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        DisableColliders(modelChild);

        // PreviewBox ¡ª unit cubes for footprint outline
        previewUnitVisuals = new List<BaseVisualController>();
        BuildPreviewUnits(property, rotationStep, cellSize, currentPreview.transform, previewUnitVisuals);

        SetPreviewValid(true);
    }

    /// <summary>
    /// Destroy the current single-buildable preview.
    /// </summary>
    public void HidePreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
        }
        modelChild = null;
        previewUnitVisuals = null;
        cachedProperty = null;
    }

    /// <summary>
    /// Move the preview to a snapped world position and update validity color.
    /// </summary>
    public void UpdatePreviewPosition(Vector3 snappedWorld, Vector3Int anchor, Vector3Int[] footprintOffsets, bool canPlace)
    {
        if (currentPreview == null) return;
        currentPreview.transform.position = snappedWorld;
        SetPreviewValid(canPlace);
    }

    /// <summary>
    /// Rotate the preview: rebuild unit cube positions + rotate model.
    /// </summary>
    public void UpdateRotation(int rotationStep)
    {
        if (currentPreview == null || cachedProperty == null) return;

        cachedRotationStep = rotationStep;

        // Rotate model
        float yaw = rotationStep * 90f;
        if (modelChild != null)
            modelChild.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        // Rebuild preview units
        RebuildPreviewUnits(cachedProperty, rotationStep, cachedCellSize, currentPreview.transform, previewUnitVisuals);
    }

    /// <summary>
    /// Set all preview unit cubes to valid (green) or invalid (red) material.
    /// </summary>
    public void SetPreviewValid(bool valid)
    {
        if (previewUnitVisuals == null) return;
        Material mat = valid ? validPreviewMaterial : invalidPreviewMaterial;
        for (int i = 0; i < previewUnitVisuals.Count; i++)
        {
            previewUnitVisuals[i].SetMaterialAll(mat);
        }
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    // Blueprint Multi-Preview
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    private GameObject blueprintRoot;
    private List<GameObject> blueprintModelChildren;
    private List<BaseVisualController> blueprintUnitVisuals;

    /// <summary>
    /// Show preview for a blueprint: all entry models + merged PreviewBox.
    /// </summary>
    /// <param name="prefabs">One prefab per entry.</param>
    /// <param name="localOffsets">World-space offset per entry (already rotated).</param>
    /// <param name="localRotations">Rotation per entry.</param>
    /// <param name="cellSize">Grid cell size.</param>
    /// <param name="footprintCells">All deduplicated footprint cell offsets in world-space (already rotated),
    /// relative to the blueprint anchor, for unit cube generation.</param>
    public void ShowBlueprintPreview(GameObject[] prefabs, Vector3[] localOffsets, Quaternion[] localRotations,
                                     Vector3 cellSize, Vector3Int[] footprintCells)
    {
        HidePreview();
        HideBlueprintPreview();

        blueprintRoot = new GameObject("[BlueprintPreviewRoot]");
        blueprintModelChildren = new List<GameObject>(prefabs.Length);
        blueprintUnitVisuals = new List<BaseVisualController>();

        // Spawn model children
        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null) continue;

            GameObject go = Instantiate(prefabs[i], blueprintRoot.transform);
            go.name = $"[BPModel_{i}]";
            go.transform.localScale = cellSize;
            go.transform.localPosition = localOffsets[i];
            go.transform.localRotation = localRotations[i];

            DisableColliders(go);
            blueprintModelChildren.Add(go);
        }

        // Spawn preview unit cubes for merged footprint
        SpawnUnitsFromCells(footprintCells, cellSize, blueprintRoot.transform, blueprintUnitVisuals);

        SetBlueprintPreviewValid(true);
    }

    /// <summary>
    /// Move the blueprint preview root to follow the cursor.
    /// </summary>
    public void UpdateBlueprintPreviewPosition(Vector3 snappedWorld, bool canPlace)
    {
        if (blueprintRoot == null) return;
        blueprintRoot.transform.position = snappedWorld;
        SetBlueprintPreviewValid(canPlace);
    }

    /// <summary>
    /// Rebuild model transforms and preview units when the blueprint rotation changes.
    /// </summary>
    public void UpdateBlueprintPreview(Vector3[] newModelOffsets, Quaternion[] newModelRotations, Vector3Int[] newFootprintCells, Vector3 cellSize)
    {
        if (blueprintRoot == null) return;

        // Update model positions
        if (blueprintModelChildren != null)
        {
            for (int i = 0; i < blueprintModelChildren.Count && i < newModelOffsets.Length; i++)
            {
                blueprintModelChildren[i].transform.localPosition = newModelOffsets[i];
                blueprintModelChildren[i].transform.localRotation = newModelRotations[i];
            }
        }

        // Rebuild unit cubes
        DestroyUnits(blueprintUnitVisuals, blueprintRoot.transform);
        blueprintUnitVisuals = new List<BaseVisualController>();
        SpawnUnitsFromCells(newFootprintCells, cellSize, blueprintRoot.transform, blueprintUnitVisuals);

        SetBlueprintPreviewValid(true);
    }

    /// <summary>
    /// Destroy all blueprint preview objects.
    /// </summary>
    public void HideBlueprintPreview()
    {
        if (blueprintRoot != null)
        {
            Destroy(blueprintRoot);
            blueprintRoot = null;
        }
        blueprintModelChildren = null;
        blueprintUnitVisuals = null;
    }

    /// <summary>
    /// Set all blueprint preview unit cubes to valid/invalid material.
    /// </summary>
    public void SetBlueprintPreviewValid(bool valid)
    {
        if (blueprintUnitVisuals == null) return;
        Material mat = valid ? validPreviewMaterial : invalidPreviewMaterial;
        for (int i = 0; i < blueprintUnitVisuals.Count; i++)
        {
            blueprintUnitVisuals[i].SetMaterialAll(mat);
        }
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    // Internal Helpers
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    /// <summary>
    /// Build preview unit cubes from a BuildableProperty's footprint.
    /// </summary>
    private void BuildPreviewUnits(BuildableProperty property, int rotationStep, Vector3 cellSize,
                                   Transform parent, List<BaseVisualController> outVisuals)
    {
        Vector3Int[] footprint = property.GetRotatedFootprint(rotationStep);
        SpawnUnitsFromCells(footprint, cellSize, parent, outVisuals);
    }

    /// <summary>
    /// Destroy existing units and rebuild from a new footprint.
    /// </summary>
    private void RebuildPreviewUnits(BuildableProperty property, int rotationStep, Vector3 cellSize,
                                     Transform parent, List<BaseVisualController> visuals)
    {
        DestroyUnits(visuals, parent);
        visuals.Clear();
        BuildPreviewUnits(property, rotationStep, cellSize, parent, visuals);
    }

    /// <summary>
    /// Spawn one unit cube per cell offset. Each cube gets a <see cref="BaseVisualController"/>.
    /// </summary>
    private void SpawnUnitsFromCells(Vector3Int[] cells, Vector3 cellSize, Transform parent,
                                     List<BaseVisualController> outVisuals)
    {
        if (previewUnitPrefab == null)
        {
            Debug.LogWarning("[BuildPreviewController] previewUnitPrefab is not assigned.");
            return;
        }

        Vector3 unitScale = new Vector3(
            cellSize.x * unitScaleFactor,
            cellSize.y * unitScaleFactor,
            cellSize.z * unitScaleFactor
        );

        for (int i = 0; i < cells.Length; i++)
        {
            // Root is already at the anchor cell center (CellToWorldCenter),
            // so unit local positions are relative offsets only.
            Vector3 localPos = new Vector3(
                cells[i].x * cellSize.x,
                cells[i].y * cellSize.y,
                cells[i].z * cellSize.z
            );

            GameObject unit = Instantiate(previewUnitPrefab, parent);
            unit.name = $"[PreviewUnit_{i}]";
            unit.transform.localPosition = localPos;
            unit.transform.localRotation = Quaternion.identity;
            unit.transform.localScale = unitScale;

            var visual = unit.GetComponent<BaseVisualController>();
            if (visual == null)
            {
                Debug.LogWarning($"[BuildPreviewController] previewUnitPrefab is missing BaseVisualController on '{previewUnitPrefab.name}'.");
                visual = unit.AddComponent<BaseVisualController>();
            }

            outVisuals.Add(visual);
        }
    }

    /// <summary>
    /// Destroy all unit cube GameObjects managed by the given visual list.
    /// </summary>
    private void DestroyUnits(List<BaseVisualController> visuals, Transform parent)
    {
        if (visuals == null) return;
        for (int i = 0; i < visuals.Count; i++)
        {
            if (visuals[i] != null && visuals[i].gameObject != null)
                Destroy(visuals[i].gameObject);
        }
    }

    /// <summary>
    /// Disable all colliders on a GameObject hierarchy.
    /// </summary>
    private static void DisableColliders(GameObject go)
    {
        Collider[] cols = go.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
    }
}