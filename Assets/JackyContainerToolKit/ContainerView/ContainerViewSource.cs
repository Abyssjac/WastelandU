using System;
using System.Collections.Generic;

/// <summary>
/// Wraps a <see cref="Container{TEnum}"/> as an observable data source for
/// <see cref="FilteredContainerView{TEnum}"/>.
/// Fires <see cref="OnSourceChanged"/> whenever the underlying container reports a change.
/// </summary>
public class ContainerViewSource<TEnum> where TEnum : struct
{
    private readonly Container<TEnum> _container;

    /// <summary>Fired whenever the underlying container changes.</summary>
    public event Action OnSourceChanged;

    public ContainerViewSource(Container<TEnum> container)
    {
        _container = container;
        _container.OnContainerChanged += HandleContainerChanged;
    }

    // ©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Returns all non-empty slots as (key, count) pairs in slot order.
    /// </summary>
    public List<(TEnum key, int count)> GetEntries()
    {
        var result = new List<(TEnum key, int count)>();
        IReadOnlyList<ContainerSlot<TEnum>> slots = _container.Slots;

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
                result.Add((slots[i].ItemEnum, slots[i].ItemCount));
        }

        return result;
    }

    /// <summary>
    /// Unsubscribes from the underlying container.
    /// Call when the owning view is no longer needed.
    /// </summary>
    public void Dispose()
    {
        _container.OnContainerChanged -= HandleContainerChanged;
    }

    // ©¤©¤ Private ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void HandleContainerChanged()
    {
        OnSourceChanged?.Invoke();
    }
}
