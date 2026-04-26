using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tags a facing direction for blocks and teleport points on the boss.
/// </summary>
public enum EnemyVisualFacing
{
    XPos,
    XNeg,
    YPos,
    YNeg,
    ZPos,
    ZNeg,
}

// ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
//  Serializable pairing structs
// ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

[System.Serializable]
public struct FacingUnstableObj
{
    public EnemyVisualFacing facing;
    public UnstableObjBehaviour unstableObj;
}

[System.Serializable]
public struct FacingTransform
{
    public EnemyVisualFacing facing;
    public Transform point;
}

// ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
//  BossVisual
// ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

/// <summary>
/// Visual layer for the boss.
/// Manages directional unstable block visuals and the boss root teleport logic.
/// </summary>
public class BossVisual : MonoBehaviour
{
    // ©¤©¤ Block visuals ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Header("Block Visuals (facing ¡ú UnstableObjBehaviour)")]
    [SerializeField] private List<FacingUnstableObj> facingBlocks = new List<FacingUnstableObj>();

    // ©¤©¤ Enemy root ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Header("Enemy Root")]
    [SerializeField] private UnstableObjBehaviour enemyRoot;

    [Header("Root Teleport Points (facing ¡ú Transform)")]
    [SerializeField] private List<FacingTransform> rootTeleportPoints = new List<FacingTransform>();

    [Header("Available Facings For Teleportation")]
    // Runtime: available facing list (shrinks as facings are removed)
    [SerializeField] private List<EnemyVisualFacing> availableFacings = new List<EnemyVisualFacing>();

    // Per-facing ordered queues so we advance through them in sequence
    private Dictionary<EnemyVisualFacing, Queue<Transform>> teleportQueues
        = new Dictionary<EnemyVisualFacing, Queue<Transform>>();

    // ©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Awake()
    {
        BuildQueues();
        //ResetAvailableFacings();
    }

    private void BuildQueues()
    {
        teleportQueues.Clear();

        // Build per-facing ordered queues from the inspector list (preserving list order)
        foreach (FacingTransform ft in rootTeleportPoints)
        {
            if (ft.point == null) continue;

            if (!teleportQueues.ContainsKey(ft.facing))
                teleportQueues[ft.facing] = new Queue<Transform>();

            teleportQueues[ft.facing].Enqueue(ft.point);
        }
    }

    private void ResetAvailableFacings()
    {
        availableFacings.Clear();
        foreach (EnemyVisualFacing facing in System.Enum.GetValues(typeof(EnemyVisualFacing)))
            availableFacings.Add(facing);
    }

    // ©¤©¤ Public API: blocks ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Trigger StableAnim on every UnstableObjBehaviour tagged with the given facing.
    /// </summary>
    public void StableEnemyBlock(EnemyVisualFacing facing)
    {
        bool found = false;
        foreach (FacingUnstableObj entry in facingBlocks)
        {
            if (entry.facing != facing) continue;
            if (entry.unstableObj == null) continue;
            entry.unstableObj.TriggerStableAnim();
            found = true;
        }

        if (!found)
            Debug.LogWarning($"[BossVisual] StableEnemyBlock: no blocks found for facing {facing}.", this);
    }

    // ©¤©¤ Public API: available facings ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>Remove a facing from the available teleport-point pool.</summary>
    public void RemoveFacing(EnemyVisualFacing facing)
    {
        if (!availableFacings.Remove(facing))
            Debug.LogWarning($"[BossVisual] RemoveFacing: facing {facing} was not in the available list.", this);
    }

    public bool HasAvailableFacing(EnemyVisualFacing facing) => availableFacings.Contains(facing);

    public List<EnemyVisualFacing> GetAvailableFacings() => new List<EnemyVisualFacing>(availableFacings);

    // ©¤©¤ Public API: root teleport ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Trigger the enemy root's glitch-then-teleport animation to the next queued
    /// transform for the given facing.
    /// </summary>
    public void TeleportEnemyRoot(EnemyVisualFacing facing)
    {
        if (enemyRoot == null)
        {
            Debug.LogError("[BossVisual] TeleportEnemyRoot: enemyRoot is not set.", this);
            return;
        }

        if (!teleportQueues.TryGetValue(facing, out Queue<Transform> queue) || queue.Count == 0)
        {
            Debug.LogError($"[BossVisual] TeleportEnemyRoot: no teleport points left for facing {facing}.", this);
            return;
        }

        Transform destination = queue.Dequeue();
        TeleportEnemyRoot(destination);
    }

    /// <summary>
    /// Trigger the enemy root's glitch-then-teleport animation to an explicit destination.
    /// </summary>
    public void TeleportEnemyRoot(Transform destination)
    {
        if (enemyRoot == null)
        {
            Debug.LogError("[BossVisual] TeleportEnemyRoot: enemyRoot is not set.", this);
            return;
        }

        if (destination == null)
        {
            Debug.LogError("[BossVisual] TeleportEnemyRoot: destination is null.", this);
            return;
        }

        // Inject the destination as a one-shot teleport point and trigger glitch
        Debug.Log($"[BossVisual] Teleporting enemy root to {destination.name} at position {destination.position}.", this);
        enemyRoot.SetTeleportOverride(destination);
        enemyRoot.TriggerGlitch();
    }
}
