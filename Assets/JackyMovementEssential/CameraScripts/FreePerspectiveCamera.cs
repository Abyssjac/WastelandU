using UnityEngine;

/// <summary>
/// Free-roaming camera for build mode / spectator.
/// Controls:
///   A/D       ¡ú move along camera local X axis
///   W/S       ¡ú move along world Y axis (up/down)
///   Q/E       ¡ú rotate around world Y axis (yaw)
///   ScrollWheel ¡ú zoom (move along camera local Z axis)
///   Middle Mouse Drag (vertical) ¡ú rotate around camera local X axis (pitch)
/// </summary>
public class FreePerspectiveCamera : CameraBase
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;
    [SerializeField] private float verticalSpeed = 8f;

    [Header("Rotation")]
    [SerializeField] private float yawSpeed = 90f;         // degrees per second
    [SerializeField] private float pitchSpeed = 3f;         // sensitivity for middle-mouse drag
    [SerializeField] private float minPitch = 10f;
    [SerializeField] private float maxPitch = 85f;

    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 5f;

    [Header("Smoothing")]
    [SerializeField] private float moveSmoothTime = 0.08f;
    [SerializeField] private float rotationSmoothTime = 0.06f;

    // Runtime state
    private float currentYaw;
    private float currentPitch;
    private float targetYaw;
    private float targetPitch;
    private float yawVel;
    private float pitchVel;

    private Vector3 targetPosition;
    private Vector3 posVel;

    public override void ActivateCamera()
    {
        base.ActivateCamera();

        // Initialize from current transform
        Vector3 euler = transform.eulerAngles;
        currentYaw = targetYaw = euler.y;
        currentPitch = targetPitch = euler.x;
        if (currentPitch > 180f) currentPitch -= 360f;
        targetPitch = currentPitch;

        targetPosition = transform.position;
        posVel = Vector3.zero;
        yawVel = 0f;
        pitchVel = 0f;
    }

    public override void DeactivateCamera()
    {
        base.DeactivateCamera();
        posVel = Vector3.zero;
        yawVel = 0f;
        pitchVel = 0f;
    }

    public override void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // ©¤©¤ Rotation: Q/E yaw ©¤©¤
        float yawInput = 0f;
        if (Input.GetKey(KeyCode.E)) yawInput += 1f;
        if (Input.GetKey(KeyCode.Q)) yawInput -= 1f;
        targetYaw += yawInput * yawSpeed * dt;

        // ©¤©¤ Rotation: Middle mouse drag ¡ú pitch ©¤©¤
        if (Input.GetMouseButton(2)) // middle button held
        {
            float mouseY = Input.GetAxis("Mouse Y");
            targetPitch -= mouseY * pitchSpeed;
            targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
        }

        // Smooth rotation
        currentYaw = Mathf.SmoothDamp(currentYaw, targetYaw, ref yawVel, rotationSmoothTime, Mathf.Infinity, dt);
        currentPitch = Mathf.SmoothDamp(currentPitch, targetPitch, ref pitchVel, rotationSmoothTime, Mathf.Infinity, dt);

        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);

        // ©¤©¤ Movement: A/D along camera local X ©¤©¤
        float horizontal = 0f;
        if (Input.GetKey(KeyCode.D)) horizontal += 1f;
        if (Input.GetKey(KeyCode.A)) horizontal -= 1f;

        // ©¤©¤ Movement: W/S along world Y ©¤©¤
        float vertical = 0f;
        if (Input.GetKey(KeyCode.W)) vertical += 1f;
        if (Input.GetKey(KeyCode.S)) vertical -= 1f;

        Vector3 moveDir = transform.right * horizontal * moveSpeed
                        + Vector3.up * vertical * verticalSpeed;

        targetPosition += moveDir * dt;

        // ©¤©¤ Zoom: scroll wheel along camera local Z ©¤©¤
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            targetPosition += transform.forward * scroll * zoomSpeed;
        }

        // Smooth position
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref posVel, moveSmoothTime, Mathf.Infinity, dt);
    }
}
