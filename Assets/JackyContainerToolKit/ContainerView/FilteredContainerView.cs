using System;
using System.Collections.Generic;

/// <summary>
/// Combines a container entry source with an optional filter and property resolver.
/// </summary>
public class FilteredContainerView<TEnum> where TEnum : struct
{
    private readonly IContainerEntrySource<TEnum> source;
    private readonly Func<TEnum, ISlotDisplayableProperty> propertyResolver;
    private IContainerFilter<TEnum> filter;
    private readonly List<TEnum> filteredKeys = new List<TEnum>();

    public event Action OnViewChanged;

    public FilteredContainerView(
        IContainerEntrySource<TEnum> source,
        Func<TEnum, ISlotDisplayableProperty> propertyResolver,
        IContainerFilter<TEnum> filter = null)
    {
        this.source = source;
        this.propertyResolver = propertyResolver;
        this.filter = filter;
        this.source.OnSourceChanged += HandleSourceChanged;
    }

    public int FilteredCount => filteredKeys.Count;

    public void SetFilter(IContainerFilter<TEnum> filter)
    {
        this.filter = filter;
        OnViewChanged?.Invoke();
    }

    public bool TryGetKeyAtIndex(int index, out TEnum key)
    {
        if (index >= 0 && index < filteredKeys.Count)
        {
            key = filteredKeys[index];
            return true;
        }

        key = default;
        return false;
    }

    public SlotDisplayData[] GetDisplayData()
    {
        filteredKeys.Clear();

        List<(TEnum key, int count)> entries = source.GetEntries();
        var countMap = new Dictionary<TEnum, int>(entries.Count);
        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (filter == null || filter.Matches(entry.key))
            {
                filteredKeys.Add(entry.key);
                countMap[entry.key] = entry.count;
            }
        }

        var result = new SlotDisplayData[filteredKeys.Count];
        for (int i = 0; i < filteredKeys.Count; i++)
        {
            TEnum key = filteredKeys[i];
            ISlotDisplayableProperty property = propertyResolver(key);
            int count = countMap.TryGetValue(key, out int value) ? value : 0;
            result[i] = property != null
                ? property.ToSlotDisplayData(count)
                : SlotDisplayData.Empty;
        }

        return result;
    }

    public void Dispose()
    {
        source.OnSourceChanged -= HandleSourceChanged;
    }

    private void HandleSourceChanged()
    {
        OnViewChanged?.Invoke();
    }
}