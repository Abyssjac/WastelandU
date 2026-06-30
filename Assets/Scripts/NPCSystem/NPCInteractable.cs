using UnityEngine;

/// <summary>
/// Attach to an NPC GameObject alongside <see cref="NPCBehaviour"/>.
/// When the player interacts, opens the <see cref="NPCPanelUI"/> singleton and
/// passes this NPC's key so the panel can display the correct data.
/// </summary>
[DisallowMultipleComponent]
public class NPCInteractable : BasePanelInteractable
{
    // ── Inspector ─────────────────────────────────────────────────

    [Header("NPC Identity")]
    [Tooltip("Must match the Key_NPC set on the NPCBehaviour component on this GameObject.")]
    private Key_NPC npcKey;

    // ── Runtime ───────────────────────────────────────────────────

    private NPCBehaviour _behaviour;

    // ─────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        _behaviour = GetComponent<NPCBehaviour>();

        if (_behaviour == null)
        {
            Debug.LogWarning($"[NPCInteractable] No NPCBehaviour found on '{gameObject.name}'.", this);
            npcKey = Key_NPC.None;
            return;
        }

        npcKey = _behaviour.NpcKey; // Auto-assign from NPCBehaviour to avoid mismatches in the inspector
    }

    // ─────────────────────────────────────────────────────────────
    // BasePanelInteractable
    // ─────────────────────────────────────────────────────────────

    protected override void OnOpenPanel()
    {
        NPCPanelUI panel = NPCPanelUI.Instance;

        if (panel == null)
        {
            Debug.LogWarning("[NPCInteractable] NPCPanelUI.Instance is null. " +
                             "Make sure an NPCPanelUI is present in the scene.", this);
            // Abort — release interaction immediately so the player is not stuck
            NotifyPanelClosed();
            return;
        }

        panel.OpenPanel(this);
        _panel = panel;
    }

    // ─────────────────────────────────────────────────────────────
    // Public API
    // ─────────────────────────────────────────────────────────────

    /// <summary>The NPC this interactable represents.</summary>
    public Key_NPC NpcKey => npcKey;

    /// <summary>The NPCBehaviour on the same GameObject, or null if not found.</summary>
    public NPCBehaviour Behaviour => _behaviour;
}
