using System;
using System.Collections.Generic;

/// <summary>
/// Adapts an <see cref="SContainer{TSlot,TEnum}"/> for container views.
/// </summary>
public class SContainerViewSource<TSlot, TEnum> : IContainerEntrySource<TEnum>
    where TSlot : Slot<TEnum>, new()
    where TEnum : struct
{
    private readonly SContainer<TSlot, TEnum> container;

    public event Action OnSourceChanged;

    public SContainerViewSource(SContainer<TSlot, TEnum> container)
    {
        this.container = container;
        this.container.OnContainerChanged += HandleContainerChanged;
    }

    public List<(TEnum key, int count)> GetEntries()
    {
        var result = new List<(TEnum key, int count)>();
        IReadOnlyList<TSlot> slots = container.Slots;

        for (int i = 0; i < slots.Count; i++)
        {
            TSlot slot = slots[i];
            if (slot != null && !slot.IsEmpty)
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
