using UnityEngine;

/// <summary>
/// Manages the translucent preview object that follows the cursor during placement.
/// Changes material color to indicate valid (green) or invalid (red) placement.
/// </summary>
public class BuildPreviewController : MonoBehaviour
{
    [Header("Preview Materials")]
    [SerializeField] private Material validPreviewMaterial;    // semi-transparent green
    [SerializeField] private Material invalidPreviewMaterial;  // semi-transparent red

    private GameObject currentPreview;
    private Renderer[] previewRenderers;
    private bool isValid;

    /// <summary>
    /// Instantiate and show a preview object for the given property.
    /// </summary>
    public void ShowPreview(BuildableProperty property, int rotationStep)
    {
        HidePreview();

        GameObject prefab = property.previewPrefab != null
            ? property.previewPrefab
            : property.prefab;

        currentPreview = Instantiate(prefab);
        currentPreview.name = "[BuildPreview]";

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
}