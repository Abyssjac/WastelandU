using UnityEngine;
using System;
using System.Collections.Generic;
using JackyUtility;

[Serializable]
public struct ContainerSlot<TEnum> where TEnum : struct
{
    [SerializeField] private TEnum itemEnum;
    [SerializeField] private int itemCount;

    public TEnum ItemEnum => itemEnum;
    public int ItemCount => itemCount;
    public bool IsEmpty => itemCount <= 0;

    public ContainerSlot(TEnum itemEnum, int itemCount)
    {
        this.itemEnum = itemEnum;
        this.itemCount = itemCount;
    }

    public ContainerSlot<TEnum> WithCount(int newCount)
    {
        return new ContainerSlot<TEnum>(itemEnum, newCount);
    }

    public static ContainerSlot<TEnum> Empty => new ContainerSlot<TEnum>(default, 0);
}

[Serializable]
public class Container<TEnum> where TEnum : struct
{
    [SerializeField] private int maxSlots;
    [SerializeField] private List<ContainerSlot<TEnum>> slots = new List<ContainerSlot<TEnum>>();

    private Dictionary<TEnum, int> enumCountMap;

    /// <summary>
    /// Optional callback that returns the max stack size for a given item enum.
    /// If null, stacking is unlimited (all items pile into one slot).
    /// </summary>
    private Func<TEnum, int> getMaxStack;

    public event Action OnContainerChanged;

