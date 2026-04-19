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
    [Tooltip("Layer mask for enemy surfaces that can be hit.")]
    [SerializeField] private LayerMask enemyHitMask;

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

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, enemyHitMask))
        {
            EnemyGridBehaviour enemyGrid = hit.collider.GetComponentInParent<EnemyGridBehaviour>();
            if (enemyGrid != null)
            {
                Vector3Int localCell = enemyGrid.WorldToLocalCell(hit.point);
                Vector3 snappedWorld = enemyGrid.LocalCellToWorld(localCell);
                Vector3 snappedCenter = enemyGrid.LocalCellToWorldCenter(localCell);

                HasValidHit = true;
                CurrentHitResult = new WeaponHitResult
                {
                    HitCell = localCell,
                    HitWorldPosition = hit.point,
                    HitSnappedWorldPosition = snappedWorld,
                    HitSnappedWorldPositionCenter = snappedCenter,
                    HitEnemyGridBehaviour = enemyGrid,
                    HitNormal = hit.normal,
                };
                return;
            }
        }

        HasValidHit = false;
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
            $"Enemy: {(r.HitEnemyGridBehaviour != null ? r.HitEnemyGridBehaviour.gameObject.name : "null")}");
    }
#endif
}
