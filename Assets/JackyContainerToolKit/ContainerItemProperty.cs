using System;

/// <summary>
/// Legacy compatibility type for existing ContainerItem assets.
/// New systems use <see cref="ItemDefinitionSO"/> directly.
/// </summary>
[Obsolete("Use ItemDefinitionSO for new item definitions.")]
public class ContainerItemProperty : ItemDefinitionSO
{
}