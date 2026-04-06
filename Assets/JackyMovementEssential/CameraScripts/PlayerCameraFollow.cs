using UnityEngine;

[DisallowMultipleComponent]
public class PlayerCameraFollow : CameraBase
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Tooltip("相机跟随的目标点偏移（通常看胸口/头顶）")]
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Angle (2.5D)")]
    [Tooltip("绕Y轴旋转角（45 常见）")]
    [Range(0f, 360f)]
    [SerializeField] private float yaw = 45f;

    [Tooltip("俯视倾角（越大越俯视，比如 50~70）")]
    [Range(10f, 89f)]
    [SerializeField] private float pitch = 60f;

    [Header("Distance / Zoom")]
    [SerializeField] private float distance = 10f;
    [SerializeField] private bool enableZoom = true;
    [SerializeField] private float zoomSpeed = 2.5f;
    [SerializeField] private float minDistance = 6f;
    [SerializeField] private float maxDistance = 18f;

    [Header("Follow Smoothing")]
    [SerializeField] private bool rotateWithTarget = false; // 是否跟随目标旋转（如果玩家会转向，建议开；如果玩家永远面朝一个方向，关也行）

    [Tooltip("位置平滑时间：越小越跟手，越大越柔")]
    [SerializeField] private float smoothTime = 0.10f;

    [Tooltip("旋转插值速度：越大越快跟随朝向")]
    [SerializeField] private float rotationLerp = 12f;

    [Header("Look Ahead (Optional)")]
    [Tooltip("跟随点沿玩家移动方向前移，增强速度感")]
    [SerializeField] private bool enableLookAhead = false;

    [SerializeField] private float lookAheadDistance = 1.5f;
    [SerializeField] private float lookAheadSmoothTime = 0.12f;

    [Tooltip("如果你有 PlayerMotor，可把它的速度喂进来；没有就用 target 的位移近似")]
    [SerializeField] private bool estimateVelocityFromTargetDelta = true;

    [Header("Clamp / Bounds (Optional)")]
    [SerializeField] private bool clampPosition = false;
    [SerializeField] private Vector2 minXZ = new Vector2(-50, -50);
    [SerializeField] private Vector2 maxXZ = new Vector2(50, 50);

    [Header("Debug")]
    [SerializeField] private bool drawDebug = true;

    private Vector3 posVel;              // SmoothDamp 用
    private Vector3 lastTargetPos;
    private Vector3 lookAheadCurrent;
    private Vector3 lookAheadVel;

    [ContextMenu("Reset Parameters")]
    private void Reset()
    {
        // 尽量给个常用默认值
        yaw = 45f;
        pitch = 60f;
        distance = 10f;
        smoothTime = 0.10f;
        rotationLerp = 12f;
    }

    public override void ActivateCamera()
    {
        base.ActivateCamera();
        SnapToTargetImmediate();
    }

    public override void DeactivateCamera()
    {
        base.DeactivateCamera();
        posVel = Vector3.zero;
        lookAheadVel = Vector3.zero;
    }

    public void InitCameraTarget(Transform target)
    {
        if (this.target != null) { 
            Debug.LogWarning("Camera target already set. Overriding with new target.");
        }
        this.target = target;
    }

    public override void LateUpdate()
    {
        if (target == null) return;

        float dt = Time.deltaTime;

        // 1) Zoom
        if (enableZoom)
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f)
            {
                distance -= scroll * zoomSpeed;
                distance = Mathf.Clamp(distance, minDistance, maxDistance);
            }
        }

        // 2) Focus point
        Vector3 focus = target.position + targetOffset;

        // 3) LookAhead（可选）
        if (enableLookAhead)
        {
            Vector3 vel = Vector3.zero;

            if (estimateVelocityFromTargetDelta)
            {
                vel = (target.position - lastTargetPos) / Mathf.Max(dt, 0.0001f);
                lastTargetPos = target.position;
            }

            Vector3 planarVel = new Vector3(vel.x, 0f, vel.z);
            Vector3 dir = planarVel.sqrMagnitude > 0.01f ? planarVel.normalized : Vector3.zero;
            Vector3 targetLookAhead = dir * lookAheadDistance;

            lookAheadCurrent = Vector3.SmoothDamp(
                lookAheadCurrent,
                targetLookAhead,
                ref lookAheadVel,
                lookAheadSmoothTime
            );

            focus += lookAheadCurrent;
        }

        // 4) Desired camera pose (position + rotation)
        Quaternion rigRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = focus - (rigRot * Vector3.forward) * distance;

        // 5) Smooth position
        Vector3 smoothedPos = Vector3.SmoothDamp(transform.position, desiredPos, ref posVel, smoothTime);

        // 6) Clamp (optional)
        if (clampPosition)
        {
            smoothedPos.x = Mathf.Clamp(smoothedPos.x, minXZ.x, maxXZ.x);
            smoothedPos.z = Mathf.Clamp(smoothedPos.z, minXZ.y, maxXZ.y);
        }

        transform.position = smoothedPos;

        // 7) Look at focus (smooth rotation)
        if (rotateWithTarget)
        {
            Quaternion desiredRot = Quaternion.LookRotation(focus - transform.position, Vector3.up);
            float t = 1f - Mathf.Exp(-rotationLerp * dt); // 帧率无关插值
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);
        }
        else
        {
            Quaternion desiredRot = Quaternion.Euler(pitch, yaw, 0f);
            float t = 1f - Mathf.Exp(-rotationLerp * dt); // 帧率无关插值
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, t);
        }

        // 8) Debug
        if (drawDebug)
        {
            Debug.DrawLine(transform.position, focus, Color.cyan);
            Debug.DrawRay(focus, Vector3.up * 0.5f, Color.yellow);
        }
    }

    // 给外部设置目标（例如换角色/过场）
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;

        if (target != null)
            lastTargetPos = target.position;

        lookAheadCurrent = Vector3.zero;
        lookAheadVel = Vector3.zero;
        posVel = Vector3.zero;

        if (CachedCamera != null && CachedCamera.enabled)
            SnapToTargetImmediate();
    }

    private void SnapToTargetImmediate()
    {
        if (target == null)
            return;

        Vector3 focus = target.position + targetOffset;
        Quaternion rigRot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = focus - (rigRot * Vector3.forward) * distance;

        transform.position = desiredPos;
        transform.rotation = rotateWithTarget
            ? Quaternion.LookRotation(focus - transform.position, Vector3.up)
            : Quaternion.Euler(pitch, yaw, 0f);

        lastTargetPos = target.position;
        lookAheadCurrent = Vector3.zero;
        lookAheadVel = Vector3.zero;
        posVel = Vector3.zero;
    }

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
