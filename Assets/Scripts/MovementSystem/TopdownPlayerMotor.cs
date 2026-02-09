using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[DisallowMultipleComponent]
public class TopdownPlayerMotor : MonoBehaviour
{
    // ----------------------------
    // References
    // ----------------------------
    private CharacterController controller;
    private PlayerControl controls;

    // ----------------------------
    // Movement Settings
    // ----------------------------
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("转向平滑度（越大越快）")]
    [SerializeField] private float rotateSpeed = 15f;

    [Tooltip("是否允许相机相对移动（2.5D 常见）。不开则世界坐标 WASD。")]
    [SerializeField] private bool useCameraRelativeMove = false;
    [SerializeField] private Transform moveReference;


    // ----------------------------
    // Ground / Gravity
    // ----------------------------
    [Header("Ground / Gravity")]
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float gravity = 20f;          // 用“正数”，代码里向下施加
    [SerializeField] private float fallSpeedMax = 25f;
    [SerializeField] private float groundStickVelocity = 2f; // 贴地速度（避免小坡弹起）
    [SerializeField] private float groundRayExtra = 0.15f;    // 射线额外长度

    // ----------------------------
    // Dash Settings
    // ----------------------------
    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1f;

    [Tooltip("Dash 方向锁定方式：true=锁定当前面朝方向；false=锁定当前输入方向")]
    [SerializeField] private bool dashLockToFacing = true;

    // ----------------------------
    // Debug
    // ----------------------------
    [Header("Debug")]
    [SerializeField] private bool debugOverlay = true;
    [SerializeField] private bool debugDrawRays = true;
    [SerializeField] private bool debugLogStateChanges = false;

    // ----------------------------
    // Runtime State
    // ----------------------------
    private Vector3 moveDirWorld;        // 输入方向（世界空间，y=0）
    private Vector3 planarVelocity;      // 水平速度（x,z）
    private float verticalVelocity;      // 垂直速度（y）
    private bool isGrounded;

    // Dash runtime
    private bool isDashing;
    private float dashTimer;
    private float lastDashTime;
    private Vector3 dashDirWorld;

    // Debug cache（只在变化时 log）
    private bool prevGrounded;
    private bool prevDashing;

    // ----------------------------
    // Unity
    // ----------------------------
    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        controls = GetComponent<PlayerControl>();

        if (useCameraRelativeMove && moveReference == null) { 
            Debug.LogWarning("[TopdownPlayerMotor] useCameraRelativeMove 为 true，但 moveReference 未设置，已自动改为 false。", this);
        }

        // 防止 CC 缩放导致奇怪结果（官方也不推荐缩放 CharacterController）
        if (transform.lossyScale != Vector3.one)
        {
            Debug.LogWarning("[TopdownPlayerMotor] CharacterController 不建议跟随缩放。建议把模型作为子物体缩放。", this);
        }
    }

    private void Update()
    {
        ReadInput();
        UpdateDashState();

        // Ground/Gravity 放在 Update：保持同一 tick 内一致（CharacterController 通常推荐这样）
        isGrounded = CheckGrounded();
        ApplyGravity(Time.deltaTime);

        // 计算水平速度
        Vector3 desiredPlanarVel = GetDesiredPlanarVelocity();
        planarVelocity = desiredPlanarVel;

        // Dash 覆盖水平速度
        if (isDashing)
            planarVelocity = dashDirWorld * dashSpeed;

        // 合成移动
        Vector3 velocity = planarVelocity + Vector3.down * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);

        HandleRotation();

        DebugStateChanges();
    }

    private void OnGUI()
    {
        if (!debugOverlay) return;

        GUILayout.BeginArea(new Rect(10, 10, 460, 220), GUI.skin.box);
        GUILayout.Label($"[TopdownPlayerMotor]");
        GUILayout.Label($"Grounded: {isGrounded} | Dashing: {isDashing}");
        GUILayout.Label($"MoveDir: {moveDirWorld} (mag={moveDirWorld.magnitude:0.00})");
        GUILayout.Label($"PlanarVel: {planarVelocity} (mag={planarVelocity.magnitude:0.00})");
        GUILayout.Label($"VerticalVel: {verticalVelocity:0.00}");
        float cdLeft = Mathf.Max(0f, dashCooldown - (Time.time - lastDashTime));
        GUILayout.Label($"DashTimer: {dashTimer:0.00} | DashCD left: {cdLeft:0.00}");
        GUILayout.EndArea();
    }

    // ----------------------------
    // Input
    // ----------------------------
    private void ReadInput()
    {
        Vector2 input = controls.MoveInput(); // WASD / stick
        Vector3 raw = new Vector3(input.x, 0f, input.y);
        if (raw.sqrMagnitude > 1f) raw.Normalize();

        if (useCameraRelativeMove && moveReference != null)
        {
            Vector3 camF = moveReference.transform.forward;
            Vector3 camR = moveReference.transform.right;
            camF.y = 0f; camR.y = 0f;
            camF.Normalize(); camR.Normalize();

            moveDirWorld = (camR * raw.x + camF * raw.z);
        }
        else
        {
            moveDirWorld = raw;
        }
    }

    // ----------------------------
    // Movement
    // ----------------------------
    private Vector3 GetDesiredPlanarVelocity()
    {
        // Dash 中不看输入（保持锁定）
        if (isDashing) return Vector3.zero;

        // 普通移动
        return moveDirWorld * moveSpeed;
    }

    // ----------------------------
    // Rotation
    // ----------------------------
    private void HandleRotation()
    {
        // Dash 期间：你想要“冲刺时仍转向”也可以改这里
        if (isDashing) return;

        if (moveDirWorld.sqrMagnitude < 0.01f) return;

        Quaternion target = Quaternion.LookRotation(moveDirWorld, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * rotateSpeed);
    }

    // ----------------------------
    // Ground / Gravity
    // ----------------------------
    private bool CheckGrounded()
    {
        // 用 CharacterController 的几何参数算射线点（更稳）
        // 计算“脚底中心点”
        Vector3 worldCenter = transform.TransformPoint(controller.center);
        float halfHeight = Mathf.Max(0f, controller.height * 0.5f - controller.radius);
        Vector3 foot = worldCenter + Vector3.down * halfHeight;

        float rayLen = controller.radius + groundRayExtra;

        // 中心射线
        if (RaycastDown(foot, rayLen)) return true;

        // 4个边缘点（可按需改成 8 个）
        float r = controller.radius * 0.9f;
        Vector3 right = transform.right * r;
        Vector3 forward = transform.forward * r;

        if (RaycastDown(foot + right, rayLen)) return true;
        if (RaycastDown(foot - right, rayLen)) return true;
        if (RaycastDown(foot + forward, rayLen)) return true;
        if (RaycastDown(foot - forward, rayLen)) return true;

        return false;
    }

    private bool RaycastDown(Vector3 origin, float length)
    {
        bool hit = Physics.Raycast(origin, Vector3.down, out RaycastHit _, length, groundMask, QueryTriggerInteraction.Ignore);

        if (debugDrawRays)
        {
            Debug.DrawRay(origin, Vector3.down * length, hit ? Color.green : Color.red);
        }

        return hit;
    }

    private void ApplyGravity(float dt)
    {
        if (isGrounded)
        {
            // 贴地：给一个很小的向下速度（防止“isGrounded 抖动”）
            verticalVelocity = groundStickVelocity;
            return;
        }

        // 不在地面：加速下落（verticalVelocity 用“速度”，这里用正数表示“向下速度”）
        verticalVelocity += gravity * dt;
        if (verticalVelocity > fallSpeedMax) verticalVelocity = fallSpeedMax;
    }

    // ----------------------------
    // Dash
    // ----------------------------
    private void UpdateDashState()
    {
        // 先更新 dash 计时
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
                isDashing = false;
        }

        // 再检测是否触发 dash
        if (controls.DashTriggered())
            TryStartDash();
    }

    private void TryStartDash()
    {
        if (isDashing) return;
        if ((Time.time - lastDashTime) < dashCooldown) return;

        // 没方向不 dash（你也可以允许“原地 dash”，那就去掉）
        if (moveDirWorld.sqrMagnitude < 0.01f && !dashLockToFacing) return;

        isDashing = true;
        dashTimer = dashDuration;
        lastDashTime = Time.time;

        // 锁定 dash 方向
        dashDirWorld = dashLockToFacing ? transform.forward : moveDirWorld.normalized;

        // Dash 开始时，通常建议清掉垂直速度，避免冲刺时突然下坠/弹起
        verticalVelocity = groundStickVelocity;
    }

    // ----------------------------
    // Debug
    // ----------------------------
    private void DebugStateChanges()
    {
        if (!debugLogStateChanges) return;

        if (prevGrounded != isGrounded)
        {
            Debug.Log($"[TopdownPlayerMotor] Grounded changed: {prevGrounded} -> {isGrounded}", this);
            prevGrounded = isGrounded;
        }

        if (prevDashing != isDashing)
        {
            Debug.Log($"[TopdownPlayerMotor] Dashing changed: {prevDashing} -> {isDashing}", this);
            prevDashing = isDashing;
        }
    }

    // ----------------------------
    // Utility
    // ----------------------------
    public void TeleportToPosition(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
    }
}
