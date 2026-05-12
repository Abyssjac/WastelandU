/// <summary>
/// Represents the current state of the player's interact system.
/// None        – no interactable object in range
/// HasTarget   – an interactable is detected and focused
/// Interacting – an interaction is in progress (e.g. a UI panel is open)
/// </summary>
public enum InteractState
{
    None = 0,
    HasTarget = 1,
    Interacting = 2,
}
