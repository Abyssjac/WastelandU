using System.Collections.Generic;
using UnityEngine;
using JackyUtility;

/// <summary>
/// Result of a weapon raycast hit against an enemy grid.
/// </summary>
public struct WeaponHitResult
{
    /// <summary>The local-space cell that was hit on the enemy grid.</summary>
    public Vector3Int HitCell;

    /// <summary>Raw world-space hit point.</summary>
    public Vector3 HitWorldPosition;

    /// <summary>World-space position snapped to the cell corner.</summary>
    public Vector3 HitSnappedWorldPosition;

    /// <summary>World-space position snapped to the cell center.</summary>
    public Vector3 HitSnappedWorldPositionCenter;

    /// <summary>The enemy grid behaviour that was hit.</summary>
    public EnemyGridBehaviour HitEnemyGridBehaviour;

    /// <summary>World-space normal of the surface that was hit.</summary>
    public Vector3 HitNormal;

    /// <summary>The placed buildable that was hit (via buildSelectableMask). Null if no buildable was hit.</summary>
    public BuildableBehaviour HitBuildable;
}

/// <summary>
/// Provides weapon aiming information by casting a ray from the screen center.
/// Detects which <see cref="EnemyGridBehaviour"/> and which cell the player is aiming at.
/// Designed for third-person perspective gameplay.
/// </summary>
public class PlayerShootPositionProvider : MonoBehaviour, IDebuggable
{
    [Header("Reference")]
    [SerializeField] private Camera targetCamera;

    [Header("Raycast")]
    [Tooltip("Layer mask for enemy surfaces that can be hit (for placement).")]
    [SerializeField] private LayerMask enemyHitMask;

    [Tooltip("Layer mask for selectable placed buildable objects (for recycling).")]
    [SerializeField] private LayerMask buildSelectableMask;

    [SerializeField] private float maxRayDistance = 1000f;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;
    [SerializeField] private float debugSphereRadius = 0.15f;

    // ---- IDebuggable ----
    public string DebugId => "shootpos";
    public bool DebugEnabled
    {
        get => enableDebug;
        set => enableDebug = value;
    }

    /// <summary>Whether a valid enemy grid was hit this frame.</summary>
    public bool HasValidHit { get; private set; }

    /// <summary>Current hit result. Only valid when <see cref="HasValidHit"/> is true.</summary>
    public WeaponHitResult CurrentHitResult { get; private set; }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Start()
    {
        if (targetCamera == null)
        {
            List<CameraBase> activateCameras = AllCameraManager.Instance.FindCamerasActivated();
            if (activateCameras.Count > 0)
            {
                targetCamera = activateCameras[0].CachedCamera;
                Debug.LogWarning("[PlayerShootPositionProvider] No camera assigned. Automatically assigned: " + targetCamera.name);
            }
            else
            {
                Debug.LogError("[PlayerShootPositionProvider] No active camera found.");
            }
        }

        DebugConsoleManager.Instance.RegisterDebugTarget(this);
    }

    private void OnDestroy()
    {
        if (DebugConsoleManager.Instance != null)
            DebugConsoleManager.Instance.UnregisterDebugTarget(this);
    }

    private void OnEnable()
    {
        AllCameraManager.Instance.OnCameraModeSwitched += OnCameraModeSwitched;
    }

    private void OnDisable()
    {
        AllCameraManager.Instance.OnCameraModeSwitched -= OnCameraModeSwitched;
    }

    private void OnCameraModeSwitched(CameraMode curMode)
    {
        List<CameraBase> activateCameras = AllCameraManager.Instance.FindCamerasByMode(curMode);
        if (activateCameras.Count > 0)
        {
            targetCamera = activateCameras[0].CachedCamera;
            Debug.LogWarning("[PlayerShootPositionProvider] Camera mode switched, set to: " + targetCamera.name);
        }
        else
        {
            Debug.LogError("[PlayerShootPositionProvider] No active camera found for mode: " + curMode);
        }
    }

