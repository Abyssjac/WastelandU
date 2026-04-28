using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Cut-scene camera that plays Animator states on demand.
///
/// Usage:
/// <code>
///     cutSceneCamera.Play("BossDefeatIntro");
/// </code>
///
/// When the clip finishes:
/// <list type="bullet">
///   <item><see cref="OnClipCompleted"/> is fired.</item>
///   <item><see cref="AllCameraManager"/> automatically switches back to the
///         camera mode that was active before <see cref="Play"/> was called.</item>
/// </list>
/// </summary>
[RequireComponent(typeof(Animator))]
public class CameraCutScene : CameraBase
{
    [Header("Cut-scene")]
    [Tooltip("Animator controlling this camera's clips. Auto-fetched if left empty.")]
    [SerializeField] private Animator cutSceneAnimator;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Fired when the currently playing clip reaches its end (normalizedTime >= 1).
    /// Unsubscribe inside the handler if you only want a one-shot callback.
    /// </summary>
    public event Action OnClipCompleted;

    /// <summary>
    /// Whether a clip is currently playing.
    /// </summary>
    public bool IsPlaying { get; private set; }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    protected override void Awake()
    {
        base.Awake();

        if (cutSceneAnimator == null)
            cutSceneAnimator = GetComponent<Animator>();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Playback ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Switch to <see cref="CameraMode.CutScene"/>, play the Animator state with the
    /// given name, and return immediately (non-blocking).
    /// Subscribe to <see cref="OnClipCompleted"/> or use <see cref="PlayAndWait"/>
    /// inside a Coroutine to sequence with other logic.
    /// </summary>
    /// <param name="stateName">
    /// Name of the Animator <b>Trigger</b> parameter to set.
    /// </param>
    public void Play(string stateName)
    {
        if (cutSceneAnimator == null)
        {
            Debug.LogError("[CameraCutScene] Animator is not assigned.", this);
            return;
        }

        if (IsPlaying)
        {
            StopAllCoroutines();
            IsPlaying = false;
        }

        AllCameraManager.Instance.SwitchToCameraMode(CameraMode.CutScene);
        cutSceneAnimator.SetTrigger(stateName);

        if (enableDebug)
            Debug.Log($"[CameraCutScene] Playing state '{stateName}'.", this);

        StartCoroutine(PollCompletion(stateName));
    }

    /// <summary>
    /// Coroutine-friendly version of <see cref="Play"/>.
    /// <c>yield return</c> this to pause the caller until the clip finishes.
    /// </summary>
    /// <example>
    /// <code>
    ///     yield return StartCoroutine(cutSceneCamera.PlayAndWait("BossDefeatIntro"));
    /// </code>
    /// </example>
    public IEnumerator PlayAndWait(string stateName)
    {
        bool done = false;
        Action onDone = () => done = true;
        OnClipCompleted += onDone;
        Play(stateName);
        yield return new WaitUntil(() => done);
        OnClipCompleted -= onDone;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Internal ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private IEnumerator PollCompletion(string triggerName)
    {
        IsPlaying = true;

        // Allow one frame for SetTrigger to be processed by the Animator
        yield return null;

        // Wait for the transition triggered by SetTrigger to BEGIN.
        // A short timeout handles the edge case of a zero-duration transition
        // (which starts and ends before IsInTransition can be detected).
        float waitTime = 0f;
        const float transitionStartTimeout = 0.15f;
        while (!cutSceneAnimator.IsInTransition(0) && waitTime < transitionStartTimeout)
        {
            waitTime += Time.deltaTime;
            yield return null;
        }

        // Wait for the transition to FINISH so normalizedTime reflects the destination state
        while (cutSceneAnimator.IsInTransition(0))
            yield return null;

        // Poll the destination state until it has played through once
        while (cutSceneAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
            yield return null;

        IsPlaying = false;

        if (enableDebug)
            Debug.Log($"[CameraCutScene] Trigger '{triggerName}' clip complete. Returning to previous camera.", this);

        OnClipCompleted?.Invoke();

        // Auto-restore the camera that was active before Play() was called
        AllCameraManager.Instance.SwitchToPreviousMode();
    }
}
