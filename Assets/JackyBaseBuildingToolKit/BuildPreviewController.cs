using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the translucent preview object that follows the cursor during placement.
/// Changes material color to indicate valid (green) or invalid (red) placement.
/// Supports both single-buildable preview and multi-object blueprint preview.
/// </summary>
public class BuildPreviewController : MonoBehaviour
{
    [Header("Preview Materials")]
    [SerializeField] private Material validPreviewMaterial;    // semi-transparent green
    [SerializeField] private Material invalidPreviewMaterial;  // semi-transparent red

    // ©¤©¤ Single preview ©¤©¤
    private GameObject currentPreview;
    private Renderer[] previewRenderers;
    private bool isValid;

    // ©¤©¤ Blueprint multi-preview ©¤©¤
    private GameObject blueprintRoot;                        // empty parent that moves with the cursor
    private List<GameObject> blueprintPreviews;              // one child GO per blueprint entry
    private Renderer[] blueprintRenderers;                   // cached renderers across all children
    private List<Vector3> blueprintLocalOffsets;             // local position of each child relative to root
    private List<Quaternion> blueprintLocalRotations;        // local rotation of each child

    /// <summary>
    /// Instantiate and show a preview object for the given property.
    /// </summary>
    public void ShowPreview(BuildableProperty property, int rotationStep, Vector3 cellSize)
    {
        HidePreview();

        GameObject prefab = property.previewPrefab != null
            ? property.previewPrefab
            : property.prefab;

        currentPreview = Instantiate(prefab);
        currentPreview.name = "[BuildPreview]";

        // Scale to match cell size
        currentPreview.transform.localScale = cellSize;

        // disable all colliders on preview so it doesn't interfere with raycasts
        Collider[] cols = currentPreview.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;

        previewRenderers = currentPreview.GetComponentsInChildren<Renderer>();

        float yaw = property.GetRotationDegrees(rotationStep);
        currentPreview.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        SetPreviewValid(true);
    }

    /// <summary>
    /// Destroy the current preview object.
    /// </summary>
    public void HidePreview()
    {
        if (currentPreview != null)
        {
            Destroy(currentPreview);
            currentPreview = null;
            previewRenderers = null;
        }
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
    /// Rotate the preview by rotation step (0-3).
    /// </summary>
    public void UpdateRotation(int rotationStep)
    {
        if (currentPreview == null) return;
        float yaw = rotationStep * 90f;
        currentPreview.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    /// <summary>
    /// Set the preview material to valid (green) or invalid (red).
    /// </summary>
    public void SetPreviewValid(bool valid)
    {
        if (previewRenderers == null) return;
        isValid = valid;
        Material mat = valid ? validPreviewMaterial : invalidPreviewMaterial;
        for (int i = 0; i < previewRenderers.Length; i++)
        {
            previewRenderers[i].sharedMaterial = mat;
        }
    }

    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T
    // Blueprint Multi-Preview
    // ¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T¨T

    /// <summary>
    /// Spawn preview objects for every entry in a blueprint.
    /// Each entry's prefab is instantiated as a child of an invisible root object.
    /// </summary>
    /// <param name="prefabs">One prefab (or previewPrefab) per blueprint entry.</param>
    /// <param name="localOffsets">World-space offset for each entry relative to the blueprint anchor (already rotated by initial rotation).</param>
    /// <param name="localRotations">Rotation for each entry.</param>
    /// <param name="cellSize">Cell size for scaling.</param>
    public void ShowBlueprintPreview(GameObject[] prefabs, Vector3[] localOffsets, Quaternion[] localRotations, Vector3 cellSize)
    {
        HidePreview();
        HideBlueprintPreview();

        blueprintRoot = new GameObject("[BlueprintPreviewRoot]");
        blueprintPreviews = new List<GameObject>(prefabs.Length);
        blueprintLocalOffsets = new List<Vector3>(localOffsets.Length);
        blueprintLocalRotations = new List<Quaternion>(localRotations.Length);

        List<Renderer> allRenderers = new List<Renderer>();

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null) continue;

            GameObject go = Instantiate(prefabs[i], blueprintRoot.transform);
            go.name = $"[BPPreview_{i}]";
            go.transform.localScale = cellSize;
            go.transform.localPosition = localOffsets[i];
            go.transform.localRotation = localRotations[i];

            // Disable colliders
            Collider[] cols = go.GetComponentsInChildren<Collider>();
            for (int c = 0; c < cols.Length; c++) cols[c].enabled = false;

            allRenderers.AddRange(go.GetComponentsInChildren<Renderer>());
            blueprintPreviews.Add(go);
            blueprintLocalOffsets.Add(localOffsets[i]);
            blueprintLocalRotations.Add(localRotations[i]);
        }

        blueprintRenderers = allRenderers.ToArray();
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
    /// Rebuild child local positions/rotations when the blueprint rotation changes.
    /// </summary>
    public void UpdateBlueprintChildTransforms(Vector3[] newLocalOffsets, Quaternion[] newLocalRotations)
    {
        if (blueprintPreviews == null) return;

        for (int i = 0; i < blueprintPreviews.Count && i < newLocalOffsets.Length; i++)
        {
            blueprintPreviews[i].transform.localPosition = newLocalOffsets[i];
            blueprintPreviews[i].transform.localRotation = newLocalRotations[i];
            blueprintLocalOffsets[i] = newLocalOffsets[i];
            blueprintLocalRotations[i] = newLocalRotations[i];
        }
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
        blueprintPreviews = null;
        blueprintRenderers = null;
        blueprintLocalOffsets = null;
        blueprintLocalRotations = null;
    }

    /// <summary>
    /// Set all blueprint preview renderers to valid/invalid material.
    /// </summary>
    public void SetBlueprintPreviewValid(bool valid)
    {
        if (blueprintRenderers == null) return;
        Material mat = valid ? validPreviewMaterial : invalidPreviewMaterial;
        for (int i = 0; i < blueprintRenderers.Length; i++)
        {
            blueprintRenderers[i].sharedMaterial = mat;
        }
    }
}