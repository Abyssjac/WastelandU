using UnityEngine;

[DisallowMultipleComponent]
public class CameraFirstPerson : CameraBase
{
    [Header("Target")]
    [Tooltip("The transform this camera will follow (typically the player character).")]
    [SerializeField] private Transform target;

    [Tooltip("Local offset from the target's pivot to the eye position (e.g. head height).")]
    [SerializeField] private Vector3 eyeOffset = new Vector3(0f, 1.65f, 0.1f);

    [Header("Mouse Sensitivity")]
    [Tooltip("Horizontal (yaw) mouse sensitivity.")]
    [SerializeField] private float sensitivityX = 2f;

    [Tooltip("Vertical (pitch) mouse sensitivity.")]
    [SerializeField] private float sensitivityY = 2f;

    [Tooltip("Multiplier applied on top of base sensitivity for fine-tuning.")]
    [Range(0.1f, 5f)]
    [SerializeField] private float sensitivityMultiplier = 1f;

    [Header("Vertical Look Limits")]
    [Tooltip("Maximum upward angle (negative = look up).")]
    [SerializeField] private float minPitch = -80f;

    [Tooltip("Maximum downward angle (positive = look down).")]
    [SerializeField] private float maxPitch = 80f;

    [Header("Smoothing")]
    [Tooltip("Enable input smoothing to reduce jitter.")]
    [SerializeField] private bool enableSmoothing = true;

    [Tooltip("Smoothing time ¨C lower = snappier, higher = smoother.")]
    [Range(0.001f, 0.15f)]
    [SerializeField] private float smoothTime = 0.03f;

    [Header("Head Bob")]
    [Tooltip("Enable a subtle head bobbing effect while moving.")]
    [SerializeField] private bool enableHeadBob = false;

    [Tooltip("Bobbing frequency (cycles per second).")]
    [SerializeField] private float bobFrequency = 8f;

    [Tooltip("Vertical bob amplitude.")]
    [SerializeField] private float bobAmplitudeY = 0.03f;

    [Tooltip("Horizontal bob amplitude.")]
    [SerializeField] private float bobAmplitudeX = 0.015f;

    [Header("FOV")]
    [Tooltip("Base field-of-view when standing still.")]
    [Range(50f, 120f)]
    [SerializeField] private float baseFOV = 75f;

    [Tooltip("Enable FOV kick when moving fast (e.g. sprinting).")]
    [SerializeField] private bool enableFOVKick = false;

    [Tooltip("Speed threshold above which FOV kick begins.")]
    [SerializeField] private float fovKickSpeedThreshold = 6f;

    [Tooltip("Maximum extra FOV added at high speed.")]
    [SerializeField] private float fovKickAmount = 8f;

    [Tooltip("Speed at which FOV transitions.")]
    [SerializeField] private float fovLerpSpeed = 6f;

    [Header("Cursor Lock")]
    [Tooltip("Lock and hide the cursor when this camera is active.")]
    [SerializeField] private bool lockCursor = true;

    [Header("Rotation Axis")]
    [Tooltip("Invert the Y-axis input.")]
    [SerializeField] private bool invertY = false;

    [Tooltip("Invert the X-axis input.")]
    [SerializeField] private bool invertX = false;

    [Header("Target Body Rotation")]
    [Tooltip("Rotate the target transform around the Y-axis to match yaw (typical FPS setup).")]
    [SerializeField] private bool rotateTargetWithYaw = true;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = false;

    // Runtime state
    private float currentYaw;
    private float currentPitch;
    private float smoothYawVel;
    private float smoothPitchVel;
    private float smoothYaw;
    private float smoothPitch;
    private float bobTimer;
    private float currentFOV;
    private Vector3 lastTargetPos;

    public override void ActivateCamera()
    {
        base.ActivateCamera();

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        currentFOV = baseFOV;
        if (CachedCamera != null)
            CachedCamera.fieldOfView = baseFOV;

        SnapToTargetImmediate();
    }

    public override void DeactivateCamera()
    {
        base.DeactivateCamera();

        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        smoothYawVel = 0f;
        smoothPitchVel = 0f;
        bobTimer = 0f;
    }

