using UnityEngine;

public class FlightVoyageVisualController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform shipAnchor;
    [SerializeField] private FlightDirectionMapper directionMapper = new FlightDirectionMapper();
    [SerializeField] private FlightAmbientPropController ambientPropController;
    [SerializeField] private FlightTargetIslandController targetIslandController;
    [SerializeField] private FlightShipVisualController shipVisualController;

    [Header("Visual Distance")]
    [SerializeField, Min(1f)] private float visualUnitsPerGrid = 300f;
    [SerializeField] private AnimationCurve visualProgressCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 1.1f),
        new Keyframe(1f, 1f, 0.25f, 0f));

    [Header("Arrival Drift")]
    [SerializeField, Min(0f)] private float arrivalDriftStartSpeed = 6f;
    [SerializeField, Min(0.01f)] private float arrivalDriftDuration = 1.2f;

    [Header("Voyage View")]
    [SerializeField] private KeyCode voyageViewToggleKey = KeyCode.Tab;
    [SerializeField] private CameraMode voyageViewCameraMode = CameraMode.Game_VoyageView;
    [SerializeField] private CameraMode playerViewCameraMode = CameraMode.FollowTarget;
    [SerializeField] private bool lockPlayerMovementInVoyageView = true;

    [Header("Debug")]
    [SerializeField] private bool drawDirectionGizmos = true;
    [SerializeField, Min(0.1f)] private float gizmoAxisLength = 25f;
    [SerializeField, Min(0.01f)] private float gizmoArrowHeadLength = 2f;
    [SerializeField, Min(0.01f)] private float gizmoArrowHeadWidth = 0.75f;

    private FlightVisualSegmentContext currentSegment;
    private float previousVisualDistance;
    private float arrivalDriftVelocity;
    private float arrivalDriftElapsed;
    private int currentTargetRuntimeId = -1;
    private int currentRouteIndex = -1;
    private FlightState previousFlightState = FlightState.Planning;
    private PlayerAgent movementLockedPlayer;

    private Transform ShipAnchor => shipAnchor != null ? shipAnchor : transform;
    public bool IsVoyageViewActive => AllCameraManager.Instance != null
        && AllCameraManager.Instance.CurrentCameraMode == voyageViewCameraMode;

    private void Update()
    {
        if (Input.GetKeyDown(voyageViewToggleKey))
            ToggleVoyageView();
    }

    private void OnDisable()
    {
        ReleaseVoyageViewMovementLock();
    }

    public bool ToggleVoyageView()
    {
        return IsVoyageViewActive ? ExitVoyageView() : EnterVoyageView();
    }

    public bool EnterVoyageView()
    {
        AllCameraManager cameraManager = AllCameraManager.Instance;
        if (cameraManager == null)
        {
            Debug.LogWarning($"[{nameof(FlightVoyageVisualController)}] Cannot switch voyage view because no {nameof(AllCameraManager)} exists.", this);
            return false;
        }

        if (!cameraManager.SwitchCameraMode(voyageViewCameraMode))
            return false;

        AcquireVoyageViewMovementLock();
        return true;
    }

    public bool ExitVoyageView()
    {
        AllCameraManager cameraManager = AllCameraManager.Instance;
        if (cameraManager == null)
        {
            Debug.LogWarning($"[{nameof(FlightVoyageVisualController)}] Cannot switch player view because no {nameof(AllCameraManager)} exists.", this);
            return false;
        }

        if (!cameraManager.SwitchCameraMode(playerViewCameraMode))
            return false;

        ReleaseVoyageViewMovementLock();
        return true;
    }

    private void LateUpdate()
    {
        FlightManager flightManager = FlightManager.Instance;
        if (flightManager == null)
            return;

        switch (flightManager.State)
        {
            case FlightState.Flying:
                TickFlying(flightManager);
                break;

            case FlightState.Arrived:
                TickArrived(flightManager);
                break;

            case FlightState.Planning:
                if (previousFlightState != FlightState.Planning)
                    ResetVisualState();
                break;
        }

        previousFlightState = flightManager.State;
    }

    private void TickFlying(FlightManager flightManager)
    {
        MapNodeRuntime targetNode = flightManager.GetCurrentTargetNode();
        FlightInfo flightInfo = flightManager.FlightInfo;
        if (targetNode == null || flightInfo == null)
            return;

        if (NeedsNewSegment(flightInfo.CurrentRouteIndex, targetNode.RuntimeId))
            BeginSegment(flightManager, targetNode);

        if (currentSegment == null)
            return;

        float logicalProgress = flightManager.SegmentProgress01;
        float visualProgress = EvaluateVisualProgress(logicalProgress);
        float visualDistance = currentSegment.VisualDistance * visualProgress;
        float scrollDelta = Mathf.Max(0f, visualDistance - previousVisualDistance);
        previousVisualDistance = visualDistance;

        FlightVisualFrameContext frameContext = CreateFrameContext(
            logicalProgress,
            visualProgress,
            scrollDelta,
            currentSegment.VisualDistance - visualDistance,
            true);

        ambientPropController?.Tick(frameContext);
        targetIslandController?.Tick(frameContext);
        shipVisualController?.Tick(frameContext);
    }

    private void TickArrived(FlightManager flightManager)
    {
        if (currentSegment == null)
            return;

        if (previousFlightState != FlightState.Arrived)
            CompleteArrival();

        if (arrivalDriftElapsed >= arrivalDriftDuration || arrivalDriftVelocity <= 0f)
            return;

        arrivalDriftElapsed += Time.deltaTime;
        float remainingRatio = 1f - Mathf.Clamp01(arrivalDriftElapsed / arrivalDriftDuration);
        float currentVelocity = arrivalDriftVelocity * remainingRatio;
        float scrollDelta = currentVelocity * Time.deltaTime;

        FlightVisualFrameContext frameContext = CreateFrameContext(
            1f,
            1f,
            scrollDelta,
            0f,
            false);

        ambientPropController?.Tick(frameContext);
        targetIslandController?.Tick(frameContext);
        shipVisualController?.Tick(frameContext);
    }

    private void BeginSegment(FlightManager flightManager, MapNodeRuntime targetNode)
    {
        FlightInfo flightInfo = flightManager.FlightInfo;
        Vector2Int startPosition = flightInfo.GetCurrentSegmentStartPosition();
        Vector2Int targetPosition = flightInfo.GetCurrentSegmentTargetPosition();
        Vector3 forward = directionMapper.GetWorldForward(startPosition, targetPosition);
        Vector3 right = directionMapper.GetWorldRight(forward);
        float mapDistance = flightManager.CalculateRouteDistance(startPosition, targetPosition);
        float visualDistance = mapDistance * visualUnitsPerGrid;
        int randomSeed = targetNode.RuntimeId * 397 ^ flightInfo.CurrentRouteIndex;

        currentSegment = new FlightVisualSegmentContext(
            startPosition,
            targetPosition,
            targetNode,
            flightInfo.CurrentRouteIndex,
            mapDistance,
            visualDistance,
            forward,
            right,
            randomSeed);

        currentTargetRuntimeId = targetNode.RuntimeId;
        currentRouteIndex = flightInfo.CurrentRouteIndex;
        previousVisualDistance = currentSegment.VisualDistance * EvaluateVisualProgress(flightManager.SegmentProgress01);
        arrivalDriftVelocity = 0f;
        arrivalDriftElapsed = 0f;

        ambientPropController?.BeginSegment(currentSegment);
        targetIslandController?.BeginSegment(currentSegment);
    }

    private void CompleteArrival()
    {
        float finalVisualDistance = currentSegment.VisualDistance;
        float finalScrollDelta = Mathf.Max(0f, finalVisualDistance - previousVisualDistance);
        previousVisualDistance = finalVisualDistance;

        FlightVisualFrameContext finalFrameContext = CreateFrameContext(
            1f,
            1f,
            finalScrollDelta,
            0f,
            false);

        ambientPropController?.Tick(finalFrameContext);
        targetIslandController?.Tick(finalFrameContext);
        targetIslandController?.OnArrived(finalFrameContext);
        shipVisualController?.Tick(finalFrameContext);

        float lastFrameSpeed = finalFrameContext.DeltaTime > Mathf.Epsilon
            ? finalScrollDelta / finalFrameContext.DeltaTime
            : 0f;
        arrivalDriftVelocity = Mathf.Max(arrivalDriftStartSpeed, lastFrameSpeed);
        arrivalDriftElapsed = 0f;
    }

    private FlightVisualFrameContext CreateFrameContext(
        float logicalProgress,
        float visualProgress,
        float scrollDelta,
        float remainingVisualDistance,
        bool allowsAmbientSpawning)
    {
        float deltaTime = Time.deltaTime;
        float scrollSpeed = deltaTime > Mathf.Epsilon ? scrollDelta / deltaTime : 0f;
        return new FlightVisualFrameContext(
            ShipAnchor.position,
            currentSegment.Forward,
            currentSegment.Right,
            scrollDelta,
            scrollSpeed,
            logicalProgress,
            visualProgress,
            remainingVisualDistance,
            deltaTime,
            allowsAmbientSpawning);
    }

    private bool NeedsNewSegment(int routeIndex, int targetRuntimeId)
    {
        return currentSegment == null
            || currentRouteIndex != routeIndex
            || currentTargetRuntimeId != targetRuntimeId;
    }

    private float EvaluateVisualProgress(float logicalProgress)
    {
        if (visualProgressCurve == null || visualProgressCurve.length == 0)
            return Mathf.Clamp01(logicalProgress);

        return Mathf.Clamp01(visualProgressCurve.Evaluate(Mathf.Clamp01(logicalProgress)));
    }

    private void ResetVisualState()
    {
        ambientPropController?.ReleaseAll();
        targetIslandController?.ReleaseAll();
        currentSegment = null;
        previousVisualDistance = 0f;
        arrivalDriftVelocity = 0f;
        arrivalDriftElapsed = 0f;
        currentTargetRuntimeId = -1;
        currentRouteIndex = -1;
    }

    private void AcquireVoyageViewMovementLock()
    {
        if (!lockPlayerMovementInVoyageView || movementLockedPlayer != null)
            return;

        PlayerManager playerManager = PlayerManager.Instance;
        if (playerManager == null || !playerManager.TryGetActivePlayer(out PlayerAgent activePlayer))
            return;

        if (activePlayer.AcquireMovementLock(this) || activePlayer.HasMovementLockOwner(this))
            movementLockedPlayer = activePlayer;
    }

    private void ReleaseVoyageViewMovementLock()
    {
        if (movementLockedPlayer == null)
            return;

        movementLockedPlayer.ReleaseMovementLock(this);
        movementLockedPlayer = null;
    }

    private void OnDrawGizmos()
    {
        if (!drawDirectionGizmos || currentSegment == null)
            return;

        Vector3 origin = ShipAnchor.position;
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(origin, gizmoAxisLength * 0.04f);

        DrawGizmoArrow(origin, currentSegment.Forward, gizmoAxisLength, Color.blue);
        DrawGizmoArrow(origin, currentSegment.Right, gizmoAxisLength, Color.red);
        DrawGizmoArrow(origin, Vector3.up, gizmoAxisLength, Color.green);
        Gizmos.color = Color.white;
    }

    private void DrawGizmoArrow(Vector3 origin, Vector3 direction, float length, Color color)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return;

        Vector3 normalizedDirection = direction.normalized;
        Vector3 tip = origin + normalizedDirection * length;
        Vector3 side = Vector3.Cross(normalizedDirection, Vector3.up);
        if (side.sqrMagnitude <= Mathf.Epsilon)
            side = Vector3.Cross(normalizedDirection, Vector3.forward);

        side.Normalize();
        Vector3 arrowBack = -normalizedDirection * gizmoArrowHeadLength;
        Vector3 arrowSide = side * gizmoArrowHeadWidth;

        Gizmos.color = color;
        Gizmos.DrawLine(origin, tip);
        Gizmos.DrawLine(tip, tip + arrowBack + arrowSide);
        Gizmos.DrawLine(tip, tip + arrowBack - arrowSide);
    }
}
