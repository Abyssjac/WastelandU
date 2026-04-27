using System.Collections.Generic;
using UnityEngine;
using JackyUtility;

// ─────────────────────────────────────────────────────────
//  Serializable pairing structs (shared with BossVisual)
// ─────────────────────────────────────────────────────────

[System.Serializable]
public struct FacingEnemyGrid
{
    public EnemyVisualFacing facing;
    public EnemyGridBehaviour gridBehaviour;
}

// ─────────────────────────────────────────────────────────
//  BossState
// ─────────────────────────────────────────────────────────

public enum BossState
{
    InActive,
    Active,
    Defeated,
}

// ─────────────────────────────────────────────────────────
//  BossController
// ─────────────────────────────────────────────────────────

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
public class BossController : MonoBehaviour, IDebuggable
{
    [Header("References")]
    [SerializeField] private BossVisual bossVisual;

    [Header("Initial Facing")]
    [SerializeField] private EnemyVisualFacing initialFacing = EnemyVisualFacing.XPos;

    [Header("Combat Grids (player fills these)")]
    [SerializeField] private List<FacingEnemyGrid> combatGrids = new List<FacingEnemyGrid>();

    [Header("Activate Grids (toggled by ActivateGridRegion)")]
    [SerializeField] private List<FacingEnemyGrid> activateGrids = new List<FacingEnemyGrid>();

    [Header("Steal All Trigger")]
    [Tooltip("When any combat grid whose facing matches one of these entries is fully filled, StealAllBlocks is called on the steal skill.")]
    [SerializeField] private List<EnemyVisualFacing> stealAllTriggerFacings = new List<EnemyVisualFacing>();

    [Tooltip("The steal skill that will have StealAllBlocks() called when a trigger grid is fulfilled.")]
    [SerializeField] private BossBlockStealSkill stealSkill;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;

    // ── IDebuggable ────────────────────────────────────────
    public string DebugId => "bosscontroller";
    public bool DebugEnabled
    {
        get => enableDebug;
        set => enableDebug = value;
    }
    private EnemyVisualFacing currentFacing;
    private BossState currentState = BossState.InActive;

    /// <summary>Current state of the boss encounter.</summary>
    public BossState CurrentState => currentState;

    // ── Lifecycle ──────────────────────────────────────────

    private void Awake()
    {
        if (bossVisual == null)
            bossVisual = GetComponent<BossVisual>();
    }

    private void Start()
    {
        stealSkill?.DeactivateSkill();
        SubscribeAllCombatGrids();
        StartEncounter(initialFacing);
        DebugConsoleManager.Instance.RegisterDebugTarget(this);
    }

    private void OnDestroy()
    {
        UnsubscribeAllCombatGrids();
        if (DebugConsoleManager.Instance != null)
            DebugConsoleManager.Instance.UnregisterDebugTarget(this);
    }

    // ── Subscriptions ──────────────────────────────────────

    private void SubscribeAllCombatGrids()
    {
        foreach (FacingEnemyGrid entry in combatGrids)
        {
            if (entry.gridBehaviour == null) continue;

            EnemyVisualFacing capturedFacing = entry.facing;
            entry.gridBehaviour.OnGridFulfilled += () => OnCombatGridFulfilled(capturedFacing);

            if (stealSkill != null && stealAllTriggerFacings.Contains(capturedFacing))
                entry.gridBehaviour.OnGridFulfilled += () => OnStealTriggerGridFulfilled(capturedFacing);
        }
    }

    private void UnsubscribeAllCombatGrids()
    {
        // Lambdas can't be individually unsubscribed; rely on object destruction to clean up.
        // If longer lifetimes are needed, cache the delegates instead.
    }

    // ── Steal trigger ───────────────────────────────────────

    private void OnStealTriggerGridFulfilled(EnemyVisualFacing triggerFacing)
    {
        if (stealSkill == null) return;
        if (currentState != BossState.InActive) return;

        currentState = BossState.Active;
        Debug.Log($"[BossController] Boss activated by facing {triggerFacing}. State → Active.", this);

        stealSkill.ActivateSkill();
        stealSkill.StealAllBlocks();
    }

    // ── Encounter flow ─────────────────────────────────────

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
            Debug.Log("[BossController] All facings completed — boss defeated.", this);
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

    // ── Public API ─────────────────────────────────────────

    /// <summary>
    /// SetActive(true) every activate-grid GameObject whose facing matches.
    /// Other facings are NOT deactivated — multiple regions can be active simultaneously.
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

    // ── Extension point ────────────────────────────────────

    /// <summary>Called when every facing has been fulfilled. Override or extend for win logic.</summary>
    protected virtual void OnAllFacingsCleared()
    {
        currentState = BossState.Defeated;
        stealSkill?.DeactivateSkill();
        Debug.Log("[BossController] All facings cleared. State → Defeated.", this);
    }

    // ── Debug GUI ──────────────────────────────────────────

    private void OnGUI()
    {
        if (!enableDebug) return;

        // 固定行数 = 基础行 + 每个 combat grid 一行
        int combatLineCount = combatGrids != null ? combatGrids.Count : 0;
        int totalLines = 10 + combatLineCount;

        var panel = DebugGUIPanel.Begin(new Vector2(10f, 10f), 340f, totalLines);

        // ── Header ──
        panel.DrawLine("<b>── BossController ──</b>");
        panel.DrawLine($"State:           <b>{currentState}</b>");
        panel.DrawLine($"Current Facing:  <b>{currentFacing}</b>");
        panel.Space();

        // ── Combat Grids ──
        panel.DrawLine("<b>Combat Grids</b>");
        if (combatGrids != null)
        {
            foreach (FacingEnemyGrid entry in combatGrids)
            {
                bool fulfilled  = entry.gridBehaviour != null && entry.gridBehaviour.IsGridFulfilled;
                bool isTrigger  = stealAllTriggerFacings != null && stealAllTriggerFacings.Contains(entry.facing);
                string trigger  = isTrigger ? " <color=yellow>[trigger]</color>" : "";
                string status   = fulfilled  ? "<color=green>?</color>" : "<color=grey>○</color>";
                panel.DrawLine($"  {status} {entry.facing}{trigger}");
            }
        }
        panel.Space();

        // ── Steal Skill ──
        panel.DrawLine("<b>Steal Skill</b>");
        if (stealSkill != null)
        {
            string activeStr = stealSkill.IsSkillActive
                ? "<color=green>Active</color>"
                : "<color=grey>Inactive</color>";
            panel.DrawLine($"  Status: {activeStr}");
            panel.DrawLine($"  Timer:  {stealSkill.Timer:F1}s / {stealSkill.AttackInterval:F1}s");
        }
        else
        {
            panel.DrawLine("  <color=red>stealSkill not assigned</color>");
        }
        panel.Space();

        // ── Remaining Facings ──
        panel.DrawLine("<b>Remaining Facings</b>");
        List<EnemyVisualFacing> remaining = bossVisual != null
            ? bossVisual.GetAvailableFacings()
            : new List<EnemyVisualFacing>();
        string remainingStr = remaining.Count > 0
            ? string.Join(", ", remaining)
            : "<color=green>None (all cleared)</color>";
        panel.DrawLine($"  {remainingStr}");

        panel.End();
    }
}
