using System.Collections.Generic;
using DG.Tweening;
using JackyUtility;
using UnityEngine;

public enum UnstableAnimState
{
    None = 0,
    Float = 1,
    Glitch = 2,
    Stable = 3,
    Unstable = 4,
}

public class UnstableObjBehaviour : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform animatedTarget;

    [Header("Default Switches")]
    [SerializeField] private bool enableFloatAnim = true;
    [SerializeField] private bool enableGlitchAnim = true;
    [SerializeField] private bool startWithFloatAnim = true;

    [Header("Float Anim (Local Y)")]
    [SerializeField] private float minOffsetHeight = 0.15f;
    [SerializeField] private float maxOffsetHeight = 0.5f;
    [SerializeField] private float floatSegmentDuration = 0.6f;

    [Header("Glitch Anim")]
    [SerializeField] private float glitchTotalDuration = 1.2f;
    [SerializeField] private AnimationCurve glitchGrowthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float glitchMinStepDuration = 0.03f;
    [SerializeField] private float glitchMaxStepDuration = 0.16f;
    [SerializeField] private float glitchMaxAmplitude = 0.5f;
    [SerializeField] private float teleportDelay = 0f;
    [SerializeField] private Transform[] teleportPoints = new Transform[0];

    [Header("Stable / Unstable Anim")]
    [SerializeField] private Transform stableTargetTransform;
    [SerializeField] private Transform unstableTargetTransform;
    [SerializeField] private float stableAnimDuration = 0.8f;
    [SerializeField] private float unstableAnimDuration = 0.8f;
    [SerializeField] private Ease stableAnimEase = Ease.OutQuad;
    [SerializeField] private Ease unstableAnimEase = Ease.OutQuad;

    [Header("Default Loop")]
    [SerializeField] private bool useDefaultUpdateLoop = true;
    [SerializeField] private float glitchInterval = 4f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool debugGizmos = true;
    [SerializeField] private bool debugPanel = true;
    [SerializeField] private Vector2 debugPanelPos = new Vector2(10f, 180f);

    // Runtime
    private Transform moveTarget;
    private Vector3 centerLocalPos;
    private Vector3 floatTargetLocalPos;
    private Transform nextTeleportPoint;
    private Transform lastTeleportPoint;
    private Transform teleportOverride; // one-shot override set externally (e.g. BossVisual)
    private Vector3 glitchBaseLocalPos;

    private Tween activeTween;
    private Sequence glitchSequence;
    private Sequence repositionSequence;
    private Tween teleportDelayTween;

    private float stateElapsed;
    private float floatSinceLastGlitch;

    public UnstableAnimState CurrentAnimState { get; private set; } = UnstableAnimState.None;
    public float CurrentAnimElapsed => stateElapsed;

    // Set to true by Initialize() so Start() skips the auto-float until the coordinator is ready.
    private bool isRuntimeInitialized;

    private void Awake()
    {
        moveTarget = animatedTarget != null ? animatedTarget : transform;
    }

    private void Start()
    {
        if (isRuntimeInitialized) return; // coordinator will call StartFloat() manually

        centerLocalPos = moveTarget.localPosition;

        if (startWithFloatAnim && enableFloatAnim)
            StartFloat();
    }

    /// <summary>
    /// Runtime injection entry point. Called by UnstableGridBehaviourReceiver after
    /// BuildPreset has spawned this object, supplying all references that cannot be
    /// pre-assigned in the Inspector.
    /// </summary>
    public void Initialize(Transform[] runtimeTeleportPoints,
                           Transform runtimeStableTarget,
                           Transform runtimeUnstableTarget,
                           bool startUnstable = true)
    {
        isRuntimeInitialized = true;

        moveTarget = animatedTarget != null ? animatedTarget : transform;
        centerLocalPos = moveTarget.localPosition;

        if (runtimeTeleportPoints != null)
            teleportPoints = runtimeTeleportPoints;

        if (runtimeStableTarget != null)
            stableTargetTransform = runtimeStableTarget;

        if (runtimeUnstableTarget != null)
            unstableTargetTransform = runtimeUnstableTarget;

        if (debugLog)
            Debug.Log($"[{name}] UnstableObjBehaviour initialized at runtime. " +
                      $"teleportPoints={teleportPoints.Length}, " +
                      $"stableTarget={stableTargetTransform?.name}, " +
                      $"unstableTarget={unstableTargetTransform?.name}", this);

        if (startUnstable)
            TriggerUnstableAnim();
        else if (enableFloatAnim)
            StartFloat();
    }

    private void Update()
    {
        if (CurrentAnimState != UnstableAnimState.None)
            stateElapsed += Time.deltaTime;

        if (useDefaultUpdateLoop)
            TickDefaultLoop(Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.U)) { 
            TriggerStableAnim();
        }
        if (Input.GetKeyDown(KeyCode.I)) { 
            TriggerUnstableAnim();
        }
    }

    // Internal tick ¡ª called only from Update to avoid double-increment when DefaultUpdate is also called externally.
    private void TickDefaultLoop(float dt)
    {
        if (CurrentAnimState != UnstableAnimState.Float) return;
        if (!enableGlitchAnim) return;

        floatSinceLastGlitch += dt;
        if (floatSinceLastGlitch < glitchInterval) return;

        floatSinceLastGlitch = 0f;
        TriggerGlitch();
    }

    /// <summary>
    /// Manually step the default loop logic. When useDefaultUpdateLoop is true this is
    /// already called every frame internally; call this only when managing the loop yourself.
    /// </summary>
    public void DefaultUpdate()
    {
        TickDefaultLoop(Time.deltaTime);
    }

    public void StartFloat()
    {
        if (!enableFloatAnim)
        {
            if (debugLog) Debug.LogWarning($"[{name}] FloatAnim is disabled.", this);
            return;
        }

        StopAnim();
        CurrentAnimState = UnstableAnimState.Float;
        stateElapsed = 0f;

        PlayNextFloatSegment();
    }

    public void StopFloat()
    {
        if (CurrentAnimState == UnstableAnimState.Float)
            StopAnim();
    }

    public void TriggerGlitch()
    {
        if (!enableGlitchAnim)
        {
            if (debugLog) Debug.LogWarning($"[{name}] GlitchAnim is disabled.", this);
            return;
        }

        if (CurrentAnimState == UnstableAnimState.Glitch)
            Debug.LogError($"[{name}] TriggerGlitch called while already glitching. Will reset and replay.", this);

        // If a one-shot override has been set (e.g. from BossVisual.TeleportEnemyRoot),
        // skip the normal selection entirely ¡ª the override will be consumed in ExecuteTeleport.
        if (teleportOverride == null)
        {
            if (!TrySelectNextTeleportPoint(out nextTeleportPoint))
            {
                Debug.LogWarning($"[{name}] GlitchAnim aborted: no valid teleport point. Falling back to FloatAnim.", this);
                floatSinceLastGlitch = 0f;
                if (enableFloatAnim)
                    StartFloat();
                return;
            }
        }

        StopAnim();
        CurrentAnimState = UnstableAnimState.Glitch;
        stateElapsed = 0f;

        glitchBaseLocalPos = moveTarget.localPosition;
        BuildAndPlayGlitchSequence();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Stable Anim ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Move animatedTarget to stableTargetTransform (position + rotation).
    /// On arrival: stays at that position, no further behaviour.
    /// Ignored with an error if already in Stable or Unstable anim.
    /// </summary>
    public void TriggerStableAnim()
    {
        if (CurrentAnimState == UnstableAnimState.Stable || CurrentAnimState == UnstableAnimState.Unstable)
        {
            Debug.LogError($"[{name}] TriggerStableAnim ignored: already in {CurrentAnimState}.", this);
            return;
        }

        if (stableTargetTransform == null)
        {
            Debug.LogError($"[{name}] TriggerStableAnim aborted: stableTargetTransform is not set.", this);
            return;
        }

        PlayRepositionAnim(stableTargetTransform, stableAnimDuration, stableAnimEase,
            UnstableAnimState.Stable, onComplete: OnStableAnimComplete);
    }

    /// <summary>
    /// Move animatedTarget to unstableTargetTransform (position + rotation).
    /// On arrival: enables useDefaultUpdateLoop and resets the glitch interval timer,
    /// re-entering the Float ¡ú Glitch cycle.
    /// Ignored with an error if already in Stable or Unstable anim.
    /// </summary>
    public void TriggerUnstableAnim()
    {
        if (CurrentAnimState == UnstableAnimState.Stable || CurrentAnimState == UnstableAnimState.Unstable)
        {
            Debug.LogError($"[{name}] TriggerUnstableAnim ignored: already in {CurrentAnimState}.", this);
            return;
        }

        if (unstableTargetTransform == null)
        {
            Debug.LogError($"[{name}] TriggerUnstableAnim aborted: unstableTargetTransform is not set.", this);
            return;
        }

        PlayRepositionAnim(unstableTargetTransform, unstableAnimDuration, unstableAnimEase,
            UnstableAnimState.Unstable, onComplete: OnUnstableAnimComplete);
    }

    private void PlayRepositionAnim(Transform target, float duration, Ease ease,
        UnstableAnimState state, System.Action onComplete)
    {
        StopAnim();
        CurrentAnimState = state;
        stateElapsed = 0f;

        Vector3 targetLocalPos = moveTarget.parent != null
            ? moveTarget.parent.InverseTransformPoint(target.position)
            : target.position;

        Quaternion targetLocalRot = moveTarget.parent != null
            ? Quaternion.Inverse(moveTarget.parent.rotation) * target.rotation
            : target.rotation;

        float d = Mathf.Max(0.01f, duration);

        repositionSequence = DOTween.Sequence();
        repositionSequence.Join(moveTarget.DOLocalMove(targetLocalPos, d).SetEase(ease));
        repositionSequence.Join(moveTarget.DOLocalRotateQuaternion(targetLocalRot, d).SetEase(ease));
        repositionSequence.OnComplete(() =>
        {
            if (debugLog)
                Debug.Log($"[{name}] RepositionAnim complete ¡ú {state}.", this);
            onComplete?.Invoke();
        });
    }

    private void OnUnstableAnimComplete()
    {
        SetCenter(moveTarget.position);
        floatSinceLastGlitch = 0f;
        useDefaultUpdateLoop = true;

        if (enableFloatAnim)
            StartFloat();
        else
            CurrentAnimState = UnstableAnimState.None;
    }

    private void OnStableAnimComplete()
    {
        CurrentAnimState = UnstableAnimState.None;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Stop ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public void StopAnim()
    {
        KillTweens();
        CurrentAnimState = UnstableAnimState.None;
        stateElapsed = 0f;
        floatSinceLastGlitch = 0f;
    }

    public void StopAllAnims()
    {
        StopAnim();
    }

    public void SetCenter(Transform centerPoint)
    {
        if (centerPoint == null) return;
        SetCenter(centerPoint.position);
    }

    public void SetCenter(Vector3 worldPos)
    {
        centerLocalPos = moveTarget.parent != null
            ? moveTarget.parent.InverseTransformPoint(worldPos)
            : worldPos;
    }

    /// <summary>
    /// Set a one-shot teleport destination that overrides the normal teleport-point selection
    /// for the next Glitch cycle only. Cleared automatically after use.
    /// </summary>
    public void SetTeleportOverride(Transform destination)
    {
        teleportOverride = destination;
    }

    private void PlayNextFloatSegment()
    {
        if (CurrentAnimState != UnstableAnimState.Float) return;

        float curY = moveTarget.localPosition.y;
        float centerY = centerLocalPos.y;

        bool moveUp;
        if (Mathf.Abs(curY - centerY) <= 0.0001f)
            moveUp = true; // default from center: move up
        else
            moveUp = curY < centerY;

        float randomOffset = Random.Range(Mathf.Min(minOffsetHeight, maxOffsetHeight), Mathf.Max(minOffsetHeight, maxOffsetHeight));
        float targetY = centerY + (moveUp ? randomOffset : -randomOffset);

        floatTargetLocalPos = new Vector3(centerLocalPos.x, targetY, centerLocalPos.z);

        activeTween = moveTarget
            .DOLocalMove(floatTargetLocalPos, Mathf.Max(0.01f, floatSegmentDuration))
            .SetEase(Ease.InOutSine)
            .OnComplete(PlayNextFloatSegment);
    }

    private void BuildAndPlayGlitchSequence()
    {
        float total = Mathf.Max(0.01f, glitchTotalDuration);
        float minStep = Mathf.Max(0.005f, glitchMinStepDuration);
        float maxStep = Mathf.Max(minStep, glitchMaxStepDuration);

        glitchSequence = DOTween.Sequence();

        float t = 0f;
        while (t < total)
        {
            float normalized = Mathf.Clamp01(t / total);
            float growth = Mathf.Clamp01(glitchGrowthCurve.Evaluate(normalized));

            float stepDuration = Mathf.Lerp(maxStep, minStep, growth);
            if (t + stepDuration > total)
                stepDuration = total - t;

            float amp = glitchMaxAmplitude * growth;
            Vector3 jitterOffset = new Vector3(
                Random.Range(-amp, amp),
                Random.Range(-amp, amp),
                Random.Range(-amp, amp));

            glitchSequence.Append(
                moveTarget.DOLocalMove(glitchBaseLocalPos + jitterOffset, Mathf.Max(0.005f, stepDuration)).SetEase(Ease.Linear)
            );

            t += stepDuration;
        }

        glitchSequence.OnComplete(HandleGlitchComplete);
    }

    private void HandleGlitchComplete()
    {
        if (CurrentAnimState != UnstableAnimState.Glitch)
            return;

        if (teleportDelay <= 0f)
        {
            ExecuteTeleport();
            return;
        }

        teleportDelayTween = DOVirtual.DelayedCall(teleportDelay, ExecuteTeleport);
    }

    private void ExecuteTeleport()
    {
        // One-shot override takes priority over the normally-selected teleport point
        if (teleportOverride != null)
        {
            nextTeleportPoint = teleportOverride;
            teleportOverride = null;
        }

        if (nextTeleportPoint == null)
        {
            Debug.LogError($"[{name}] ExecuteTeleport failed: nextTeleportPoint is null.", this);
            StopAnim();
            return;
        }

        moveTarget.position = nextTeleportPoint.position;
        SetCenter(nextTeleportPoint.position);
        lastTeleportPoint = nextTeleportPoint;

        if (debugLog)
            Debug.Log($"[{name}] Teleported to '{nextTeleportPoint.name}'.", this);

        if (enableFloatAnim)
        {
            floatSinceLastGlitch = 0f;
            StartFloat();
        }
        else
        {
            StopAnim();
        }
    }

    private bool TrySelectNextTeleportPoint(out Transform result)
    {
        result = null;

        if (teleportPoints == null || teleportPoints.Length == 0)
            return false;

        List<Transform> candidates = new List<Transform>(teleportPoints.Length);
        Vector3 currentPos = moveTarget.position;

        for (int i = 0; i < teleportPoints.Length; i++)
        {
            Transform p = teleportPoints[i];
            if (p == null) continue;
            if (p == lastTeleportPoint) continue;
            if ((p.position - currentPos).sqrMagnitude <= 1f) continue;
            candidates.Add(p);
        }

        if (candidates.Count == 0)
            return false;

        result = candidates[Random.Range(0, candidates.Count)];
        return true;
    }

    private void KillTweens()
    {
        if (activeTween != null && activeTween.IsActive())
            activeTween.Kill();

        if (glitchSequence != null && glitchSequence.IsActive())
            glitchSequence.Kill();

        if (repositionSequence != null && repositionSequence.IsActive())
            repositionSequence.Kill();

        if (teleportDelayTween != null && teleportDelayTween.IsActive())
            teleportDelayTween.Kill();

        activeTween = null;
        glitchSequence = null;
        repositionSequence = null;
        teleportDelayTween = null;
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    private void OnGUI()
    {
        if (!debugPanel) return;

        var panel = DebugGUIPanel.Begin(debugPanelPos, 460f, 14, 18f, 12);
        panel.DrawLine($"<b>[UnstableObj] {name}</b>");
        panel.DrawLine($"State: {CurrentAnimState}");
        panel.DrawLine($"State Elapsed: {stateElapsed:0.00}s");
        panel.DrawLine($"DefaultLoop: {useDefaultUpdateLoop} | Interval: {glitchInterval:0.00}s");
        panel.DrawLine($"FloatSinceLastGlitch: {floatSinceLastGlitch:0.00}s");
        panel.DrawLine($"Center(Local): {centerLocalPos}");
        panel.DrawLine($"Target(Local): {moveTarget.localPosition}");
        panel.DrawLine($"FloatTarget(Local): {floatTargetLocalPos}");
        panel.DrawLine($"GlitchTotal: {glitchTotalDuration:0.00}s | Delay: {teleportDelay:0.00}s");
        panel.DrawLine($"NextTeleport: {(nextTeleportPoint != null ? nextTeleportPoint.name : "None")}");
        panel.DrawLine($"StableTarget: {(stableTargetTransform != null ? stableTargetTransform.name : "None")}");
        panel.DrawLine($"UnstableTarget: {(unstableTargetTransform != null ? unstableTargetTransform.name : "None")}");
        panel.DrawLine($"EnableFloat: {enableFloatAnim} | EnableGlitch: {enableGlitchAnim}");
        panel.End();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!debugGizmos) return;

        Transform gizmoTarget = animatedTarget != null ? animatedTarget : transform;

        // Float gizmos
        Gizmos.color = Color.yellow;
        Vector3 worldCenter = gizmoTarget.parent != null ? gizmoTarget.parent.TransformPoint(centerLocalPos) : centerLocalPos;
        Gizmos.DrawSphere(worldCenter, 0.06f);

        float minAbs = Mathf.Min(minOffsetHeight, maxOffsetHeight);
        float maxAbs = Mathf.Max(minOffsetHeight, maxOffsetHeight);
        Vector3 upMin = worldCenter + Vector3.up * minAbs;
        Vector3 upMax = worldCenter + Vector3.up * maxAbs;
        Vector3 downMin = worldCenter - Vector3.up * minAbs;
        Vector3 downMax = worldCenter - Vector3.up * maxAbs;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.9f);
        Gizmos.DrawLine(downMax, upMax);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
        Gizmos.DrawLine(downMin, upMin);
        Gizmos.DrawLine(worldCenter + Vector3.up * minAbs, worldCenter + Vector3.up * maxAbs);
        Gizmos.DrawLine(worldCenter - Vector3.up * minAbs, worldCenter - Vector3.up * maxAbs);

        Vector3 worldFloatTarget = gizmoTarget.parent != null ? gizmoTarget.parent.TransformPoint(floatTargetLocalPos) : floatTargetLocalPos;
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(worldFloatTarget, 0.045f);

        // Stable / Unstable target gizmos
        if (stableTargetTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(stableTargetTransform.position, 0.1f);
            Gizmos.DrawLine(gizmoTarget.position, stableTargetTransform.position);
        }

        if (unstableTargetTransform != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(unstableTargetTransform.position, 0.1f);
            Gizmos.DrawLine(gizmoTarget.position, unstableTargetTransform.position);
        }

        // Teleport point gizmos (always visible)
        if (teleportPoints != null)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < teleportPoints.Length; i++)
            {
                if (teleportPoints[i] == null) continue;
                Gizmos.DrawWireSphere(teleportPoints[i].position, 0.1f);
                Gizmos.DrawLine(gizmoTarget.position, teleportPoints[i].position);
            }
        }

        // Glitch gizmos (only while glitching)
        if (CurrentAnimState == UnstableAnimState.Glitch)
        {
            Vector3 current = gizmoTarget.position;
            if (teleportPoints != null)
            {
                for (int i = 0; i < teleportPoints.Length; i++)
                {
                    if (teleportPoints[i] == null) continue;
                    Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.8f);
                    Gizmos.DrawLine(current, teleportPoints[i].position);
                }
            }

            if (nextTeleportPoint != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(current, nextTeleportPoint.position);
                Gizmos.DrawSphere(nextTeleportPoint.position, 0.08f);
            }
        }
    }
#endif
}
