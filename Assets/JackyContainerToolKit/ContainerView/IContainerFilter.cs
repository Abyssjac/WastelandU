/// <summary>
/// Predicate that decides whether an item key should be included in a
/// <see cref="FilteredContainerView{TEnum}"/>.
/// Implement this for every filtering scenario (e.g. tag-based, category-based).
/// </summary>
public interface IContainerFilter<TEnum> where TEnum : struct
{
    bool Matches(TEnum key);
}

/// <summary>
/// A no-op filter that always returns true.
/// Use this to show all items with no filtering applied.
/// </summary>
public class PassthroughFilter<TEnum> : IContainerFilter<TEnum> where TEnum : struct
{
    public bool Matches(TEnum key) => true;
}