    private void Update()
    {
        UpdateScreenCenterRaycast();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Raycast ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void UpdateScreenCenterRaycast()
    {
        if (targetCamera == null)
        {
            HasValidHit = false;
            return;
        }

        // Ray from the center of the screen (crosshair position)
        Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
        Ray ray = targetCamera.ScreenPointToRay(screenCenter);

        // --- Raycast 1: Enemy grid surface (for placement) ---
        EnemyGridBehaviour enemyGrid = null;
        Vector3Int localCell = Vector3Int.zero;
        Vector3 hitPoint = Vector3.zero;
        Vector3 hitNormal = Vector3.zero;
        Vector3 snappedWorld = Vector3.zero;
        Vector3 snappedCenter = Vector3.zero;
        bool gridHit = false;

        if (Physics.Raycast(ray, out RaycastHit gridRayHit, maxRayDistance, enemyHitMask))
        {
            enemyGrid = gridRayHit.collider.GetComponentInParent<EnemyGridBehaviour>();
            if (enemyGrid != null)
            {
                localCell = enemyGrid.WorldToLocalCell(gridRayHit.point);
                hitPoint = gridRayHit.point;
                hitNormal = gridRayHit.normal;
                snappedWorld = enemyGrid.LocalCellToWorld(localCell);
                snappedCenter = enemyGrid.LocalCellToWorldCenter(localCell);
                gridHit = true;
            }
        }

        // --- Raycast 2: Selectable buildable (for recycling) ---
        BuildableBehaviour hitBuildable = null;

        if (Physics.Raycast(ray, out RaycastHit selectableHit, maxRayDistance, buildSelectableMask))
        {
            hitBuildable = selectableHit.collider.GetComponentInParent<BuildableBehaviour>();

            // If we didn't hit a grid surface but did hit a buildable on an enemy, fill grid info from it
            if (!gridHit && hitBuildable != null)
            {
                enemyGrid = selectableHit.collider.GetComponentInParent<EnemyGridBehaviour>();
                if (enemyGrid != null)
                {
                    localCell = enemyGrid.WorldToLocalCell(selectableHit.point);
                    hitPoint = selectableHit.point;
                    hitNormal = selectableHit.normal;
                    snappedWorld = enemyGrid.LocalCellToWorld(localCell);
                    snappedCenter = enemyGrid.LocalCellToWorldCenter(localCell);
                    gridHit = true;
                }
            }
        }

        if (gridHit)
        {
            HasValidHit = true;
            CurrentHitResult = new WeaponHitResult
            {
                HitCell = localCell,
                HitWorldPosition = hitPoint,
                HitSnappedWorldPosition = snappedWorld,
                HitSnappedWorldPositionCenter = snappedCenter,
                HitEnemyGridBehaviour = enemyGrid,
                HitNormal = hitNormal,
                HitBuildable = hitBuildable,
            };
        }
        else
        {
            HasValidHit = false;
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Debug ©¤©¤©¤©¤©¤©¤©¤©¤©¤

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!enableDebug) return;
        if (!HasValidHit) return;

        WeaponHitResult r = CurrentHitResult;

        // Raw hit point
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(r.HitWorldPosition, debugSphereRadius);

        // Snapped center
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(r.HitSnappedWorldPositionCenter, debugSphereRadius * 0.8f);

        // Cell wireframe
        Gizmos.color = Color.green;
        if (r.HitEnemyGridBehaviour != null)
        {
            Vector3 size = r.HitEnemyGridBehaviour.transform.TransformVector(r.HitEnemyGridBehaviour.CellSize);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            Gizmos.DrawWireCube(r.HitSnappedWorldPositionCenter, size);
        }

        // Hit normal
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(r.HitWorldPosition, r.HitNormal * 0.5f);

        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(r.HitWorldPosition + Vector3.up * 0.5f,
            $"WeaponHitResult\n" +
            $"Cell: {r.HitCell}\n" +
            $"HitWorld: {r.HitWorldPosition:F2}\n" +
            $"SnappedCenter: {r.HitSnappedWorldPositionCenter:F2}\n" +
            $"Normal: {r.HitNormal:F2}\n" +
            $"Enemy: {(r.HitEnemyGridBehaviour != null ? r.HitEnemyGridBehaviour.gameObject.name : "null")}\n" +
            $"Buildable: {(r.HitBuildable != null ? r.HitBuildable.gameObject.name : "null")}");
    }
#endif
}
