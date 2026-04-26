using System.Collections.Generic;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Preview controller for the weapon/shoot system.
/// Supports two preview modes:
/// <list type="bullet">
/// <item><b>Build</b>: per-cell colored cubes showing where a buildable would be placed.</item>
/// <item><b>Recycle</b>: alert-colored cubes highlighting the footprint of the aimed buildable.</item>
/// </list>
/// Preview cubes are parented to the hit enemy so they follow its movement and rotation.
/// </summary>
public class PlayerShootPreviewController : MonoBehaviour
{
    [Header("Preview Materials")]
    [SerializeField] private Material validPreviewMaterial;
    [SerializeField] private Material invalidPreviewMaterial;
    [SerializeField] private Material conflictPreviewMaterial;

    [Tooltip("Material used to highlight a buildable's footprint in Recycle mode.")]
    [SerializeField] private Material recyclePreviewMaterial;

    [Tooltip("Material used when the aimed buildable cannot be recycled (canMove = false).")]
    [SerializeField] private Material recycleDisabledMaterial;

    [Header("Preview Unit")]
    [Tooltip("A simple cube prefab (no Collider) with a BaseVisualController component.")]
    [SerializeField] private GameObject previewUnitPrefab;

    [Tooltip("Slight scale multiplier for preview units to avoid z-fighting.")]
    [SerializeField] private float unitScaleFactor = 0.95f;

    // Runtime ¡ª shared
    private GameObject previewRoot;
    private List<BaseVisualController> previewUnitVisuals = new List<BaseVisualController>();

    // Cache ¡ª Build mode
    private EnemyGridBehaviour cachedEnemy;
    private BuildableProperty cachedProperty;
    private int cachedRotationStep;
    private Vector3Int cachedAnchorCell;