    public void InitCameraTarget(Transform newTarget)
    {
        if (target != null)
        {
            Debug.LogWarning($"[{nameof(CameraFirstPerson)}] Camera target already set. Overriding with new target.");
        }
        target = newTarget;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
            lastTargetPos = target.position;

        smoothYawVel = 0f;
        smoothPitchVel = 0f;
        bobTimer = 0f;

        if (CachedCamera != null && CachedCamera.enabled)
            SnapToTargetImmediate();
    }

    public override void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;

        // ---- 1) Mouse input ----
        float mouseX = Input.GetAxis("Mouse X") * sensitivityX * sensitivityMultiplier * (invertX ? -1f : 1f);
        float mouseY = Input.GetAxis("Mouse Y") * sensitivityY * sensitivityMultiplier * (invertY ? -1f : 1f);

        currentYaw += mouseX;
        currentPitch -= mouseY;
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        // ---- 2) Smoothing ----
        float yaw, pitch;
        if (enableSmoothing)
        {
            smoothYaw = Mathf.SmoothDamp(smoothYaw, currentYaw, ref smoothYawVel, smoothTime);
            smoothPitch = Mathf.SmoothDamp(smoothPitch, currentPitch, ref smoothPitchVel, smoothTime);
            yaw = smoothYaw;
            pitch = smoothPitch;
        }
        else
        {
            yaw = currentYaw;
            pitch = currentPitch;
            smoothYaw = currentYaw;
            smoothPitch = currentPitch;
        }

        // ---- 3) Eye position ----
        Vector3 eyePos = target.position + target.TransformDirection(eyeOffset);

        // ---- 4) Head bob ----
        if (enableHeadBob)
        {
            Vector3 velocity = (target.position - lastTargetPos) / Mathf.Max(dt, 0.0001f);
            float horizontalSpeed = new Vector3(velocity.x, 0f, velocity.z).magnitude;

            if (horizontalSpeed > 0.2f)
            {
                bobTimer += dt * bobFrequency;
                float bobX = Mathf.Cos(bobTimer * Mathf.PI * 2f) * bobAmplitudeX;
                float bobY = Mathf.Sin(bobTimer * Mathf.PI * 2f) * bobAmplitudeY;
                eyePos += transform.right * bobX + transform.up * bobY;
            }
            else
            {
                bobTimer = 0f;
            }
        }

        lastTargetPos = target.position;

        // ---- 5) Apply transform ----
        transform.position = eyePos;
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

        // ---- 6) Rotate target body ----
        if (rotateTargetWithYaw)
        {
            target.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ---- 7) FOV kick ----
        if (enableFOVKick && CachedCamera != null)
        {
            Vector3 vel = (target.position - lastTargetPos) / Mathf.Max(dt, 0.0001f);
            float speed = new Vector3(vel.x, 0f, vel.z).magnitude;
            float targetFOV = speed > fovKickSpeedThreshold
                ? baseFOV + fovKickAmount
                : baseFOV;

            currentFOV = Mathf.Lerp(currentFOV, targetFOV, 1f - Mathf.Exp(-fovLerpSpeed * dt));
            CachedCamera.fieldOfView = currentFOV;
        }

        // ---- 8) Debug ----
        if (drawDebug)
        {
            Debug.DrawLine(target.position, transform.position, Color.green);
            Debug.DrawRay(transform.position, transform.forward * 2f, Color.red);
        }
    }

    private void SnapToTargetImmediate()
    {
        if (target == null) return;

        Vector3 eyePos = target.position + target.TransformDirection(eyeOffset);
        transform.position = eyePos;

        // Initialise yaw/pitch from current target orientation
        currentYaw = target.eulerAngles.y;
        currentPitch = 0f;
        smoothYaw = currentYaw;
        smoothPitch = currentPitch;
        smoothYawVel = 0f;
        smoothPitchVel = 0f;
        bobTimer = 0f;
        lastTargetPos = target.position;

        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebug || target == null) return;

        Vector3 eyePos = target.position + target.TransformDirection(eyeOffset);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(eyePos, 0.08f);

        Gizmos.color = Color.white;
        Gizmos.DrawLine(target.position, eyePos);
    }
}
