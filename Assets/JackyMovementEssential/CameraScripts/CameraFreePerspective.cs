
using UnityEngine;

public class CameraFreePerspective : CameraBase
{
    [Header("Drag Settings")]
    [Tooltip("鼠标中键拖拽灵敏度")]
    [SerializeField] private float dragSpeed = 0.5f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 2.5f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 20f;

    [Header("Reset Settings")]
    [Tooltip("按R回到初始位置的Lerp速度")]
    [SerializeField] private float resetLerpSpeed = 5f;

    [Header("Debug")]
    [SerializeField] private bool drawDebug = false;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float initialZoom;

    private bool isResetting;
    private Vector3 lastMousePos;

    protected override void Awake()
    {
        base.Awake();
        RecordInitialPose();
    }

    public override void ActivateCamera()
    {
        base.ActivateCamera();
        isResetting = false;
    }

    public override void DeactivateCamera()
    {
        base.DeactivateCamera();
        isResetting = false;
    }

    public override void Update()
    {
        // 1) Reset: 按R开始Lerp回初始位置
        if (Input.GetKeyDown(KeyCode.R))
        {
            isResetting = true;
        }

        if (isResetting)
        {
            float t = 1f - Mathf.Exp(-resetLerpSpeed * Time.deltaTime); // 帧率无关插值
            Vector3 lerpedPos = Vector3.Lerp(transform.position, initialPosition, t);
            lerpedPos.y = initialPosition.y; // 锁定Y轴
            transform.position = lerpedPos;
            transform.rotation = Quaternion.Slerp(transform.rotation, initialRotation, t);

            if (CachedCamera.orthographic)
            {
                CachedCamera.orthographicSize = Mathf.Lerp(CachedCamera.orthographicSize, initialZoom, t);
            }
            else
            {
                CachedCamera.fieldOfView = Mathf.Lerp(CachedCamera.fieldOfView, initialZoom, t);
            }

            // 到达目标后停止
            if (Vector3.Distance(transform.position, initialPosition) < 0.01f)
            {
                transform.position = initialPosition;
                transform.rotation = initialRotation;

                if (CachedCamera.orthographic)
                    CachedCamera.orthographicSize = initialZoom;
                else
                    CachedCamera.fieldOfView = initialZoom;

                isResetting = false;
            }

            return; // Reset期间不接受拖拽/缩放
        }

        // 2) 鼠标中键拖拽平移
        if (Input.GetMouseButtonDown(2))
        {
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButton(2))
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            lastMousePos = Input.mousePosition;

            // 将屏幕空间的拖拽映射到相机的本地right/up方向
            Vector3 move = -transform.right * delta.x * dragSpeed * Time.deltaTime
                           - transform.up * delta.y * dragSpeed * Time.deltaTime;

            move.y = 0f; // 锁定Y轴，不允许拖拽改变Y坐标

            transform.position += move;
        }

        // 3) 滚轮缩放
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.001f)
        {
            if (CachedCamera.orthographic)
            {
                CachedCamera.orthographicSize -= scroll * zoomSpeed;
                CachedCamera.orthographicSize = Mathf.Clamp(CachedCamera.orthographicSize, minZoom, maxZoom);
            }
            else
            {
                CachedCamera.fieldOfView -= scroll * zoomSpeed;
                CachedCamera.fieldOfView = Mathf.Clamp(CachedCamera.fieldOfView, minZoom, maxZoom);
            }
        }

        // 4) Debug
        if (drawDebug)
        {
            Debug.DrawRay(initialPosition, Vector3.up * 1f, Color.green);
            Debug.DrawLine(transform.position, initialPosition, Color.yellow);
        }
    }

    /// <summary>
    /// 记录当前位姿为"初始位置"，之后按R会回到这里
    /// </summary>
    public void RecordInitialPose()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;

        if (CachedCamera != null)
            initialZoom = CachedCamera.orthographic ? CachedCamera.orthographicSize : CachedCamera.fieldOfView;
    }
}