    public int MaxSlots => maxSlots;
    public int UsedSlots
    {
        get
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty) count++;
            }
            return count;
        }
    }
    public int FreeSlots => maxSlots - UsedSlots;
    public IReadOnlyList<ContainerSlot<TEnum>> Slots => slots;

    // ©¤©¤©¤ Construction ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public Container(int maxSlots, Func<TEnum, int> getMaxStack = null)
    {
        this.maxSlots = maxSlots;
        this.getMaxStack = getMaxStack;
        slots = new List<ContainerSlot<TEnum>>(maxSlots);
        for (int i = 0; i < maxSlots; i++)
            slots.Add(ContainerSlot<TEnum>.Empty);
        RebuildEnumCountMap();
    }

    /// <summary>
    /// Set or replace the max-stack callback at runtime.
    /// </summary>
    public void SetMaxStackProvider(Func<TEnum, int> provider)
    {
        getMaxStack = provider;
    }

    private int GetMaxStackFor(TEnum itemEnum)
    {
        if (getMaxStack != null) return getMaxStack(itemEnum);
        return int.MaxValue;
    }

    // ©¤©¤©¤ Dictionary Tracking ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void EnsureMapInitialized()
    {
        if (enumCountMap == null)
            RebuildEnumCountMap();
    }

    private void RebuildEnumCountMap()
    {
        enumCountMap = new Dictionary<TEnum, int>();
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty) continue;

            if (enumCountMap.ContainsKey(slot.ItemEnum))
                enumCountMap[slot.ItemEnum] += slot.ItemCount;
            else
                enumCountMap[slot.ItemEnum] = slot.ItemCount;
        }
    }

    // ©¤©¤©¤ Basic Add / Remove (All-or-Nothing) ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Try to add <paramref name="count"/> items of <paramref name="itemEnum"/>.
    /// Respects per-item max stack size. May spread across multiple slots.
    /// Succeeds only if ALL items can be placed; otherwise nothing is changed.
    /// Returns false with a reason string when it cannot be done.
    /// </summary>
    public bool TryAddItem(TEnum itemEnum, int count, out string failReason)
    {
        failReason = null;
        EnsureMapInitialized();

        if (count <= 0)
        {
            failReason = "Add count must be greater than zero.";
            return false;
        }

        int maxStack = GetMaxStackFor(itemEnum);
        int remaining = count;

        // Phase 1 ¡ª dry run: check if there is enough total capacity
        int capacity = 0;
        for (int i = 0; i < slots.Count && capacity < count; i++)
        {
            if (!slots[i].IsEmpty && EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum))
                capacity += maxStack - slots[i].ItemCount;
            else if (slots[i].IsEmpty)
                capacity += maxStack;
        }

        if (capacity < count)
        {
            failReason = $"Not enough space for {count}¡Á {itemEnum} (capacity for {capacity} more).";
            return false;
        }

        // Phase 2 ¡ª commit: fill existing slots of the same enum first
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].IsEmpty) continue;
            if (!EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum)) continue;

            int space = maxStack - slots[i].ItemCount;
            if (space <= 0) continue;

            int toAdd = Mathf.Min(remaining, space);
            slots[i] = slots[i].WithCount(slots[i].ItemCount + toAdd);
            remaining -= toAdd;
        }

        // Phase 3 ¡ª commit: spill into empty slots
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;

            int toAdd = Mathf.Min(remaining, maxStack);
            slots[i] = new ContainerSlot<TEnum>(itemEnum, toAdd);
            remaining -= toAdd;
        }

        RebuildEnumCountMap();
        OnContainerChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Try to remove <paramref name="count"/> items of <paramref name="itemEnum"/>.
    /// Succeeds only if the container holds at least that many across all slots.
    /// Returns false with a reason string when it cannot be done.
    /// </summary>
    public bool TryRemoveItem(TEnum itemEnum, int count, out string failReason)
    {
        failReason = null;
        EnsureMapInitialized();

        if (count <= 0)
        {
            failReason = "Remove count must be greater than zero.";
            return false;
        }

        // Dry run ¡ª count total available
        int totalAvailable = GetItemCountByEnum(itemEnum);
        if (totalAvailable <= 0)
        {
            failReason = $"Item {itemEnum} not found in container.";
            return false;
        }
        if (totalAvailable < count)
        {
            failReason = $"Not enough {itemEnum}: need {count}, have {totalAvailable}.";
            return false;
        }

        // Commit ¡ª remove across slots
        int remaining = count;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].IsEmpty) continue;
            if (!EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum)) continue;

            int current = slots[i].ItemCount;
            int toRemove = Mathf.Min(current, remaining);
            int left = current - toRemove;
            remaining -= toRemove;

            if (left > 0)
                slots[i] = slots[i].WithCount(left);
            else
                slots[i] = ContainerSlot<TEnum>.Empty;
        }

        RebuildEnumCountMap();
        OnContainerChanged?.Invoke();
        return true;
    }

    // ©¤©¤©¤ Partial Add / Remove ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Adds as many items as possible, respecting max stack size.
    /// Returns true if any were added.
    /// <paramref name="excess"/> is the quantity that could NOT be added (0 if all fit).
    /// </summary>
    public bool AddItemReturnExcess(TEnum itemEnum, int count, out int excess)
    {
        EnsureMapInitialized();
        excess = 0;

        if (count <= 0)
        {
            excess = 0;
            return false;
        }

        int maxStack = GetMaxStackFor(itemEnum);
        int remaining = count;

        // Fill existing slots of the same enum first
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].IsEmpty) continue;
            if (!EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum)) continue;

            int space = maxStack - slots[i].ItemCount;
            if (space <= 0) continue;

            int toAdd = Mathf.Min(remaining, space);
            slots[i] = slots[i].WithCount(slots[i].ItemCount + toAdd);
            remaining -= toAdd;
        }

        // Spill into empty slots
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;

            int toAdd = Mathf.Min(remaining, maxStack);
            slots[i] = new ContainerSlot<TEnum>(itemEnum, toAdd);
            remaining -= toAdd;
        }

        excess = remaining;
        bool anyAdded = remaining < count;
        if (anyAdded) {
            RebuildEnumCountMap();
            OnContainerChanged?.Invoke();
        }

        return anyAdded;
    }

    /// <summary>
    /// Removes as many items as possible across all matching slots.
    /// Returns true if any were removed.
    /// <paramref name="lack"/> is the quantity that could NOT be removed (0 if all were present).
    /// </summary>
    public bool RemoveItemReturnLack(TEnum itemEnum, int count, out int lack)
    {
        EnsureMapInitialized();
        lack = 0;

        if (count <= 0)
        {
            lack = 0;
            return false;
        }

        int remaining = count;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].IsEmpty) continue;
            if (!EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum)) continue;

            int current = slots[i].ItemCount;
            int toRemove = Mathf.Min(current, remaining);
            int left = current - toRemove;
            remaining -= toRemove;

            if (left > 0)
                slots[i] = slots[i].WithCount(left);
            else
                slots[i] = ContainerSlot<TEnum>.Empty;
        }

        lack = remaining;
        bool anyRemoved = remaining < count;
        if (anyRemoved) {
            RebuildEnumCountMap();
            OnContainerChanged?.Invoke();
        } 
        return anyRemoved;
    }

    // ©¤©¤©¤ Query ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// True when every slot in the container is empty.
    /// </summary>
    public bool IsEmpty
    {
        get
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Returns whether the slot at <paramref name="index"/> is empty.
    /// Out-of-range indices return true (treated as empty).
    /// </summary>
    public bool IsSlotEmptyAtIndex(int index)
    {
        if (index < 0 || index >= slots.Count) return true;
        return slots[index].IsEmpty;
    }

    /// <summary>
    /// Returns the total count of items with the given enum across all slots.
    /// </summary>
    public int GetItemCountByEnum(TEnum itemEnum)
    {
        EnsureMapInitialized();
        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum))
                total += slots[i].ItemCount;
        }
        return total;
    }

    /// <summary>
    /// Returns the slot info at the given index. Returns an empty slot if index is out of range.
    /// </summary>
    public ContainerSlot<TEnum> GetItemInfoByIndex(int index)
    {
        if (index < 0 || index >= slots.Count)
            return ContainerSlot<TEnum>.Empty;
        return slots[index];
    }

    // ©¤©¤©¤ Clear / Empty ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Clears the slot at the given index, making it empty.
    /// </summary>
    public bool EmptySlotAtIndex(int index)
    {
        EnsureMapInitialized();

        if (index < 0 || index >= slots.Count)
            return false;

        var slot = slots[index];
        if (slot.IsEmpty)
            return false;

        TEnum itemEnum = slot.ItemEnum;
        slots[index] = ContainerSlot<TEnum>.Empty;

        // Update map: subtract this slot's count, remove key if zero
        if (enumCountMap.ContainsKey(itemEnum))
        {
            enumCountMap[itemEnum] -= slot.ItemCount;
            if (enumCountMap[itemEnum] <= 0)
                enumCountMap.Remove(itemEnum);
        }

        OnContainerChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Removes ALL items matching <paramref name="itemEnum"/> from every slot.
    /// </summary>
    public bool EmptyItemByEnum(TEnum itemEnum)
    {
        EnsureMapInitialized();

        bool removed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum))
            {
                slots[i] = ContainerSlot<TEnum>.Empty;
                removed = true;
            }
        }
        if (removed) {
            enumCountMap.Remove(itemEnum);
            OnContainerChanged?.Invoke();
        }
        return removed;
    }

    /// <summary>
    /// Clears every slot in the container, making it completely empty.
    /// Fires <see cref="OnContainerChanged"/> once if anything was cleared.
    /// </summary>
    public bool ClearAll()
    {
        bool hadContent = false;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
            {
                slots[i] = ContainerSlot<TEnum>.Empty;
                hadContent = true;
            }
        }
        if (hadContent)
        {
            RebuildEnumCountMap();
            OnContainerChanged?.Invoke();
        }
        return hadContent;
    }

    /// <summary>
    /// Replaces the contents of this container with a slot-for-slot copy of <paramref name="source"/>.
    /// Slots beyond the source length are cleared. MaxSlots is not changed.
    /// Fires <see cref="OnContainerChanged"/> once after the copy.
    /// </summary>
    public void CopyFrom(Container<TEnum> source)
    {
        if (source == null) return;

        for (int i = 0; i < slots.Count; i++)
        {
            if (i < source.Slots.Count)
                slots[i] = source.Slots[i];
            else
                slots[i] = ContainerSlot<TEnum>.Empty;
        }

        RebuildEnumCountMap();
        OnContainerChanged?.Invoke();
    }

    // ©¤©¤©¤ Internal Helpers ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private int FindSlotIndexByEnum(TEnum itemEnum)
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum))
                return i;
        }
        return -1;
    }

    private int FindFirstEmptySlotIndex()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty)
                return i;
        }
        return -1;
    }

    // ©¤©¤©¤ Navigation / Cycling ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Find the slot index of the first non-empty slot whose enum matches <paramref name="itemEnum"/>.
    /// Returns -1 if not found.
    /// </summary>
    public int FindSlotIndex(TEnum itemEnum)
    {
        return FindSlotIndexByEnum(itemEnum);
    }

    /// <summary>
    /// Find the next non-empty enum after <paramref name="currentEnum"/> (wrapping around).
    /// Returns <c>default(TEnum)</c> if the container is empty or only contains <paramref name="currentEnum"/>.
    /// </summary>
    public TEnum FindNextEnum(TEnum currentEnum)
    {
        int count = slots.Count;
        if (count == 0) return default;

        int startIndex = 0;
        for (int i = 0; i < count; i++)
        {
            if (!slots[i].IsEmpty && EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, currentEnum))
            {
                startIndex = i + 1;
                break;
            }
        }

        for (int offset = 0; offset < count; offset++)
        {
            int idx = (startIndex + offset) % count;
            if (!slots[idx].IsEmpty)
            {
                TEnum candidate = slots[idx].ItemEnum;
                if (!EqualityComparer<TEnum>.Default.Equals(candidate, currentEnum))
                    return candidate;
            }
        }

        return default;
    }

    /// <summary>
    /// Find the next non-empty enum after <paramref name="currentEnum"/> (wrapping around).
    /// Unlike <see cref="FindNextEnum"/>, this allows returning the same enum if it's the only one.
    /// Returns <c>default(TEnum)</c> only if the container is completely empty.
    /// </summary>
    public TEnum CycleNextEnum(TEnum currentEnum)
    {
        int count = slots.Count;
        if (count == 0) return default;

        int startIndex = 0;
        for (int i = 0; i < count; i++)
        {
            if (!slots[i].IsEmpty && EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, currentEnum))
            {
                startIndex = i + 1;
                break;
            }
        }

        for (int offset = 0; offset < count; offset++)
        {
            int idx = (startIndex + offset) % count;
            if (!slots[idx].IsEmpty)
                return slots[idx].ItemEnum;
        }

        return default;
    }

    /// <summary>
    /// Returns a list of all distinct non-empty enums currently in the container, in slot order.
    /// </summary>
    public List<TEnum> GetDistinctEnums()
    {
        HashSet<TEnum> seen = new HashSet<TEnum>();
        List<TEnum> result = new List<TEnum>();

        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && seen.Add(slots[i].ItemEnum))
                result.Add(slots[i].ItemEnum);
        }

        return result;
    }
}

