using JackyUtility;

/// <summary>
/// Filters <see cref="Key_BuildablePP"/> items by <see cref="FurnitureTag"/> bit-mask.
/// An item passes if its <see cref="BuildableProperty.furnitureTags"/> has at least one
/// flag in common with the required tag.
///
/// Usage:
/// <code>
///   _view.SetFilter(new FurnitureTagFilter(FurnitureTag.Art));
/// </code>
/// </summary>
public class FurnitureTagFilter : IContainerFilter<Key_BuildablePP>
{
    private readonly FurnitureTag _requiredTag;

    public FurnitureTagFilter(FurnitureTag requiredTag)
    {
        _requiredTag = requiredTag;
    }

    public bool Matches(Key_BuildablePP key)
    {
        if (PropertyDatabaseManager.Instance == null) return false;

        BuildableDatabase db = PropertyDatabaseManager.Instance.GetDatabase<BuildableDatabase>();
        if (db == null) return false;

        BuildableProperty prop = db.GetByEnum(key);
        if (prop == null) return false;

        return (prop.furnitureTags & _requiredTag) != 0;
    }
}
