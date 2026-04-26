using System.Collections.Generic;
using UnityEngine;

// ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
//  Serializable pairing structs (shared with BossVisual)
// ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

[System.Serializable]
public struct FacingEnemyGrid
{
    public EnemyVisualFacing facing;
    public EnemyGridBehaviour gridBehaviour;
}

// ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
//  BossController
// ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

/// <summary>
/// Logic layer for the boss encounter.
///
/// Manages two sets of EnemyGridBehaviours:
///   - Combat grids  : the grids the player must fill to progress.
///   - Activate grids: display/region grids toggled by ActivateGridRegion().
///
/// Flow per facing completed:
///   1. Remove that facing from BossVisual's available pool.
///   2. Teleport the boss root to the next point for a (random) still-available facing.
///   3. Stable-animate the blocks for that facing.
///   4. Activate the grid region for the new facing.
/// </summary>
public class BossController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BossVisual bossVisual;

    [Header("Initial Facing")]
    [SerializeField] private EnemyVisualFacing initialFacing = EnemyVisualFacing.XPos;

    [Header("Combat Grids (player fills these)")]
    [SerializeField] private List<FacingEnemyGrid> combatGrids = new List<FacingEnemyGrid>();

    [Header("Activate Grids (toggled by ActivateGridRegion)")]
    [SerializeField] private List<FacingEnemyGrid> activateGrids = new List<FacingEnemyGrid>();

    // Runtime
    private EnemyVisualFacing currentFacing;

    // ©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Awake()
    {
        if (bossVisual == null)
            bossVisual = GetComponent<BossVisual>();
    }

    private void Start()
    {
        SubscribeAllCombatGrids();
        StartEncounter(initialFacing);
    }

    private void OnDestroy()
    {
        UnsubscribeAllCombatGrids();
    }

    // ©¤©¤ Subscriptions ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void SubscribeAllCombatGrids()
    {
        foreach (FacingEnemyGrid entry in combatGrids)
        {
            if (entry.gridBehaviour == null) continue;

            EnemyVisualFacing capturedFacing = entry.facing;
            entry.gridBehaviour.OnGridFulfilled += () => OnCombatGridFulfilled(capturedFacing);
        }
    }

    private void UnsubscribeAllCombatGrids()
    {
        // Lambdas can't be individually unsubscribed; rely on object destruction to clean up.
        // If longer lifetimes are needed, cache the delegates instead.
    }

    // ©¤©¤ Encounter flow ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void StartEncounter(EnemyVisualFacing facing)
    {
        currentFacing = facing;
        ActivateGridRegion(facing);
    }

    private void OnCombatGridFulfilled(EnemyVisualFacing fulfilledFacing)
    {
        if (bossVisual == null)
        {
            Debug.LogError("[BossController] bossVisual is null.", this);
            return;
        }

        // 1. Remove this facing from the available teleport pool
        bossVisual.RemoveFacing(fulfilledFacing);

        // Stable-animate the blocks for the just-completed facing
        bossVisual.StableEnemyBlock(fulfilledFacing);

        // 2. Pick next facing from whatever is still available
        List<EnemyVisualFacing> remaining = bossVisual.GetAvailableFacings();

        if (remaining.Count == 0)
        {
            Debug.Log("[BossController] All facings completed ¡ª boss defeated.", this);
            OnAllFacingsCleared();
            return;
        }

        // Pick randomly from remaining available facings
        EnemyVisualFacing nextFacing = remaining[Random.Range(0, remaining.Count)];
        currentFacing = nextFacing;

        // 3. Teleport boss root to the next queued point for the new facing
        Debug.Log($"[BossController] Teleporting to next facing {nextFacing}. Remaining facings: {string.Join(", ", remaining)}", this);
        bossVisual.TeleportEnemyRoot(nextFacing);

        // 4. Activate the grid region for the new facing
        ActivateGridRegion(nextFacing);
    }

    // ©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// SetActive(true) every activate-grid GameObject whose facing matches.
    /// Other facings are NOT deactivated ¡ª multiple regions can be active simultaneously.
    /// </summary>
    public void ActivateGridRegion(EnemyVisualFacing facing)
    {
        bool found = false;
        foreach (FacingEnemyGrid entry in activateGrids)
        {
            if (entry.facing != facing) continue;
            if (entry.gridBehaviour == null) continue;
            entry.gridBehaviour.gameObject.SetActive(true);
            found = true;
        }

        if (!found)
            Debug.LogWarning($"[BossController] ActivateGridRegion: no activate-grid found for facing {facing}.", this);
    }

    // ©¤©¤ Extension point ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>Called when every facing has been fulfilled. Override or extend for win logic.</summary>
    protected virtual void OnAllFacingsCleared()
    {
        // Default: nothing. Hook in your win sequence here.
    }
}
