using System.Collections.Generic;
using UnityEngine;
using JackyUtility;
public class BuildPositionProvider : MonoBehaviour, IDebuggable
{
    [Header("Reference")]
    [SerializeField] private Camera targetCamera;

    [Header("Raycast")]
    [SerializeField] private LayerMask buildSurfaceMask;
    [SerializeField] private float maxRayDistance = 1000f;

    [Header("Grid Settings")]
    [SerializeField] private Vector3 gridOriginWorld = Vector3.zero;
    [SerializeField] private Vector3 cellSize = Vector3.one;

    public Vector3 CellSize => cellSize;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;
    [SerializeField] private float debugSphereRadius = 0.15f;
    [SerializeField] private Color debugHitColor = Color.red;
    [SerializeField] private Color debugSnappedColor = Color.green;
    [SerializeField] private Color debugSnappedCenterColor = Color.cyan;

    // ---- IDebuggable ----
    public string DebugId => "buildpos";
    public bool DebugEnabled
    {
        get => enableDebug;
        set => enableDebug = value;
    }

    public bool HasValidHit { get; private set; }
    public Vector3 CurrentHitWorldPosition { get; private set; }
    public Vector3Int CurrentCell { get; private set; }
    public Vector3 CurrentSnappedWorldPosition { get; private set; }
    public Vector3 CurrentSnappedWorldPositionCenter { get; private set; }

    /// <summary>
    /// The BuildableBehaviour on the object hit by the raycast this frame (null if none).
    /// Uses GetComponentInParent so child-colliders are supported.
    /// </summary>
    public BuildableBehaviour CurrentHitBuildable { get; private set; }

    private void Start()
    {
        if (targetCamera == null)
        {
            List<CameraBase> activateCameras = AllCameraManager.Instance.FindCamerasActivated();
            if (activateCameras.Count > 0)
            {
                targetCamera = activateCameras[0].CachedCamera;
                Debug.LogWarning("No camera assigned to BuildPositionProvider. Automatically assigned the first active camera: " + targetCamera.name);
            }
            else
            {
                Debug.LogError("No active camera found for BuildPositionProvider.");
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
        AllCameraManager.Instance.OnCameraModeSwitched += OnCameraModeSwitched_SetActivateCamera;
    }

    private void OnDisable()
    {
        AllCameraManager.Instance.OnCameraModeSwitched -= OnCameraModeSwitched_SetActivateCamera;
    }

    private void OnCameraModeSwitched_SetActivateCamera(CameraMode curMode)
    { 
        List<CameraBase> activateCameras = AllCameraManager.Instance.FindCamerasByMode(curMode);
        if (activateCameras.Count > 0)
        {
            targetCamera = activateCameras[0].CachedCamera;
            Debug.LogWarning("Camera Mode Switched, set to thef first active camera " + targetCamera.name);
        }
        else
        {
            Debug.LogError("No active camera found for BuildPositionProvider.");
        }
    }

    private void Update()
    {
        UpdateMouseGridHit();
    }

    private void UpdateMouseGridHit()
    {
        if (targetCamera == null)
        {
            HasValidHit = false;
            return;
        }

        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, buildSurfaceMask))
        {
            HasValidHit = true;
            CurrentHitWorldPosition = hit.point;
            CurrentCell = WorldToCell(hit.point);
            CurrentSnappedWorldPosition = CellToWorld(CurrentCell);
            CurrentSnappedWorldPositionCenter = CellToWorldCenter(CurrentCell);
            CurrentHitBuildable = hit.collider.GetComponentInParent<BuildableBehaviour>();
        }
        else
        {
            HasValidHit = false;
            CurrentHitBuildable = null;
        }
    }

    public Vector3Int WorldToCell(Vector3 worldPosition)
    {
        Vector3 local = worldPosition - gridOriginWorld;

        return new Vector3Int(
            Mathf.FloorToInt(local.x / cellSize.x),
            Mathf.FloorToInt(local.y / cellSize.y),
            Mathf.FloorToInt(local.z / cellSize.z)
        );
    }

    public Vector3 CellToWorld(Vector3Int cell)
    {
        return new Vector3(
            gridOriginWorld.x + cell.x * cellSize.x,
            gridOriginWorld.y + cell.y * cellSize.y,
            gridOriginWorld.z + cell.z * cellSize.z
        );
    }

    public Vector3 CellToWorldCenter(Vector3Int cell)
    {
        return CellToWorld(cell) + new Vector3(
            cellSize.x * 0.5f,
            cellSize.y * 0.5f,
            cellSize.z * 0.5f
        );
    }

    public BuildPositionInfo GetCurrentBuildPositionInfo()
    {
        return new BuildPositionInfo
        {
            HasValidHit = this.HasValidHit,
            CurrentHitWorldPosition = this.CurrentHitWorldPosition,
            CurrentCell = this.CurrentCell,
            CurrentSnappedWorldPosition = this.CurrentSnappedWorldPosition,
            CurrentSnappedWorldPositionCenter = this.CurrentSnappedWorldPositionCenter
        };
    }

    #region Debug Gizmos

    private void OnDrawGizmos()
    {
        if (!enableDebug) return;
        if (!HasValidHit) return;

        // Raw hit point ¡ª red sphere
        Gizmos.color = debugHitColor;
        Gizmos.DrawSphere(CurrentHitWorldPosition, debugSphereRadius);

        // Snapped position (cell corner) ¡ª green wire sphere
        Gizmos.color = debugSnappedColor;
        Gizmos.DrawWireSphere(CurrentSnappedWorldPosition, debugSphereRadius * 0.8f);

        // Snapped center ¡ª cyan wire sphere
        Gizmos.color = debugSnappedCenterColor;
        Gizmos.DrawWireSphere(CurrentSnappedWorldPositionCenter, debugSphereRadius * 0.8f);

        // Draw the cell wireframe
        Gizmos.color = debugSnappedColor;
        Gizmos.DrawWireCube(CurrentSnappedWorldPositionCenter, cellSize);

        // Line from hit to snapped center
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(CurrentHitWorldPosition, CurrentSnappedWorldPositionCenter);

#if UNITY_EDITOR
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(CurrentHitWorldPosition + Vector3.up * 0.4f,
            $"BuildPositionInfo\n" +
            $"HasValidHit: {HasValidHit}\n" +
            $"HitWorld: {CurrentHitWorldPosition:F2}\n" +
            $"Cell: {CurrentCell}\n" +
            $"Snapped: {CurrentSnappedWorldPosition:F2}\n" +
            $"SnappedCenter: {CurrentSnappedWorldPositionCenter:F2}");
#endif
    }
    #endregion
}

public struct BuildPositionInfo
{
    public bool HasValidHit;
    public Vector3 CurrentHitWorldPosition;
    public Vector3Int CurrentCell;
    public Vector3 CurrentSnappedWorldPosition;
    public Vector3 CurrentSnappedWorldPositionCenter;

    public override string ToString()
    {
        return $"BuildPositionInfo(HasValidHit: {HasValidHit}, CurrentHitWorldPosition: {CurrentHitWorldPosition}, CurrentCell: {CurrentCell}, CurrentSnappedWorldPosition: {CurrentSnappedWorldPosition}, CurrentSnappedWorldPositionCenter: {CurrentSnappedWorldPositionCenter})";
    }
}
