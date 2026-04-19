using UnityEngine;

/// <summary>
/// Third-person orbit camera that follows a target.
/// The player controls yaw/pitch with the mouse; the camera orbits around the target
/// at a configurable distance. Includes wall-collision pull-in to prevent clipping.
/// <para>
/// Integrates with <see cref="AllCameraManager"/> via <see cref="CameraBase"/>.
/// Set <c>CameraMode</c> to <see cref="CameraMode.ThirdPerson"/> in the Inspector.
/// </para>
/// </summary>
public class CameraThirdPerson : CameraBase
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Tooltip("Offset from the target pivot (e.g. raise to shoulder height).")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Orbit")]
    [Tooltip("Mouse X sensitivity (yaw).")]
    [SerializeField] private float sensitivityX = 3f;

    [Tooltip("Mouse Y sensitivity (pitch).")]
    [SerializeField] private float sensitivityY = 2f;

    [Tooltip("Minimum pitch angle (looking up limit).")]
    [SerializeField] private float pitchMin = -30f;

    [Tooltip("Maximum pitch angle (looking down limit).")]
    [SerializeField] private float pitchMax = 70f;

    [Header("Distance / Zoom")]
    [SerializeField] private bool canZoom = false;
    [SerializeField] private float defaultDistance = 5f;
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 15f;
    [SerializeField] private float zoomSpeed = 3f;

    [Header("Smoothing")]
    [Tooltip("Position smooth time (lower = snappier).")]
    [SerializeField] private float smoothTime = 0.08f;

    [Header("Collision")]
    [Tooltip("Layer mask for camera collision (walls, terrain, etc.).")]
    [SerializeField] private LayerMask collisionMask = ~0;

    [Tooltip("Offset to pull the camera forward from the hit point to avoid clipping.")]
    [SerializeField] private float collisionPadding = 0.2f;

    [Header("Cursor")]
    [Tooltip("Lock and hide cursor when this camera is active.")]
    [SerializeField] private bool lockCursor = true;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = false;

    // Runtime
    private float yaw;
    private float pitch;
    private float currentDistance;
    private Vector3 posVel;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ CameraBase Overrides ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public override void ActivateCamera()
    {
        base.ActivateCamera();

        // Initialize orbit angles from current transform orientation
        Vector3 euler = transform.eulerAngles;
        yaw = euler.y;
        pitch = euler.x;
        // Normalize pitch to [-180, 180] range
        if (pitch > 180f) pitch -= 360f;

        currentDistance = defaultDistance;
        posVel = Vector3.zero;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Snap immediately on activation
        if (target != null)
            SnapToTarget();
    }

    public override void DeactivateCamera()
    {
        base.DeactivateCamera();
        posVel = Vector3.zero;

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public override void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;

        // 1) Mouse input ¡ú yaw / pitch
        float mouseX = Input.GetAxis("Mouse X") * sensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // 2) Scroll ¡ú zoom
        if (canZoom) {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                currentDistance -= scroll * zoomSpeed;
                currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
            }

        }

        // 3) Focus point
        Vector3 focus = target.position + targetOffset;

        // 4) Desired position from orbit
        Quaternion orbitRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = focus - orbitRot * Vector3.forward * currentDistance;

        // 5) Collision: pull camera forward if something blocks the view
        float actualDistance = currentDistance;
        Vector3 dirFromFocus = (desiredPos - focus).normalized;

        if (Physics.Raycast(focus, dirFromFocus, out RaycastHit hit, currentDistance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            actualDistance = hit.distance - collisionPadding;
            if (actualDistance < 0.1f) actualDistance = 0.1f;
        }

        Vector3 collisionAdjustedPos = focus + dirFromFocus * actualDistance;

        // 6) Smooth position
        Vector3 smoothedPos = Vector3.SmoothDamp(transform.position, collisionAdjustedPos, ref posVel, smoothTime);
        transform.position = smoothedPos;

        // 7) Always look at focus point
        transform.rotation = Quaternion.LookRotation(focus - smoothedPos, Vector3.up);

        // 8) Debug
        if (drawDebug)
        {
            Debug.DrawLine(transform.position, focus, Color.cyan);
            Debug.DrawRay(focus, Vector3.up * 0.5f, Color.yellow);
            if (actualDistance < currentDistance)
                Debug.DrawLine(focus, focus + dirFromFocus * currentDistance, Color.red);
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        posVel = Vector3.zero;

        if (target != null && CachedCamera != null && CachedCamera.enabled)
            SnapToTarget();
    }

    public Transform Target => target;

    /// <summary>Current yaw angle (read-only, for movement reference).</summary>
    public float Yaw => yaw;

    /// <summary>Current pitch angle (read-only).</summary>
    public float Pitch => pitch;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Internal ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void SnapToTarget()
    {
        if (target == null) return;

        Vector3 focus = target.position + targetOffset;
        Quaternion orbitRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = focus - orbitRot * Vector3.forward * currentDistance;

        transform.position = desiredPos;
        transform.rotation = Quaternion.LookRotation(focus - desiredPos, Vector3.up);
        posVel = Vector3.zero;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Gizmos ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug || target == null) return;

        Vector3 focus = target.position + targetOffset;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(focus, 0.25f);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(transform.position, focus);
    }
}
