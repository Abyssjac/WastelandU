using System;

/// <summary>
/// Legacy compatibility type for existing database assets.
/// New systems use <see cref="ItemDefinitionDatabase"/> directly.
/// </summary>
[Obsolete("Use ItemDefinitionDatabase for new item databases.")]
public class ContainerItemDatabase : ItemDefinitionDatabase
{
}