/// <summary>
/// Bridges a <see cref="Container{TEnum}"/> with an
/// <see cref="EnumStringKeyedDatabase{TEntry,TEnum}"/> so that every slot
/// can resolve its enum to the full property ScriptableObject.
/// <para>
/// Container stays pure serializable data; this class is the runtime query layer.
/// </para>
/// </summary>
public class ContainerPropertyLookup<TEntry, TEnum>
    where TEntry : ScriptableObject, IEnumStringKeyedEntry<TEnum>
    where TEnum : struct
{
    private readonly Container<TEnum> container;
    private readonly EnumStringKeyedDatabase<TEntry, TEnum> database;

    private UI_Container boundUIContainer;

    public Container<TEnum> Container => container;
    public EnumStringKeyedDatabase<TEntry, TEnum> Database => database;

    public ContainerPropertyLookup(
        Container<TEnum> container,
        EnumStringKeyedDatabase<TEntry, TEnum> database)
    {
        this.container = container;
        this.database = database;
    }

    // ©¤©¤©¤ Property Query ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Returns the property for the given enum key, or null if not found in the database.
    /// </summary>
    public TEntry GetPropertyByEnum(TEnum itemEnum)
    {
        return database.GetByEnum(itemEnum);
    }

    /// <summary>
    /// Returns the property for the given string key, or null if not found in the database.
    /// </summary>
    public TEntry GetPropertyByString(string itemString)
    {
        return database.GetByString(itemString);
    }

    /// <summary>
    /// Returns the property for the slot at the given index.
    /// Returns null if the slot is empty or the enum is not in the database.
    /// </summary>
    public TEntry GetPropertyByIndex(int index)
    {
        var slot = container.GetItemInfoByIndex(index);
        if (slot.IsEmpty) return null;
        return database.GetByEnum(slot.ItemEnum);
    }

    /// <summary>
    /// Tries to get the property for a slot's enum.
    /// </summary>
    public bool TryGetPropertyByEnum(TEnum itemEnum, out TEntry entry)
    {
        return database.TryGetByEnum(itemEnum, out entry);
    }

    /// <summary>
    /// Iterates all non-empty slots and returns (slot, property) pairs.
    /// </summary>
    public List<(ContainerSlot<TEnum> slot, TEntry property)> GetAllSlotProperties()
    {
        var results = new List<(ContainerSlot<TEnum>, TEntry)>();
        var slots = container.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty) continue;

            database.TryGetByEnum(slot.ItemEnum, out var entry);
            results.Add((slot, entry));
        }
        return results;
    }


    public void BindUIContainer(UI_Container uiContainer)
    {
        boundUIContainer = uiContainer;
        boundUIContainer.InitSlots(container.MaxSlots);
        container.OnContainerChanged += RefreshUIContainer;
    }
    public void UnBindUIContainer()
    {
        if (boundUIContainer != null)
            container.OnContainerChanged -= RefreshUIContainer;
        boundUIContainer = null;
    }

    public void RefreshUIContainer()
    {
        var displayData = BuildDisplayData();
        if(displayData != null && boundUIContainer != null)
            boundUIContainer.Refresh(displayData);
    }
    /// <summary>
    /// Builds display data automatically when TEntry implements <see cref="ISlotDisplayableProperty"/>.
    /// Returns null if TEntry does not implement the interface.
    /// </summary>
    public SlotDisplayData[] BuildDisplayData()
    {
        if (!typeof(ISlotDisplayableProperty).IsAssignableFrom(typeof(TEntry)))
        {
            Debug.LogWarning($"[ContainerPropertyLookup] {typeof(TEntry).Name} does not implement ISlotDisplayableProperty. Use the delegate overload instead.");
            return null;
        }

        var slots = container.Slots;
        var result = new SlotDisplayData[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (slot.IsEmpty)
            {
                result[i] = SlotDisplayData.Empty;
                continue;
            }

            if (database.TryGetByEnum(slot.ItemEnum, out var entry) && entry is ISlotDisplayableProperty displayable)
                result[i] = displayable.ToSlotDisplayData(slot.ItemCount);
            else
                result[i] = SlotDisplayData.Empty;
        }
        return result;
    }
}

public enum E_ContainerType
{
    None = 0,
    Backpack = 1,
}