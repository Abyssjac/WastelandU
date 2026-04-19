using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Preview controller for the weapon/shoot system.
/// Displays per-cell colored preview cubes on the enemy grid to show where
/// the current weapon's buildable would be placed.
/// Each cell is colored individually: valid (green), conflict (orange), invalid (red).
/// <para>
/// Preview cubes are parented to the hit enemy so they follow its movement and rotation.
/// When the target enemy changes, cubes are destroyed and recreated.
/// </para>
/// </summary>
public class PlayerShootPreviewController : MonoBehaviour
{
    [Header("Preview Materials")]
    [SerializeField] private Material validPreviewMaterial;
    [SerializeField] private Material invalidPreviewMaterial;
    [SerializeField] private Material conflictPreviewMaterial;

    [Header("Preview Unit")]
    [Tooltip("A simple cube prefab (no Collider) with a BaseVisualController component.")]
    [SerializeField] private GameObject previewUnitPrefab;

    [Tooltip("Slight scale multiplier for preview units to avoid z-fighting.")]
    [SerializeField] private float unitScaleFactor = 0.95f;

    // Runtime
    private GameObject previewRoot;
    private List<BaseVisualController> previewUnitVisuals = new List<BaseVisualController>();
    private EnemyGridBehaviour cachedEnemy;
    private BuildableProperty cachedProperty;
    private int cachedRotationStep;
    private Vector3Int cachedAnchorCell;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Update the preview for the current frame.
    /// Call this every frame with the latest hit result and weapon data.
    /// </summary>
    /// <param name="hasHit">Whether the weapon raycast hit an enemy.</param>
    /// <param name="hitResult">The hit result (only used when hasHit is true).</param>
    /// <param name="property">The buildable property to preview. Null hides the preview.</param>
    /// <param name="rotationStep">Current rotation step of the weapon buildable.</param>
    public void UpdatePreview(bool hasHit, WeaponHitResult hitResult, BuildableProperty property, int rotationStep)
    {
        if (!hasHit || property == null || hitResult.HitEnemyGridBehaviour == null)
        {
            HidePreview();
            return;
        }

        EnemyGridBehaviour enemy = hitResult.HitEnemyGridBehaviour;
        Vector3Int anchorCell = hitResult.HitCell;

        // Check if we need to rebuild the preview units (enemy, property, rotation, or anchor changed)
        bool needsRebuild = previewRoot == null
            || cachedEnemy != enemy
            || cachedProperty != property
            || cachedRotationStep != rotationStep
            || cachedAnchorCell != anchorCell;

        if (needsRebuild)
        {
            HidePreview();
            BuildPreview(enemy, property, anchorCell, rotationStep);
        }
    }

    /// <summary>
    /// Hide and destroy all preview objects.
    /// </summary>
    public void HidePreview()
    {
        if (previewRoot != null)
        {
            Destroy(previewRoot);
            previewRoot = null;
        }
        previewUnitVisuals.Clear();
        cachedEnemy = null;
        cachedProperty = null;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Internal ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void BuildPreview(EnemyGridBehaviour enemy, BuildableProperty property,
                              Vector3Int anchorCell, int rotationStep)
    {
        cachedEnemy = enemy;
        cachedProperty = property;
        cachedRotationStep = rotationStep;
        cachedAnchorCell = anchorCell;

        // Parent the preview root under the enemy so it follows transform
        previewRoot = new GameObject("[ShootPreview]");
        previewRoot.transform.SetParent(enemy.transform, false);
        previewRoot.transform.localPosition = Vector3.zero;
        previewRoot.transform.localRotation = Quaternion.identity;
        previewRoot.transform.localScale = Vector3.one;

        // Evaluate per-cell status
        EnemyGrid3D grid = enemy.Grid;
        grid.EvaluatePlacement(property, anchorCell, rotationStep,
                               out Vector3Int[] localCells, out int[] cellStatus);

        if (previewUnitPrefab == null)
        {
            Debug.LogWarning("[PlayerShootPreviewController] previewUnitPrefab is not assigned.");
            return;
        }

        Vector3 cs = enemy.CellSize;
        Vector3 origin = enemy.GridOriginLocal;
        Vector3 unitScale = new Vector3(
            cs.x * unitScaleFactor,
            cs.y * unitScaleFactor,
            cs.z * unitScaleFactor);

        for (int i = 0; i < localCells.Length; i++)
        {
            // Position in enemy local space (cell center)
            Vector3 localPos = origin + new Vector3(
                localCells[i].x * cs.x + cs.x * 0.5f,
                localCells[i].y * cs.y + cs.y * 0.5f,
                localCells[i].z * cs.z + cs.z * 0.5f);

            GameObject unit = Instantiate(previewUnitPrefab, previewRoot.transform);
            unit.name = $"[PreviewUnit_{i}]";
            unit.transform.localPosition = localPos;
            unit.transform.localRotation = Quaternion.identity;
            unit.transform.localScale = unitScale;

            // Disable colliders so preview doesn't interfere with raycasts
            DisableColliders(unit);

            var visual = unit.GetComponent<BaseVisualController>();
            if (visual == null)
                visual = unit.AddComponent<BaseVisualController>();

            // 0 = valid, 1 = conflict, 2 = invalid
            Material mat;
            switch (cellStatus[i])
            {
                case 1:  mat = conflictPreviewMaterial; break;
                case 2:  mat = invalidPreviewMaterial;  break;
                default: mat = validPreviewMaterial;    break;
            }
            visual.SetMaterialAll(mat);

            previewUnitVisuals.Add(visual);
        }
    }

    private static void DisableColliders(GameObject go)
    {
        Collider[] cols = go.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
    }
}
