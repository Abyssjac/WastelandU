using UnityEngine;

public enum IslandTravelAction
{
    EnterCurrentIsland = 0,
    ReturnToMainWorld = 1,
}

[DisallowMultipleComponent]
public class IslandTravelInteractable : BaseInteractable
{
    [SerializeField] private IslandTravelAction travelAction = IslandTravelAction.EnterCurrentIsland;

    public override bool CanInteract
    {
        get
        {
            IslandTravelManager manager = IslandTravelManager.Instance;
            if (manager == null)
                return false;

            return travelAction == IslandTravelAction.EnterCurrentIsland
                ? manager.CanEnterCurrentDockedIsland(out _)
                : manager.CanReturnToMainWorld(out _);
        }
    }

    public override void Interact(InteractorTargetDetector caller)
    {
        IslandTravelManager manager = IslandTravelManager.Instance;
        bool requested = manager != null && (travelAction == IslandTravelAction.EnterCurrentIsland
            ? manager.RequestEnterCurrentIsland()
            : manager.RequestReturnToMainWorld());

        caller?.EndInteraction();

        if (!requested)
            Debug.LogWarning($"[{nameof(IslandTravelInteractable)}] {travelAction} was unavailable.", this);
    }
}
