using System;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives the boss-defeat cinematic sequence in six ordered steps:
///
///   1. Destroy all placed buildables on a target <see cref="EnemyGridBehaviour"/>.
///   2. Trigger a GridPuzzle Animator clip and wait for it to finish.
///   3. Trigger TriggerStableAnim() on each <see cref="UnstableObjBehaviour"/> in
///      <see cref="stableChain"/> one-by-one, waiting for each to complete before
///      starting the next.
///   4. Trigger TriggerGlitch() on the boss a random number of times, waiting for
///      each teleport to complete before the next glitch.
///   5. Override the boss teleport destination with <see cref="bossFinalDestination"/>
///      and trigger one final glitch ¡ú teleport.
///   6. Call TriggerStableAnim() on the boss and wait for it to finish.
///
/// Subscribe to <see cref="BossController.OnBossDefeated"/> to start automatically,
/// or call <see cref="StartSequence"/> manually.
/// </summary>
public class BossDefeatSequencer : MonoBehaviour
{
    [Header("Trigger")]
    [Tooltip("Subscribes to OnBossDefeated to auto-start the sequence when the boss is defeated.")]
    [SerializeField] private BossController bossController;

    [Header("Cut-scene Camera")]
    [Tooltip("CameraCutScene to activate at the start of the sequence. Leave empty to skip.")]
    [SerializeField] private CameraCutScene cutSceneCamera;

    [Tooltip("Animator trigger name to set on the cut-scene camera.")]
    [SerializeField] private string cutSceneStateName = "todefeat";

    [Header("Step 1 ¡ª Clear Grid Objects")]
    [Tooltip("All placed buildables on this grid will be destroyed at the start of the sequence.")]
    [SerializeField] private EnemyGridBehaviour clearGrid;

    [Header("Step 2 ¡ª Grid Puzzle Animation")]
    [Tooltip("Animator that plays the grid-puzzle move clip.")]
    [SerializeField] private Animator gridPuzzleAnimator;

    [Tooltip("Animator trigger name to fire on the GridPuzzle Animator.")]
    [SerializeField] private string gridPuzzleAnimTrigger = "Play";

    [Tooltip("Seconds to wait for the animation clip to finish before moving on.")]
    [SerializeField] private float gridPuzzleAnimDuration = 2f;

    [Header("Step 3 ¡ª Unstable Chain (sequential StableAnim)")]
    [Tooltip("UnstableObjBehaviours to stabilise one-by-one in order.")]
    [SerializeField] private List<UnstableObjBehaviour> stableChain = new List<UnstableObjBehaviour>();

    [Header("Step 4 ¡ª Boss Random Glitches")]
    [Tooltip("The boss UnstableObjBehaviour (controls glitch / teleport / stable).")]
    [SerializeField] private UnstableObjBehaviour bossUnstable;

    [Tooltip("Minimum number of random glitch+teleport cycles before the final settle.")]
    [SerializeField] private int minGlitchCount = 2;

    [Tooltip("Maximum number of random glitch+teleport cycles before the final settle.")]
    [SerializeField] private int maxGlitchCount = 4;

    [Header("Step 5 ¡ª Boss Final Destination")]
    [Tooltip("The boss is force-teleported to this transform after the random glitch phase.")]
    [SerializeField] private Transform bossFinalDestination;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Start()
    {
        if (bossController != null)
            bossController.OnBossDefeated += OnBossDefeated;
    }