    // Cache ¡ª Recycle mode
    private BuildableBehaviour cachedRecycleBuildable;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API: Build Mode ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Update the Build-mode preview. Shows per-cell placement status cubes.
    /// </summary>
    public void UpdateBuildPreview(bool hasHit, WeaponHitResult hitResult, BuildableProperty property, int rotationStep)
    {
        if (!hasHit || property == null || hitResult.HitEnemyGridBehaviour == null)
        {
            HidePreview();
            return;
        }

        EnemyGridBehaviour enemy = hitResult.HitEnemyGridBehaviour;
        Vector3Int anchorCell = hitResult.HitCell;

        bool needsRebuild = previewRoot == null
            || cachedEnemy != enemy
            || cachedProperty != property
            || cachedRotationStep != rotationStep
            || cachedAnchorCell != anchorCell
            || cachedRecycleBuildable != null; // was in recycle mode before

        if (needsRebuild)
        {
            HidePreview();
            BuildPlacementPreview(enemy, property, anchorCell, rotationStep);
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API: Recycle Mode ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Update the Recycle-mode preview. Highlights the footprint of the aimed buildable.
    /// </summary>
    public void UpdateRecyclePreview(bool hasHit, WeaponHitResult hitResult)
    {
        if (!hasHit || hitResult.HitBuildable == null)
        {
            HidePreview();
            return;
        }

        BuildableBehaviour buildable = hitResult.HitBuildable;

        // Same buildable as last frame ¡ª no rebuild needed
        if (previewRoot != null && cachedRecycleBuildable == buildable)
            return;

        HidePreview();
        BuildRecyclePreview(buildable);
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API: Common ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Hide and destroy all preview objects (both modes).
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
        cachedRecycleBuildable = null;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Internal: Build Preview ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void BuildPlacementPreview(EnemyGridBehaviour enemy, BuildableProperty property,
                                       Vector3Int anchorCell, int rotationStep)
    {
        cachedEnemy = enemy;
        cachedProperty = property;
        cachedRotationStep = rotationStep;
        cachedAnchorCell = anchorCell;
        cachedRecycleBuildable = null;

        previewRoot = new GameObject("[ShootPreview]");
        previewRoot.transform.SetParent(enemy.transform, false);
        previewRoot.transform.localPosition = Vector3.zero;
        previewRoot.transform.localRotation = Quaternion.identity;
        previewRoot.transform.localScale = Vector3.one;

        EnemyGrid3D grid = enemy.Grid;
        grid.EvaluatePlacement(property, anchorCell, rotationStep,
                               out Vector3Int[] localCells, out int[] cellStatus);

        if (previewUnitPrefab == null) return;

        Vector3 cs = enemy.CellSize;
        Vector3 origin = enemy.GridOriginLocal;
        Vector3 unitScale = new Vector3(cs.x * unitScaleFactor, cs.y * unitScaleFactor, cs.z * unitScaleFactor);

        for (int i = 0; i < localCells.Length; i++)
        {
            Vector3 localPos = origin + new Vector3(
                localCells[i].x * cs.x + cs.x * 0.5f,
                localCells[i].y * cs.y + cs.y * 0.5f,
                localCells[i].z * cs.z + cs.z * 0.5f);

            GameObject unit = Instantiate(previewUnitPrefab, previewRoot.transform);
            unit.name = $"[PreviewUnit_{i}]";
            unit.transform.localPosition = localPos;
            unit.transform.localRotation = Quaternion.identity;
            unit.transform.localScale = unitScale;
            DisableColliders(unit);

            var visual = unit.GetComponent<BaseVisualController>();
            if (visual == null) visual = unit.AddComponent<BaseVisualController>();

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

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Internal: Recycle Preview ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void BuildRecyclePreview(BuildableBehaviour buildable)
    {
        cachedRecycleBuildable = buildable;
        cachedEnemy = null;
        cachedProperty = null;

        PlacedBuildableData data = buildable.Data;
        if (data == null) return;

        EnemyGridBehaviour enemy = buildable.GetComponentInParent<EnemyGridBehaviour>();

        // ©¤©¤ Detached floating block: no grid parent, parent preview directly to the buildable ©¤©¤
        if (enemy == null)
        {
            if (!buildable.IsDetached) return;

            previewRoot = new GameObject("[RecyclePreview]");
            previewRoot.transform.SetParent(buildable.transform, false);
            previewRoot.transform.localPosition = Vector3.zero;
            previewRoot.transform.localRotation = Quaternion.identity;
            previewRoot.transform.localScale = Vector3.one;

            if (previewUnitPrefab == null) return;

            bool detachedCanRecycle = data.Property != null && data.Property.canMove;
            Material detachedMat = detachedCanRecycle ? recyclePreviewMaterial : recycleDisabledMaterial;
            if (detachedMat == null) detachedMat = detachedCanRecycle ? validPreviewMaterial : invalidPreviewMaterial;

            // The buildable's localScale == cellSize, so 1 local unit == 1 cell in world space.
            // The buildable's transform origin == center of the anchor cell.
            // The buildable GO already carries the rotation (Euler(0, rotationStep*90, 0)),
            // so we use the UNROTATED footprint as local offsets ¡ª the parent transform
            // handles direction; applying GetRotatedFootprint here would double-rotate.
            Vector3Int[] detachedFootprint = data.Property.GetFootprint();
            for (int i = 0; i < detachedFootprint.Length; i++)
            {
                GameObject unit = Instantiate(previewUnitPrefab, previewRoot.transform);
                unit.name = $"[RecycleUnit_Detached_{i}]";
                unit.transform.localPosition = new Vector3(detachedFootprint[i].x, detachedFootprint[i].y, detachedFootprint[i].z);
                unit.transform.localRotation = Quaternion.identity;
                unit.transform.localScale = Vector3.one * unitScaleFactor;
                DisableColliders(unit);

                var visual = unit.GetComponent<BaseVisualController>();
                if (visual == null) visual = unit.AddComponent<BaseVisualController>();
                visual.SetMaterialAll(detachedMat);
                previewUnitVisuals.Add(visual);
            }
            return;
        }

        previewRoot = new GameObject("[RecyclePreview]");
        previewRoot.transform.SetParent(enemy.transform, false);
        previewRoot.transform.localPosition = Vector3.zero;
        previewRoot.transform.localRotation = Quaternion.identity;
        previewRoot.transform.localScale = Vector3.one;

        if (previewUnitPrefab == null) return;

        // Get the footprint cells of the aimed buildable
        Vector3Int[] footprint = data.Property.GetRotatedFootprint(data.RotationStep);
        Vector3 cs = enemy.CellSize;
        Vector3 origin = enemy.GridOriginLocal;
        Vector3 unitScale = new Vector3(cs.x * unitScaleFactor, cs.y * unitScaleFactor, cs.z * unitScaleFactor);

        // Choose material based on canMove
        bool canRecycle = data.Property.canMove;
        Material mat = canRecycle ? recyclePreviewMaterial : recycleDisabledMaterial;
        if (mat == null) mat = canRecycle ? validPreviewMaterial : invalidPreviewMaterial; // fallback

        for (int i = 0; i < footprint.Length; i++)
        {
            Vector3Int worldCell = data.AnchorCell + footprint[i];

            Vector3 localPos = origin + new Vector3(
                worldCell.x * cs.x + cs.x * 0.5f,
                worldCell.y * cs.y + cs.y * 0.5f,
                worldCell.z * cs.z + cs.z * 0.5f);

            GameObject unit = Instantiate(previewUnitPrefab, previewRoot.transform);
            unit.name = $"[RecycleUnit_{i}]";
            unit.transform.localPosition = localPos;
            unit.transform.localRotation = Quaternion.identity;
            unit.transform.localScale = unitScale;
            DisableColliders(unit);

            var visual = unit.GetComponent<BaseVisualController>();
            if (visual == null) visual = unit.AddComponent<BaseVisualController>();
            visual.SetMaterialAll(mat);
            previewUnitVisuals.Add(visual);
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Utility ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private static void DisableColliders(GameObject go)
    {
        Collider[] cols = go.GetComponentsInChildren<Collider>();
        for (int i = 0; i < cols.Length; i++) cols[i].enabled = false;
    }
}
