using UnityEngine;

/// <summary>
/// ������ TopdownPlayerMotor ��ȫһ�£���ʹ�� Rigidbody ���� CharacterController��
/// ������ Unity �������洦���������ֶ�ģ�⡣
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class TopdownPlayerMotorRB : MonoBehaviour
{
    // ----------------------------
    // References
    // ----------------------------
    private Rigidbody rb;
    private PlayerControl controls;

    // ----------------------------
    // Movement Settings
    // ----------------------------
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("ת��ƽ���ȣ�Խ��Խ�죩")]
    [SerializeField] private float rotateSpeed = 15f;

    [Tooltip("�Ƿ������������ƶ���2.5D ���������������������� WASD��")]
    [SerializeField] private bool useCameraRelativeMove = false;
    [SerializeField] private Transform moveReference;

    // ----------------------------
    // Ground Check
    // ----------------------------
    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask;

    [Tooltip("������������Խŵ׵�ƫ��")]
    [SerializeField] private float groundCheckOffset = 0.05f;

    [Tooltip("��������뾶")]
    [SerializeField] private float groundCheckRadius = 0.2f;

    // ----------------------------
    // Dash Settings
    // ----------------------------
    [Header("Dash")]
    [SerializeField] private float dashSpeed = 12f;
    [SerializeField] private float dashDuration = 0.15f;
    [SerializeField] private float dashCooldown = 1f;

    [Tooltip("Dash ����������ʽ��true=������ǰ�泯����false=������ǰ���뷽��")]
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
    private Vector3 moveDirWorld;        // ���뷽������ռ䣬y=0��
    private Vector3 planarVelocity;      // ˮƽ�ٶȣ�x,z��
    private bool isGrounded;

    // Dash runtime
    private bool isDashing;
    private float dashTimer;
    private float lastDashTime;
    private Vector3 dashDirWorld;

    // Debug cache��ֻ�ڱ仯ʱ log��
    private bool prevGrounded;
    private bool prevDashing;

    // ----------------------------
    // Collider cache (for ground check)
    // ----------------------------
    private Collider attachedCollider;

    // ----------------------------
    // Unity
    // ----------------------------
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        controls = GetComponent<PlayerControl>();

        // ���� Collider ���ڵ�����
        attachedCollider = GetComponent<Collider>();

        if (useCameraRelativeMove && moveReference == null)
        {
            Debug.LogWarning("[TopdownPlayerMotorRB] useCameraRelativeMove Ϊ true���� moveReference δ���ã����Զ���Ϊ false��", this);
        }

        // Rigidbody ���ý���
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // ��ת�ɽű�����
    }

    private void Update()
    {
        // ������ Update �ж�ȡ����֤��Ӧ��
        ReadInput();
        UpdateDashState();

        // ��ת�� Update �д�������ƽ����
        HandleRotation();

        DebugStateChanges();
    }

    private void FixedUpdate()
    {
        // ������
        isGrounded = CheckGrounded();

        // ����ˮƽ�ٶ�
        Vector3 desiredPlanarVel = GetDesiredPlanarVelocity();
        planarVelocity = desiredPlanarVel;

        // Dash ����ˮƽ�ٶ�
        if (isDashing)
            planarVelocity = dashDirWorld * dashSpeed;

        // ֻ�޸�ˮƽ���������� Rigidbody �����Ĵ�ֱ�ٶȣ��������������洦����
        Vector3 currentVel = rb.linearVelocity;
        Vector3 targetVel = new Vector3(planarVelocity.x, currentVel.y, planarVelocity.z);
        rb.linearVelocity = targetVel;
    }

    private void OnGUI()
    {
        if (!debugOverlay) return;

        GUILayout.BeginArea(new Rect(10, 10, 460, 220), GUI.skin.box);
        GUILayout.Label("[TopdownPlayerMotorRB]");
        GUILayout.Label($"Grounded: {isGrounded} | Dashing: {isDashing}");
        GUILayout.Label($"MoveDir: {moveDirWorld} (mag={moveDirWorld.magnitude:0.00})");
        GUILayout.Label($"PlanarVel: {planarVelocity} (mag={planarVelocity.magnitude:0.00})");
        GUILayout.Label($"RB Velocity: {rb.linearVelocity} (mag={rb.linearVelocity.magnitude:0.00})");
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
        // Dash �в������루����������
        if (isDashing) return Vector3.zero;

        // ��ͨ�ƶ�
        return moveDirWorld * moveSpeed;
    }

    // ----------------------------
    // Rotation
    // ----------------------------
    private void HandleRotation()
    {
        // Dash �ڼ䣺����Ҫ"���ʱ��ת��"Ҳ���Ը�����
        if (isDashing) return;

        if (moveDirWorld.sqrMagnitude < 0.01f) return;

        Quaternion target = Quaternion.LookRotation(moveDirWorld, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * rotateSpeed);
    }

    // ----------------------------
    // Ground Check
    // ----------------------------
    private bool CheckGrounded()
    {
        // ʹ�� OverlapSphere ���ŵ��Ƿ�Ӵ�����
        Vector3 checkPos = GetGroundCheckCenter();
        bool grounded = Physics.CheckSphere(checkPos, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

        if (debugDrawRays)
        {
            // ��һ���򵥵��������߸������ӻ�
            Debug.DrawRay(checkPos, Vector3.down * groundCheckRadius, grounded ? Color.green : Color.red);
        }

        return grounded;
    }

    private Vector3 GetGroundCheckCenter()
    {
        // ����� Collider�������ĵײ��������� transform.position
        if (attachedCollider != null)
        {
            Vector3 boundsMin = attachedCollider.bounds.min;
            return new Vector3(transform.position.x, boundsMin.y + groundCheckOffset, transform.position.z);
        }

        return transform.position + Vector3.down * groundCheckOffset;
    }

    private void OnDrawGizmosSelected()
    {
        // �� Scene ��ͼ�п��ӻ������ⷶΧ
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(GetGroundCheckCenter(), groundCheckRadius);
    }

    // ----------------------------
    // Dash
    // ----------------------------
    private void UpdateDashState()
    {
        // �ȸ��� dash ��ʱ
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
                isDashing = false;
        }

        // �ټ���Ƿ񴥷� dash
        if (controls.DashTriggered())
            TryStartDash();
    }

    private void TryStartDash()
    {
        if (isDashing) return;
        if ((Time.time - lastDashTime) < dashCooldown) return;

        // û���� dash����Ҳ��������"ԭ�� dash"���Ǿ�ȥ����
        if (moveDirWorld.sqrMagnitude < 0.01f && !dashLockToFacing) return;

        isDashing = true;
        dashTimer = dashDuration;
        lastDashTime = Time.time;

        // ���� dash ����
        dashDirWorld = dashLockToFacing ? transform.forward : moveDirWorld.normalized;

        // Dash ��ʼʱ�����ֱ�ٶȣ�������ʱͻȻ��׹/����
        Vector3 vel = rb.linearVelocity;
        vel.y = 0f;
        rb.linearVelocity = vel;
    }

    // ----------------------------
    // Debug
    // ----------------------------
    private void DebugStateChanges()
    {
        if (!debugLogStateChanges) return;

        if (prevGrounded != isGrounded)
        {
            Debug.Log($"[TopdownPlayerMotorRB] Grounded changed: {prevGrounded} -> {isGrounded}", this);
            prevGrounded = isGrounded;
        }

        if (prevDashing != isDashing)
        {
            Debug.Log($"[TopdownPlayerMotorRB] Dashing changed: {prevDashing} -> {isDashing}", this);
            prevDashing = isDashing;
        }
    }

    // ----------------------------
    // Utility
    // ----------------------------
    public void TeleportToPosition(Vector3 position)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.position = position;
        transform.position = position;
    }
}