    private void OnDestroy()
    {
        if (bossController != null)
            bossController.OnBossDefeated -= OnBossDefeated;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Entry points ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void OnBossDefeated()
    {
        StartSequence();
    }

    /// <summary>
    /// Manually start the defeat sequence. Safe to call even without a BossController reference.
    /// </summary>
    [ContextMenu("Start Sequence (Debug)")]
    public void StartSequence()
    {
        StartCoroutine(RunSequence());
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Sequence Coroutine ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private IEnumerator RunSequence()
    {
        Log("Defeat sequence started.");

        // ©¤©¤ Cut-scene camera (fire-and-forget, runs in parallel with the sequence) ©¤©¤
        if (cutSceneCamera != null && !string.IsNullOrEmpty(cutSceneStateName))
        {
            Log($"Playing cut-scene state '{cutSceneStateName}'.");
            cutSceneCamera.Play(cutSceneStateName);
        }

        // ©¤©¤ Step 1: Clear all placed objects from the target grid ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        if (clearGrid != null && clearGrid.Grid != null)
        {
            // Snapshot IDs first to avoid modifying the collection while iterating
            List<string> ids = new List<string>(clearGrid.Grid.AllPlaced.Keys);
            Log($"Step 1: Clearing {ids.Count} placed object(s) from '{clearGrid.name}'.");

            foreach (string id in ids)
                clearGrid.TryRemove(id);

            Log("Step 1: Grid cleared.");
        }
        else
        {
            Log("Step 1: No clearGrid assigned ¡ª skipping grid clear.");
        }

        // Also destroy every detached (floating) buildable still alive in the scene
        BuildableBehaviour[] allBehaviours = FindObjectsOfType<BuildableBehaviour>();
        //allBehaviours = Object.FindObjectsOfType<BuildableBehaviour>();
        int detachedCount = 0;
        foreach (BuildableBehaviour bb in allBehaviours)
        {
            if (!bb.IsDetached) continue;

            // Stop any running DOTween animations cleanly before destroying
            UnstableObjBehaviour unstable = bb.GetComponent<UnstableObjBehaviour>();
            if (unstable != null) unstable.StopAnim();

            Destroy(bb.gameObject);
            detachedCount++;
        }
        Log($"Step 1: Destroyed {detachedCount} detached floating object(s).");

        // ©¤©¤ Step 2: Grid puzzle animation ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        if (gridPuzzleAnimator != null)
        {
            Log("Step 2: Triggering grid puzzle animation.");

            if (!string.IsNullOrEmpty(gridPuzzleAnimTrigger))
                gridPuzzleAnimator.SetTrigger(gridPuzzleAnimTrigger);

            yield return new WaitForSeconds(gridPuzzleAnimDuration);
            Log("Step 2: Grid puzzle animation complete.");
        }
        else
        {
            Log("Step 2: No GridPuzzle Animator assigned ¡ª skipping.");
        }

        // ©¤©¤ Step 3: Sequential StableAnim chain ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        Log($"Step 3: Starting stable chain ({stableChain.Count} object(s)).");

        for (int i = 0; i < stableChain.Count; i++)
        {
            UnstableObjBehaviour obj = stableChain[i];
            if (obj == null)
            {
                Log($"Step 3: Entry {i} is null ¡ª skipping.");
                continue;
            }

            Log($"Step 3: TriggerStableAnim on '{obj.name}' ({i + 1}/{stableChain.Count}).");

            bool done = false;
            Action onDone = () => done = true;
            obj.OnStableAnimCompleted += onDone;
            obj.TriggerStableAnim();
            yield return new WaitUntil(() => done);
            obj.OnStableAnimCompleted -= onDone;

            Log($"Step 3: '{obj.name}' stable complete.");
        }

        // ©¤©¤ Step 4 & 5: Boss glitch + final teleport ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        if (bossUnstable == null)
        {
            Log("Steps 4-6: bossUnstable not assigned ¡ª skipping boss sequence.");
            yield break;
        }

        int glitchCount = UnityEngine.Random.Range(minGlitchCount, maxGlitchCount + 1);
        Log($"Step 4: Boss will glitch {glitchCount} time(s).");

        for (int i = 0; i < glitchCount; i++)
        {
            yield return RunBossGlitch();
            Log($"Step 4: Boss glitch {i + 1}/{glitchCount} complete.");
        }

        // ©¤©¤ Step 5: Boss final teleport ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        if (bossFinalDestination != null)
        {
            Log("Step 5: Boss final teleport.");
            bossUnstable.SetTeleportOverride(bossFinalDestination);
            yield return RunBossGlitch();
            Log("Step 5: Boss arrived at final destination.");
        }
        else
        {
            Log("Step 5: No bossFinalDestination assigned ¡ª skipping final teleport.");
        }

        // ©¤©¤ Step 6: Boss stable anim ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        Log("Step 6: Boss TriggerStableAnim.");

        bool stableDone = false;
        Action onStable = () => stableDone = true;
        bossUnstable.OnStableAnimCompleted += onStable;
        bossUnstable.TriggerStableAnim();
        yield return new WaitUntil(() => stableDone);
        bossUnstable.OnStableAnimCompleted -= onStable;

        Log("Defeat sequence complete.");
    }

    /// <summary>
    /// Triggers one glitch on the boss and waits until the teleport finishes.
    /// </summary>
    private IEnumerator RunBossGlitch()
    {
        bool teleportDone = false;
        Action onTeleport = () => teleportDone = true;
        bossUnstable.OnTeleportCompleted += onTeleport;
        bossUnstable.TriggerGlitch();
        yield return new WaitUntil(() => teleportDone);
        bossUnstable.OnTeleportCompleted -= onTeleport;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Utility ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Log(string msg)
    {
        if (enableDebug)
            Debug.Log($"[BossDefeatSequencer] {msg}", this);
    }
}
