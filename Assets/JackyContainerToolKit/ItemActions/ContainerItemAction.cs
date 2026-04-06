using UnityEngine;

/// <summary>
/// Abstract base class for all container-item actions.
/// Each concrete subclass represents a specific capability
/// (buildable, droppable, usable, etc.) and holds only the data
/// relevant to that capability.
/// <para>
/// Attach instances of these ScriptableObjects to
/// <see cref="ContainerItemProperty.actions"/> to declare what
/// behaviours a given item supports.
/// </para>
/// </summary>
public abstract class ContainerItemAction : ScriptableObject
{
    /// <summary>
    /// Human-readable name shown on UI action buttons / tooltips.
    /// Subclasses override this to provide a fixed name for the action type.
    /// </summary>
    public abstract string ActionName { get; }
}
