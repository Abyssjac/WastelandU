using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Boss skill that periodically steals placed buildable blocks from an EnemyGridBehaviour.
///
/// Stolen blocks are:
///   1. Detached from the grid (slot freed, GO un-parented to scene root).
///   2. Given a temporary UnstableObjBehaviour that plays Glitch then Float.
///   3. Teleported to a random point inside the AABB defined by boundsMin / boundsMax.
///
/// While floating (UnstableAnimState.Float) the player can recycle the block back into
/// their weapon container by aiming at it in Recycle mode.
/// </summary>
public class BossBlockStealSkill : MonoBehaviour
{
    [Header("Target Grid")]
    [SerializeField] private EnemyGridBehaviour targetGrid;

    [Header("Steal Settings")]
    [Tooltip("How many blocks to steal per attack.")]
    [SerializeField] private int stealCount = 2;

    [Tooltip("Seconds between each steal attack.")]
    [SerializeField] private float attackInterval = 8f;

    [Header("Teleport Bounds")]
    [Tooltip("One corner of the AABB that stolen blocks teleport into.")]
    [SerializeField] private Transform boundsMin;

    [Tooltip("Opposite corner of the AABB.")]
    [SerializeField] private Transform boundsMax;

    [Header("Float Anim Settings (applied to stolen blocks)")]
    [SerializeField] private float floatMinOffset = 0.1f;
    [SerializeField] private float floatMaxOffset = 0.3f;
    [SerializeField] private float floatSegmentDuration = 0.8f;

    [Header("Glitch Anim Settings (applied to stolen blocks)")]
    [SerializeField] private float glitchTotalDuration = 1.0f;
    [SerializeField] private AnimationCurve glitchGrowthCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float glitchMinStep = 0.03f;
    [SerializeField] private float glitchMaxStep = 0.15f;
    [SerializeField] private float glitchAmplitude = 0.4f;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;

