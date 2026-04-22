using UnityEngine;
using JackyPuzzleInteract;

/// <summary>
/// TwoSignalReceiver that drives a collection of UnstableObjBehaviour instances.
/// OnActivated  ¡ú triggers StableAnim on all targets.
/// OnDeactivated ¡ú triggers UnstableAnim on all targets.
/// </summary>
public class UnstableObjPuzzleReceiver : TwoSignalReceiver
{
    [Header("Targets")]
    [SerializeField] private UnstableObjBehaviour[] targets = new UnstableObjBehaviour[0];

    protected override void OnActivated(GameObject sender)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].TriggerStableAnim();
        }
    }

    protected override void OnDeactivated(GameObject sender)
    {
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i] != null)
                targets[i].TriggerUnstableAnim();
        }
    }
}
