using System;
using System.Collections.Generic;

/// <summary>
/// Wraps a legacy <see cref="Container{TEnum}"/> as an observable container view source.
/// </summary>
public class ContainerViewSource<TEnum> : IContainerEntrySource<TEnum> where TEnum : struct
{
    private readonly Container<TEnum> container;

    public event Action OnSourceChanged;

    public ContainerViewSource(Container<TEnum> container)
    {
        this.container = container;
        this.container.OnContainerChanged += HandleContainerChanged;
    }

    public List<(TEnum key, int count)> GetEntries()
    {
        var result = new List<(TEnum key, int count)>();
        IReadOnlyList<ContainerSlot<TEnum>> slots = container.Slots;

        for (int i = 0; i < slots.Count; i++)
        {
            ContainerSlot<TEnum> slot = slots[i];
            if (!slot.IsEmpty)
                result.Add((slot.ItemEnum, slot.ItemCount));
        }

        return result;
    }

    public void Dispose()
    {
        container.OnContainerChanged -= HandleContainerChanged;
    }

    private void HandleContainerChanged()
    {
        OnSourceChanged?.Invoke();
    }
}