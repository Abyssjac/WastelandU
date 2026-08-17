using System;
using UnityEngine;

/// <summary>
/// Attach to the Player. Every frame performs a CapsuleCast in the player's
/// facing direction to detect the nearest <see cref="BaseInteractable"/>.
/// Manages the <see cref="InteractState"/> and fires <see cref="OnStateChanged"/>
/// whenever the state transitions.
/// </summary>
[DisallowMultipleComponent]
public class InteractorTargetDetector : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────

    [Header("Detection")]
    [Tooltip("Layer(s) that interactable objects live on.")]
    [SerializeField] private LayerMask interactableLayer;

    [Tooltip("How far forward the capsule cast reaches.")]
    [SerializeField] private float detectRange = 2f;

    [Tooltip("Radius of the capsule cast.")]
    [SerializeField] private float detectRadius = 0.5f;

    [Tooltip("Vertical offset from this transform's position for the capsule start point.")]
    [SerializeField] private float castHeightOffset = 1f;

    [Header("Input")]
    [Tooltip("Key used to trigger an interaction.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    [Header("Movement Lock")]
    [Tooltip("Acquire one PlayerManager movement lock for the whole Interacting state, then release it only when that state ends.")]
    [SerializeField] private bool lockPlayerMovementWhileInteracting = true;

    [Header("Debug")]
    [SerializeField] private bool debugDrawCast = true;

    // ── Events ────────────────────────────────────────────────────

    /// <summary>
    /// Fired whenever <see cref="CurrentState"/> changes.
    /// Parameters: (previousState, newState)
    /// </summary>
    public event Action<InteractState, InteractState> OnStateChanged;

    // ── Public Properties ─────────────────────────────────────────

    public InteractState CurrentState { get; private set; } = InteractState.None;

    /// <summary>Returns the interactable currently in focus, or null.</summary>
    public BaseInteractable GetCurrentInteractable() => _currentTarget;

    // ── Runtime ───────────────────────────────────────────────────

    private BaseInteractable _currentTarget;

    // Cached for the capsule cast
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[8];

    private PlayerAgent _movementLockedPlayer;

    // ─────────────────────────────────────────────────────────────
    // Unity
    // ─────────────────────────────────────────────────────────────

    private void Update()
    {
        if (CurrentState == InteractState.Interacting)
            CheckTargetInRange();
        else
            ScanForTarget();

        if (CurrentState == InteractState.HasTarget && Input.GetKeyDown(interactKey))
            TriggerInteract();
    }

    private void OnDisable()
    {
        ReleaseInteractionMovementLock();
    }

    private void OnDestroy()
    {
        ReleaseInteractionMovementLock();
    }

    // ─────────────────────────────────────────────────────────────
    // Detection
    // ─────────────────────────────────────────────────────────────

    // Returns the first valid BaseInteractable from the capsule cast, or null.
    private BaseInteractable PerformCapsuleCast()
    {
        Vector3 origin = transform.position + Vector3.up * castHeightOffset;
        Vector3 direction = transform.forward;

        // Two capsule end points separated by a small amount so it works as a sphere cast at short range
        Vector3 point1 = origin;
        Vector3 point2 = origin + Vector3.up * 0.01f;

        int hitCount = Physics.CapsuleCastNonAlloc(
            point1, point2, detectRadius, direction,
            _hitBuffer, detectRange, interactableLayer);

        for (int i = 0; i < hitCount; i++)
        {
            var interactable = _hitBuffer[i].collider.GetComponentInParent<BaseInteractable>();
            if (interactable != null && interactable.CanInteract)
                return interactable;
        }
        return null;
    }

    private void ScanForTarget()
    {
        BaseInteractable found = PerformCapsuleCast();
        if (found != _currentTarget)
            SetTarget(found);
    }

    /// <summary>
    /// Called every frame while <see cref="InteractState.Interacting"/>.
    /// If the first cast hit is no longer <see cref="_currentTarget"/>, the current interaction
    /// is forcibly ended via <see cref="EndInteraction"/>.
    /// If a different target is now first in range, it is immediately interacted with.
    /// </summary>
    private void CheckTargetInRange()
    {
        BaseInteractable found = PerformCapsuleCast();
        if (found == _currentTarget) return;

        EndInteraction();

        // A different target is now the first hit — immediately interact with it
        if (found != null && CurrentState == InteractState.HasTarget)
            TriggerInteract();
    }

    private void SetTarget(BaseInteractable newTarget)
    {
        // Unfocus old target
        if (_currentTarget != null)
            _currentTarget.OnUnfocused();

        _currentTarget = newTarget;

        if (_currentTarget != null)
        {
            _currentTarget.OnFocused();
            SetState(InteractState.HasTarget);
        }
        else
        {
            SetState(InteractState.None);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Interaction
    // ─────────────────────────────────────────────────────────────

    private void TriggerInteract()
    {
        if (_currentTarget == null) return;

        SetState(InteractState.Interacting);
        _currentTarget.Interact(this);
    }

    /// <summary>
    /// Call this from a <see cref="BaseInteractable"/> subclass (or from a UI panel)
    /// to signal that the current interaction has finished.
    /// </summary>
    public void EndInteraction()
    {
        if (CurrentState != InteractState.Interacting) return;

        if (_currentTarget != null)
            _currentTarget.OnInteractEnd();

        // Re-scan immediately to decide HasTarget or None
        ScanForTarget();

        // Keep the same valid target focused so immediately-repeatable interactions
        // can be triggered again without moving the player away and back.
        if (CurrentState == InteractState.Interacting)
            SetState(_currentTarget != null && _currentTarget.CanInteract
                ? InteractState.HasTarget
                : InteractState.None);
    }

    // ─────────────────────────────────────────────────────────────
    // State
    // ─────────────────────────────────────────────────────────────

    private void SetState(InteractState newState)
    {
        if (newState == CurrentState) return;
        var prev = CurrentState;
        CurrentState = newState;

        UpdateInteractionMovementLock(prev, newState);
        OnStateChanged?.Invoke(prev, newState);
    }

    private void UpdateInteractionMovementLock(InteractState previousState, InteractState nextState)
    {
        if (!lockPlayerMovementWhileInteracting)
        {
            ReleaseInteractionMovementLock();
            return;
        }

        if (previousState != InteractState.Interacting && nextState == InteractState.Interacting)
            AcquireInteractionMovementLock();
        else if (previousState == InteractState.Interacting && nextState != InteractState.Interacting)
            ReleaseInteractionMovementLock();
    }

    private void AcquireInteractionMovementLock()
    {
        if (_movementLockedPlayer != null)
            return;

        if (PlayerManager.Instance == null || !PlayerManager.Instance.TryGetActivePlayer(out PlayerAgent player))
        {
            Debug.LogWarning($"[{nameof(InteractorTargetDetector)}] Cannot lock movement because {nameof(PlayerManager)} has no active player.", this);
            return;
        }

        if (player.AcquireMovementLock(this))
            _movementLockedPlayer = player;
    }

    private void ReleaseInteractionMovementLock()
    {
        if (_movementLockedPlayer == null)
            return;

        _movementLockedPlayer.ReleaseMovementLock(this);
        _movementLockedPlayer = null;
    }

    // ─────────────────────────────────────────────────────────────
    // Editor Gizmos
    // ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    /// <summary>
    /// Draws the CapsuleCast volume in the Scene view whenever the GameObject is selected.
    /// Visible in both Edit Mode and Play Mode.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!debugDrawCast) return;

        Vector3 origin    = transform.position + Vector3.up * castHeightOffset;
        Vector3 direction = transform.forward;
        Vector3 castEnd   = origin + direction * detectRange;

        // Colour: green when a target is held at runtime, white otherwise
        Gizmos.color = (_currentTarget != null) ? new Color(0f, 1f, 0f, 0.6f)
                                                 : new Color(1f, 1f, 1f, 0.4f);

        // Draw sphere at cast origin
        Gizmos.DrawWireSphere(origin, detectRadius);

        // Draw sphere at cast end
        Gizmos.DrawWireSphere(castEnd, detectRadius);

        // Connect the two spheres with lines along the capsule edges
        Vector3 right = transform.right * detectRadius;
        Vector3 up    = transform.up    * detectRadius;

        Gizmos.DrawLine(origin + right,  castEnd + right);
        Gizmos.DrawLine(origin - right,  castEnd - right);
        Gizmos.DrawLine(origin + up,     castEnd + up);
        Gizmos.DrawLine(origin - up,     castEnd - up);

        // Centre line
        Gizmos.color = (_currentTarget != null) ? Color.green : Color.yellow;
        Gizmos.DrawLine(origin, castEnd);
    }
#endif
}
