using JackyUtility;
using UnityEngine;

// ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤ Shared Enums / Action Classes ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

/// <summary>Whether an action fires when the player enters or exits the region.</summary>
public enum TriggerTiming
{
    OnEnter,
    OnExit,
}

/// <summary>Update the AllLevelManager respawn point to a new world position.</summary>
[System.Serializable]
public class RespawnUpdateAction
{
    public bool enabled = false;
    public TriggerTiming timing = TriggerTiming.OnEnter;
    [Tooltip("New respawn position. Taken from this Transform's world position at trigger time.")]
    public Transform respawnPoint;
}

/// <summary>Clear the player's WeaponBehaviour container.</summary>
[System.Serializable]
public class ClearWeaponContainerAction
{
    public bool enabled = false;
    public TriggerTiming timing = TriggerTiming.OnEnter;
}

/// <summary>Teleport the player to a target Transform.</summary>
[System.Serializable]
public class TeleportPlayerAction
{
    public bool enabled = false;
    public TriggerTiming timing = TriggerTiming.OnEnter;
    [Tooltip("World position (and optionally rotation) to teleport the player to.")]
    public Transform target;
    [Tooltip("If true, also match the player's rotation to the target transform.")]
    public bool matchRotation = false;
}

/// <summary>Send a signal through a SingleSignalInteractable.</summary>
[System.Serializable]
public class SendSignalAction
{
    public bool enabled = false;
    public TriggerTiming timing = TriggerTiming.OnEnter;
    public SingleSignalInteractable target;
}

// ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤ Main Component ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

/// <summary>
/// Place on a Trigger collider. Executes a configurable set of level-transition
/// actions (respawn update, clear weapons, teleport, send puzzle signal) when
/// an accepted tag enters or exits the region.
/// </summary>
public class TransitionRegionBehaviour : MonoBehaviour
{
    [Header("Tag Filter")]
    [Tooltip("Only objects with these tags trigger actions. Leave empty to accept any tag.")]
    [SerializeField] private string[] acceptedTags = new string[] { "Player" };

    [Header("Repeat")]
    [Tooltip("If true the region only fires once across its lifetime.")]
    [SerializeField] private bool triggerOnce = false;

    [Header("Actions")]
    [SerializeField] private RespawnUpdateAction respawnUpdate = new RespawnUpdateAction();
    [SerializeField] private ClearWeaponContainerAction clearWeapon = new ClearWeaponContainerAction();
    [SerializeField] private TeleportPlayerAction teleportPlayer = new TeleportPlayerAction();
    [SerializeField] private SendSignalAction sendSignal = new SendSignalAction();

    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;

    // Runtime
    private bool hasTriggered = false;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Trigger ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void OnTriggerEnter(Collider other)
    {
        if (!IsAcceptedTag(other.gameObject)) return;
        ExecuteActions(TriggerTiming.OnEnter, other.gameObject);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsAcceptedTag(other.gameObject)) return;
        ExecuteActions(TriggerTiming.OnExit, other.gameObject);
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Execution ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void ExecuteActions(TriggerTiming timing, GameObject instigator)
    {
        if (triggerOnce && hasTriggered) return;

        bool anyExecuted = false;

        if (respawnUpdate.enabled && respawnUpdate.timing == timing)
        {
            ExecuteRespawnUpdate();
            anyExecuted = true;
        }

        if (clearWeapon.enabled && clearWeapon.timing == timing)
        {
            ExecuteClearWeapon();
            anyExecuted = true;
        }

        if (teleportPlayer.enabled && teleportPlayer.timing == timing)
        {
            ExecuteTeleportPlayer();
            anyExecuted = true;
        }

        if (sendSignal.enabled && sendSignal.timing == timing)
        {
            ExecuteSendSignal();
            anyExecuted = true;
        }

        if (anyExecuted)
        {
            if (enableDebug)
                Debug.Log($"[TransitionRegion] '{name}' executed actions for timing={timing}, instigator='{instigator.name}'.", this);

            if (triggerOnce)
                hasTriggered = true;
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Individual Actions ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void ExecuteRespawnUpdate()
    {
        if (respawnUpdate.respawnPoint == null)
        {
            Debug.LogWarning($"[TransitionRegion] '{name}' RespawnUpdateAction: respawnPoint is not set.", this);
            return;
        }

        if (AllLevelManager.Instance == null)
        {
            Debug.LogError($"[TransitionRegion] '{name}' RespawnUpdateAction: AllLevelManager.Instance is null.", this);
            return;
        }

        AllLevelManager.Instance.SetRespawnPoint(respawnUpdate.respawnPoint.position);
    }

    private void ExecuteClearWeapon()
    {
        if (WeaponBehaviour.Instance == null)
        {
            Debug.LogError($"[TransitionRegion] '{name}' ClearWeaponContainerAction: WeaponBehaviour.Instance is null.", this);
            return;
        }

        WeaponBehaviour.Instance.container.ClearAll();
        WeaponBehaviour.Instance.RefreshSelectionFromContainer();

        if (enableDebug)
            Debug.Log($"[TransitionRegion] '{name}' Weapon container cleared.", this);
    }

    private void ExecuteTeleportPlayer()
    {
        if (teleportPlayer.target == null)
        {
            Debug.LogWarning($"[TransitionRegion] '{name}' TeleportPlayerAction: target is not set.", this);
            return;
        }

        if (PlayerMovementCC.Instance == null)
        {
            Debug.LogError($"[TransitionRegion] '{name}' TeleportPlayerAction: PlayerMovementCC.Instance is null.", this);
            return;
        }

        if (teleportPlayer.matchRotation)
            PlayerMovementCC.Instance.TeleportToPosition(teleportPlayer.target.position, teleportPlayer.target.rotation);
        else
            PlayerMovementCC.Instance.TeleportToPosition(teleportPlayer.target.position);

        if (enableDebug)
            Debug.Log($"[TransitionRegion] '{name}' Player teleported to '{teleportPlayer.target.name}'.", this);
    }

    private void ExecuteSendSignal()
    {
        if (sendSignal.target == null)
        {
            Debug.LogWarning($"[TransitionRegion] '{name}' SendSignalAction: target SingleSignalInteractable is not set.", this);
            return;
        }

        sendSignal.target.SendSingleSignal();

        if (enableDebug)
            Debug.Log($"[TransitionRegion] '{name}' Signal sent via '{sendSignal.target.name}'.", this);
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Utility ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private bool IsAcceptedTag(GameObject go)
    {
        if (acceptedTags == null || acceptedTags.Length == 0) return true;
        for (int i = 0; i < acceptedTags.Length; i++)
        {
            if (go.CompareTag(acceptedTags[i]))
                return true;
        }
        return false;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>Reset the trigger-once guard so the region can fire again.</summary>
    public void ResetTrigger() => hasTriggered = false;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!enableDebug) return;

        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        Matrix4x4 prev = Gizmos.matrix;
        Gizmos.matrix = box.transform.localToWorldMatrix;

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
        Gizmos.DrawCube(box.center, box.size);

        Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireCube(box.center, box.size);

        Gizmos.matrix = prev;

        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(transform.position, name);
    }
#endif
}