    // Runtime
    private float timer;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= attackInterval)
        {
            timer = 0f;
            ExecuteSteal();
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Immediately steals every stealable block from the target grid (no count cap).
    /// Intended to be triggered externally, e.g. by BossController when a designated
    /// combat grid is fully filled.
    /// </summary>
    public void StealAllBlocks()
    {
        if (targetGrid == null || targetGrid.Grid == null)
        {
            Debug.LogWarning("[BossBlockStealSkill] StealAllBlocks: targetGrid is not set or not initialized.", this);
            return;
        }

        if (boundsMin == null || boundsMax == null)
        {
            Debug.LogWarning("[BossBlockStealSkill] StealAllBlocks: boundsMin or boundsMax is not set.", this);
            return;
        }

        List<PlacedBuildableData> candidates = GatherStealCandidates();

        if (candidates.Count == 0)
        {
            if (enableDebug)
                Debug.Log("[BossBlockStealSkill] StealAllBlocks: no stealable blocks found.", this);
            return;
        }

        if (enableDebug)
            Debug.Log($"[BossBlockStealSkill] StealAllBlocks: stealing {candidates.Count} block(s).", this);

        for (int i = 0; i < candidates.Count; i++)
            StealBlock(candidates[i]);
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Core Steal Logic ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void ExecuteSteal()
    {
        if (targetGrid == null || targetGrid.Grid == null)
        {
            Debug.LogWarning("[BossBlockStealSkill] targetGrid is not set or not initialized.", this);
            return;
        }

        if (boundsMin == null || boundsMax == null)
        {
            Debug.LogWarning("[BossBlockStealSkill] boundsMin or boundsMax is not set.", this);
            return;
        }

        // Gather all placed blocks where canMove = true
        List<PlacedBuildableData> candidates = GatherStealCandidates();

        if (candidates.Count == 0)
        {
            if (enableDebug)
                Debug.Log("[BossBlockStealSkill] No stealable blocks found.", this);
            return;
        }

        // Shuffle and clamp to stealCount
        Shuffle(candidates);
        int toSteal = Mathf.Min(stealCount, candidates.Count);

        for (int i = 0; i < toSteal; i++)
            StealBlock(candidates[i]);
    }

    private List<PlacedBuildableData> GatherStealCandidates()
    {
        var results = new List<PlacedBuildableData>();

        foreach (var kvp in targetGrid.Grid.AllPlaced)
        {
            PlacedBuildableData data = kvp.Value;
            if (data == null || data.Property == null) continue;
            if (!data.Property.canMove) continue;
            if (data.SpawnedObject == null) continue;
            results.Add(data);
        }

        return results;
    }

    private void StealBlock(PlacedBuildableData data)
    {
        string instanceId = data.InstanceId;
        BuildableProperty prop = data.Property;

        // 1. Detach from grid (frees slot, un-parents GO, marks BuildableBehaviour.IsDetached)
        GameObject go = targetGrid.DetachFromGrid(instanceId);
        if (go == null)
        {
            Debug.LogWarning($"[BossBlockStealSkill] DetachFromGrid returned null for '{instanceId}'.", this);
            return;
        }

        if (enableDebug)
            Debug.Log($"[BossBlockStealSkill] Stole '{prop.EnumKey}' ({instanceId}).", this);

        // 2. Pick a random destination inside the AABB
        Vector3 destination = RandomPointInBounds();

        // Create a temporary transform at the destination for the teleport override
        GameObject destGO = new GameObject("[StealDest_Temp]");
        destGO.transform.position = destination;

        // 3. Set up UnstableObjBehaviour on the stolen block
        UnstableObjBehaviour unstable = go.GetComponent<UnstableObjBehaviour>();
        if (unstable == null)
            unstable = go.AddComponent<UnstableObjBehaviour>();

        ConfigureUnstable(unstable, go.transform, destGO.transform);

        // 4. Trigger Glitch (will use the override, then enter Float)
        unstable.SetTeleportOverride(destGO.transform);
        unstable.TriggerGlitch();

        // Clean up the temp dest GO after the glitch + teleport delay has passed
        float cleanupDelay = glitchTotalDuration + 0.5f;
        DOVirtual.DelayedCall(cleanupDelay, () =>
        {
            if (destGO != null)
                Destroy(destGO);
        });
    }

    private void ConfigureUnstable(UnstableObjBehaviour unstable, Transform self, Transform destTransform)
    {
        // Use reflection-free runtime configuration via Initialize()
        // animatedTarget = self, teleportPoints not needed (override is used),
        // stable/unstable targets not needed (no loop), no default update loop.
        unstable.Initialize(
            runtimeTeleportPoints: new Transform[] { destTransform },
            runtimeStableTarget: null,
            runtimeUnstableTarget: null,
            startUnstable: false   // we call TriggerGlitch manually
        );

        // Override individual anim parameters via public setters
        unstable.SetFloatParams(floatMinOffset, floatMaxOffset, floatSegmentDuration);
        unstable.SetGlitchParams(glitchTotalDuration, glitchGrowthCurve, glitchMinStep, glitchMaxStep, glitchAmplitude);
        unstable.SetLoopEnabled(false);
    }

    private Vector3 RandomPointInBounds()
    {
        Vector3 min = boundsMin.position;
        Vector3 max = boundsMax.position;

        return new Vector3(
            Random.Range(Mathf.Min(min.x, max.x), Mathf.Max(min.x, max.x)),
            Random.Range(Mathf.Min(min.y, max.y), Mathf.Max(min.y, max.y)),
            Random.Range(Mathf.Min(min.z, max.z), Mathf.Max(min.z, max.z))
        );
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (boundsMin == null || boundsMax == null) return;

        Vector3 min = boundsMin.position;
        Vector3 max = boundsMax.position;
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = new Vector3(
            Mathf.Abs(max.x - min.x),
            Mathf.Abs(max.y - min.y),
            Mathf.Abs(max.z - min.z));

        Gizmos.color = new Color(1f, 0.3f, 0f, 0.15f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.8f);
        Gizmos.DrawWireCube(center, size);
    }
#endif
}
