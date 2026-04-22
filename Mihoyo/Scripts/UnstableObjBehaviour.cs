using System.Collections.Generic;
using DG.Tweening;
using JackyUtility;
using UnityEngine;

public enum UnstableAnimState
{
    None = 0,
    Float = 1,
    Glitch = 2,
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
    private Vector3 glitchBaseLocalPos;

    private Tween activeTween;
    private Sequence glitchSequence;
    private Tween teleportDelayTween;

    private float stateElapsed;
    private float floatSinceLastGlitch;

    public UnstableAnimState CurrentAnimState { get; private set; } = UnstableAnimState.None;
    public float CurrentAnimElapsed => stateElapsed;

    private void Awake()
    {
        moveTarget = animatedTarget != null ? animatedTarget : transform;
    }

    private void Start()
    {
        centerLocalPos = moveTarget.localPosition;

        if (startWithFloatAnim && enableFloatAnim)
            StartFloat();
    }

    private void Update()
    {
        if (CurrentAnimState != UnstableAnimState.None)
            stateElapsed += Time.deltaTime;

        if (useDefaultUpdateLoop)
            DefaultUpdate();
    }

    public void DefaultUpdate()
    {
        if (CurrentAnimState != UnstableAnimState.Float) return;
        if (!enableGlitchAnim) return;

        floatSinceLastGlitch += Time.deltaTime;
        if (floatSinceLastGlitch < glitchInterval) return;

        floatSinceLastGlitch = 0f;
        TriggerGlitch();
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

        if (!TrySelectNextTeleportPoint(out nextTeleportPoint))
        {
            Debug.LogError($"[{name}] GlitchAnim aborted: no valid teleport point.", this);
            StopAnim();
            return;
        }

        StopAnim();
        CurrentAnimState = UnstableAnimState.Glitch;
        stateElapsed = 0f;

        glitchBaseLocalPos = moveTarget.localPosition;
        BuildAndPlayGlitchSequence();
    }

    public void StopAnim()
    {
        KillTweens();
        CurrentAnimState = UnstableAnimState.None;
        stateElapsed = 0f;
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
        if (nextTeleportPoint == null)
        {
            Debug.LogError($"[{name}] ExecuteTeleport failed: nextTeleportPoint is null.", this);
            StopAnim();
            return;
        }

        moveTarget.position = nextTeleportPoint.position;
        SetCenter(nextTeleportPoint.position);

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
            if ((p.position - currentPos).sqrMagnitude <= 0.000001f) continue;
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

        if (teleportDelayTween != null && teleportDelayTween.IsActive())
            teleportDelayTween.Kill();

        activeTween = null;
        glitchSequence = null;
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

        var panel = DebugGUIPanel.Begin(debugPanelPos, 460f, 12, 18f, 12);
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
        Gizmos.DrawLine(downMin, upMin);

        Vector3 worldFloatTarget = gizmoTarget.parent != null ? gizmoTarget.parent.TransformPoint(floatTargetLocalPos) : floatTargetLocalPos;
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(worldFloatTarget, 0.045f);

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
