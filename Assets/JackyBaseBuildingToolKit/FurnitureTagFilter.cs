using JackyUtility;

/// <summary>
/// Filters inventory items by the FurnitureTag of their linked BuildableProperty.
/// Items without a BuildableKey never pass.
/// </summary>
public class FurnitureTagFilter : IContainerFilter<Key_ItemDefinitionPP>
{
    private readonly FurnitureTag requiredTag;

    public FurnitureTagFilter(FurnitureTag requiredTag)
    {
        this.requiredTag = requiredTag;
    }

    public bool Matches(Key_ItemDefinitionPP itemKey)
    {
        PropertyDatabaseManager databaseManager = PropertyDatabaseManager.Instance;
        if (databaseManager == null)
            return false;

        ItemDefinitionDatabase itemDatabase = databaseManager.GetDatabase<ItemDefinitionDatabase>();
        BuildableDatabase buildableDatabase = databaseManager.GetDatabase<BuildableDatabase>();
        if (itemDatabase == null || buildableDatabase == null)
            return false;

        ItemDefinitionSO item = itemDatabase.GetByEnum(itemKey);
        if (item == null || !item.IsBuildable)
            return false;

        if (requiredTag == FurnitureTag.None)
            return true;

        BuildableProperty buildable = buildableDatabase.GetByEnum(item.BuildableKey);
        return buildable != null && (buildable.furnitureTags & requiredTag) != 0;
    }
}