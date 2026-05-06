using System;
using System.Collections.Generic;

/// <summary>
/// Combines a <see cref="ContainerViewSource{TEnum}"/> with an
/// <see cref="IContainerFilter{TEnum}"/> to produce a filtered
/// <see cref="SlotDisplayData"/> array ready for <see cref="UI_Container.Refresh"/>.
///
/// The property resolver delegate converts a <typeparamref name="TEnum"/> key
/// into an <see cref="ISlotDisplayableProperty"/>, keeping this class
/// database-agnostic and fully reusable.
///
/// Usage pattern (Method B ¡ª owned directly by the business manager):
/// <code>
///   _source = new ContainerViewSource&lt;TEnum&gt;(myContainer);
///   _view   = new FilteredContainerView&lt;TEnum&gt;(_source, key => db.GetByEnum(key));
///   _view.OnViewChanged += RefreshUI;
/// </code>
/// </summary>
public class FilteredContainerView<TEnum> where TEnum : struct
{
    // ©¤©¤ State ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private readonly ContainerViewSource<TEnum>             _source;
    private readonly Func<TEnum, ISlotDisplayableProperty>  _propertyResolver;
    private          IContainerFilter<TEnum>                _filter;

    /// <summary>
    /// Ordered list of keys that passed the last filter evaluation.
    /// Index maps 1-to-1 with the <see cref="UI_ContainerSlot"/> index shown in the UI.
    /// </summary>
    private readonly List<TEnum> _filteredKeys = new List<TEnum>();

    // ©¤©¤ Events ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Fired when the filtered results change, either because the container
    /// changed or because a new filter was applied.
    /// Subscribe here to call <see cref="UI_Container.InitSlots"/> +
    /// <see cref="UI_Container.Refresh"/>.
    /// </summary>
    public event Action OnViewChanged;

    // ©¤©¤ Construction ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <param name="source">The observable container data source.</param>
    /// <param name="propertyResolver">
    ///   Delegate that maps a <typeparamref name="TEnum"/> key to an
    ///   <see cref="ISlotDisplayableProperty"/>. Typically a one-liner database lookup.
    /// </param>
    /// <param name="filter">
    ///   Initial filter. Pass <c>null</c> or a <see cref="PassthroughFilter{TEnum}"/> to show all.
    /// </param>
    public FilteredContainerView(
        ContainerViewSource<TEnum>            source,
        Func<TEnum, ISlotDisplayableProperty> propertyResolver,
        IContainerFilter<TEnum>               filter = null)
    {
        _source           = source;
        _propertyResolver = propertyResolver;
        _filter           = filter;

        _source.OnSourceChanged += HandleSourceChanged;
    }

    // ©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Replace the active filter and immediately fire <see cref="OnViewChanged"/>.
    /// Pass <c>null</c> to remove filtering and show all items.
    /// </summary>
    public void SetFilter(IContainerFilter<TEnum> filter)
    {
        _filter = filter;
        OnViewChanged?.Invoke();
    }

    /// <summary>
    /// How many keys currently pass the active filter.
    /// Valid after the last call to <see cref="GetDisplayData"/>.
    /// </summary>
    public int FilteredCount => _filteredKeys.Count;

    /// <summary>
    /// Reverse-maps a UI slot index back to the original item key.
    /// Call this inside the handler for <see cref="UI_Container.OnSelectionChanged"/>.
    /// </summary>
    /// <returns><c>true</c> if the index is in range.</returns>
    public bool TryGetKeyAtIndex(int index, out TEnum key)
    {
        if (index >= 0 && index < _filteredKeys.Count)
        {
            key = _filteredKeys[index];
            return true;
        }

        key = default;
        return false;
    }

    /// <summary>
    /// Evaluates the current filter, rebuilds the internal key list, and
    /// returns a fresh <see cref="SlotDisplayData"/> array.
    ///
    /// Call sequence on receiving <see cref="OnViewChanged"/>:
    /// <code>
    ///   SlotDisplayData[] data = _view.GetDisplayData();
    ///   _ui.InitSlots(data.Length);
    ///   _ui.Refresh(data);
    /// </code>
    /// </summary>
    public SlotDisplayData[] GetDisplayData()
    {
        _filteredKeys.Clear();

        List<(TEnum key, int count)> entries = _source.GetEntries();

        // One pass: filter and build a count lookup simultaneously
        var countMap = new Dictionary<TEnum, int>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var (key, count) = entries[i];
            if (_filter == null || _filter.Matches(key))
            {
                _filteredKeys.Add(key);
                countMap[key] = count;
            }
        }

        var result = new SlotDisplayData[_filteredKeys.Count];
        for (int i = 0; i < _filteredKeys.Count; i++)
        {
            TEnum k    = _filteredKeys[i];
            var   prop = _propertyResolver(k);
            int   cnt  = countMap.TryGetValue(k, out int c) ? c : 0;

            result[i] = prop != null
                ? prop.ToSlotDisplayData(cnt)
                : SlotDisplayData.Empty;
        }

        return result;
    }

    /// <summary>
    /// Unsubscribes from the source.
    /// Call when the owning manager is destroyed.
    /// </summary>
    public void Dispose()
    {
        _source.OnSourceChanged -= HandleSourceChanged;
    }

    // ©¤©¤ Private ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void HandleSourceChanged()
    {
        OnViewChanged?.Invoke();
    }
}
