using UnityEngine;

/// <summary>
/// Abstract base class for all container-item actions.
/// Each concrete subclass represents a specific capability
/// (buildable, droppable, usable, etc.) and holds only the data
/// relevant to that capability.
/// <para>
/// These legacy ScriptableObjects are retained for existing assets.
/// New item behaviour is configured directly on <see cref="ItemDefinitionSO"/>.
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
