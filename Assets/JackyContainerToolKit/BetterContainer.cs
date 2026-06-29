using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class Slot<TEnum> where TEnum : struct
{
    [SerializeField] private TEnum itemEnum;
    [SerializeField] private int itemCount;

    public TEnum ItemEnum => itemEnum;
    public int ItemCount => itemCount;
    public virtual bool IsEmpty => itemCount <= 0;
    public virtual bool ClearWhenCountZero => true;

    public Slot()
    {
        itemEnum = default;
        itemCount = 0;
    }

    public Slot(TEnum itemEnum, int itemCount)
    {
        this.itemEnum = itemEnum;
        this.itemCount = itemCount;
    }

    public virtual void SetItem(TEnum itemEnum, int count)
    {
        this.itemEnum = itemEnum;
        this.itemCount = count;
    }

    public virtual void SetCount(int count)
    {
        itemCount = count;
    }
}

[Serializable]
public class SContainer<TSlot, TEnum> : ISerializationCallbackReceiver
    where TSlot : Slot<TEnum>, new()
    where TEnum : struct
{
    [SerializeField] private int maxSlots;
    [SerializeField] private List<TSlot> slots;
    [SerializeField] private bool useMaxStack;
    [SerializeField] private int maxStackCount = 99;

    public event Action OnContainerChanged;

    public int MaxSlots => maxSlots;
    public bool UseMaxStack { get => useMaxStack; set => useMaxStack = value; }
    public int MaxStackCount { get => maxStackCount; set => maxStackCount = value; }

    public int UsedSlots
    {
        get
        {
            EnsureInitialized();
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].IsEmpty) count++;
            return count;
        }
    }

    public int FreeSlots => maxSlots - UsedSlots;

    public bool IsEmpty
    {
        get
        {
            EnsureInitialized();
            for (int i = 0; i < slots.Count; i++)
                if (!slots[i].IsEmpty) return false;
            return true;
        }
    }

    public IReadOnlyList<TSlot> Slots
    {
        get
        {
            EnsureInitialized();
            return slots;
        }
    }

    // ─── Construction ─────────────────────────────────────────────

    public SContainer()
    {
        maxSlots = 0;
        slots = new List<TSlot>();
    }

    public SContainer(int maxSlots, bool useMaxStack = false, int maxStackCount = 99)
    {
        this.maxSlots = Mathf.Max(0, maxSlots);
        this.useMaxStack = useMaxStack;
        this.maxStackCount = maxStackCount;
        slots = new List<TSlot>(this.maxSlots);
        EnsureInitialized();
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        if (maxSlots < 0) maxSlots = 0;
        if (slots == null) slots = new List<TSlot>(maxSlots);

        while (slots.Count < maxSlots)
            slots.Add(new TSlot());

        while (slots.Count > maxSlots)
            slots.RemoveAt(slots.Count - 1);

        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null)
                slots[i] = new TSlot();
        }
    }

    public void SetMaxSlots(int slotCount)
    {
        maxSlots = Mathf.Max(0, slotCount);
        EnsureInitialized();
        OnContainerChanged?.Invoke();
    }

    // ─── Stack Limit ──────────────────────────────────────────────

    protected virtual int GetMaxStackFor(TEnum itemEnum)
    {
        return int.MaxValue;
    }

    private int ResolveMaxStack(TEnum itemEnum)
    {
        if (useMaxStack) return maxStackCount;
        return GetMaxStackFor(itemEnum);
    }

    // ─── Basic Add / Remove (All-or-Nothing) ──────────────────────

    public bool CanAddItem(TEnum itemEnum, int count, out string failReason)
    {
        EnsureInitialized();
        failReason = null;

        if (count <= 0)
        {
            failReason = "Add count must be greater than zero.";
            return false;
        }

        int maxStack = ResolveMaxStack(itemEnum);
        int capacity = 0;
        for (int i = 0; i < slots.Count && capacity < count; i++)
        {
            if (!slots[i].IsEmpty && EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum))
                capacity += maxStack - slots[i].ItemCount;
            else if (slots[i].IsEmpty)
                capacity += maxStack;
        }

        if (capacity >= count) return true;

        failReason = $"Not enough space for {count}x {itemEnum} (capacity for {capacity} more).";
        return false;
    }

    public bool TryAddItem(TEnum itemEnum, int count, out string failReason)
    {
        EnsureInitialized();
        failReason = null;

        if (count <= 0)
        {
            failReason = "Add count must be greater than zero.";
            return false;
        }

        int maxStack = ResolveMaxStack(itemEnum);

        // Phase 1 — dry run
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
            failReason = $"Not enough space for {count}x {itemEnum} (capacity for {capacity} more).";
            return false;
        }

        // Phase 2 — fill existing slots first
        int remaining = count;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].IsEmpty) continue;
            if (!EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum)) continue;

            int space = maxStack - slots[i].ItemCount;
            if (space <= 0) continue;

            int toAdd = Mathf.Min(remaining, space);
            slots[i].SetCount(slots[i].ItemCount + toAdd);
            remaining -= toAdd;
        }

        // Phase 3 — spill into empty slots
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;

            int toAdd = Mathf.Min(remaining, maxStack);
            slots[i].SetItem(itemEnum, toAdd);
            remaining -= toAdd;
        }

        OnContainerChanged?.Invoke();
        return true;
    }

    public bool TryRemoveItem(TEnum itemEnum, int count, out string failReason)
    {
        EnsureInitialized();
        failReason = null;

        if (count <= 0)
        {
            failReason = "Remove count must be greater than zero.";
            return false;
        }

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

        int remaining = count;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].IsEmpty) continue;
            if (!EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum)) continue;

            int current = slots[i].ItemCount;
            int toRemove = Mathf.Min(current, remaining);
            remaining -= toRemove;

            int left = current - toRemove;
            ApplyCountAfterRemoval(i, left);
        }

        OnContainerChanged?.Invoke();
        return true;
    }

    public bool TrySetSlotAtIndex(int index, TEnum itemEnum, int count, out string failReason)
    {
        EnsureInitialized();
        failReason = null;

        if (!IsValidIndex(index, out failReason)) return false;
        if (!IsValidCount(count, out failReason)) return false;

        int maxStack = ResolveMaxStack(itemEnum);
        if (count > maxStack)
        {
            failReason = $"Count {count} exceeds max stack {maxStack} for {itemEnum}.";
            return false;
        }

        slots[index].SetItem(itemEnum, count);
        OnContainerChanged?.Invoke();
        return true;
    }

    public bool TrySetCountAtIndex(int index, int count, out string failReason)
    {
        EnsureInitialized();
        failReason = null;

        if (!IsValidIndex(index, out failReason)) return false;
        if (!IsValidCount(count, out failReason)) return false;

        TSlot slot = slots[index];
        if (slot.IsEmpty && count > 0)
        {
            failReason = $"Cannot set count on empty slot {index}. Set the item first.";
            return false;
        }

        int maxStack = ResolveMaxStack(slot.ItemEnum);
        if (count > maxStack)
        {
            failReason = $"Count {count} exceeds max stack {maxStack} for {slot.ItemEnum}.";
            return false;
        }

        if (count > 0)
            slot.SetCount(count);
        else
            ApplyCountAfterRemoval(index, 0);

        OnContainerChanged?.Invoke();
        return true;
    }

    public bool TryAddCountAtIndex(int index, int amount, out string failReason)
    {
        EnsureInitialized();
        failReason = null;

        if (!IsValidIndex(index, out failReason)) return false;
        if (amount <= 0)
        {
            failReason = "Add amount must be greater than zero.";
            return false;
        }

        TSlot slot = slots[index];
        if (slot.IsEmpty)
        {
            failReason = $"Cannot add count to empty slot {index}. Set the item first.";
            return false;
        }

        int newCount = slot.ItemCount + amount;
        int maxStack = ResolveMaxStack(slot.ItemEnum);
        if (newCount > maxStack)
        {
            failReason = $"Count {newCount} exceeds max stack {maxStack} for {slot.ItemEnum}.";
            return false;
        }

        slot.SetCount(newCount);
        OnContainerChanged?.Invoke();
        return true;
    }

    public bool TryRemoveCountAtIndex(int index, int amount, out string failReason)
    {
        EnsureInitialized();
        failReason = null;

        if (!IsValidIndex(index, out failReason)) return false;
        if (amount <= 0)
        {
            failReason = "Remove amount must be greater than zero.";
            return false;
        }

        TSlot slot = slots[index];
        if (slot.IsEmpty)
        {
            failReason = $"Slot {index} is empty.";
            return false;
        }

        if (slot.ItemCount < amount)
        {
            failReason = $"Not enough items in slot {index}: need {amount}, have {slot.ItemCount}.";
            return false;
        }

        ApplyCountAfterRemoval(index, slot.ItemCount - amount);
        OnContainerChanged?.Invoke();
        return true;
    }

    // ─── Partial Add / Remove ─────────────────────────────────────

    public bool AddItemReturnExcess(TEnum itemEnum, int count, out int excess)
    {
        EnsureInitialized();
        excess = 0;

        if (count <= 0) return false;

        int maxStack = ResolveMaxStack(itemEnum);
        int remaining = count;

        // Fill existing slots first
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].IsEmpty) continue;
            if (!EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum)) continue;

            int space = maxStack - slots[i].ItemCount;
            if (space <= 0) continue;

            int toAdd = Mathf.Min(remaining, space);
            slots[i].SetCount(slots[i].ItemCount + toAdd);
            remaining -= toAdd;
        }

        // Spill into empty slots
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;

            int toAdd = Mathf.Min(remaining, maxStack);
            slots[i].SetItem(itemEnum, toAdd);
            remaining -= toAdd;
        }

        excess = remaining;
        bool anyAdded = remaining < count;
        if (anyAdded) OnContainerChanged?.Invoke();
        return anyAdded;
    }

    public bool RemoveItemReturnLack(TEnum itemEnum, int count, out int lack)
    {
        EnsureInitialized();
        lack = 0;

        if (count <= 0) return false;

        int totalAvailable = GetItemCountByEnum(itemEnum);

        if (totalAvailable <= 0)
        {
            lack = count;
            return false;
        }

        int remaining = count;
        for (int i = 0; i < slots.Count && remaining > 0; i++)
        {
            if (slots[i].IsEmpty) continue;
            if (!EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum)) continue;

            int current = slots[i].ItemCount;
            int toRemove = Mathf.Min(current, remaining);
            remaining -= toRemove;

            int left = current - toRemove;
            ApplyCountAfterRemoval(i, left);
        }

        lack = remaining;
        bool anyRemoved = remaining < count;
        if (anyRemoved) OnContainerChanged?.Invoke();
        return anyRemoved;
    }

    // ─── Query ────────────────────────────────────────────────────

    public bool IsSlotEmptyAtIndex(int index)
    {
        EnsureInitialized();
        if (index < 0 || index >= slots.Count) return true;
        return slots[index].IsEmpty;
    }

    public int GetItemCountByEnum(TEnum itemEnum)
    {
        EnsureInitialized();
        int total = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty) continue;
            if (EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum))
                total += slots[i].ItemCount;
        }
        return total;
    }

    public TSlot GetSlotByIndex(int index)
    {
        EnsureInitialized();
        if (index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    // ─── Clear / Empty ────────────────────────────────────────────

    public bool EmptySlotAtIndex(int index)
    {
        EnsureInitialized();
        if (index < 0 || index >= slots.Count) return false;
        if (slots[index].IsEmpty) return false;

        slots[index] = new TSlot();
        OnContainerChanged?.Invoke();
        return true;
    }

    public bool EmptyItemByEnum(TEnum itemEnum)
    {
        EnsureInitialized();
        bool removed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty) continue;
            if (!EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum)) continue;

            slots[i] = new TSlot();
            removed = true;
        }

        if (removed) OnContainerChanged?.Invoke();
        return removed;
    }

    // ─── Internal Helpers ─────────────────────────────────────────

    /// <summary>
    /// Fully resets every slot to a new empty slot instance.
    /// This clears both item data and slot metadata such as locked/unlocked flags.
    /// Use this when the entire container should return to its default structure.
    /// </summary>
    public bool ClearAllSlots()
    {
        EnsureInitialized();

        bool changed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty)
                changed = true;

            slots[i] = new TSlot();
        }

        if (changed) OnContainerChanged?.Invoke();
        return changed;
    }

    /// <summary>
    /// Removes item data from every slot while preserving each slot object and its metadata.
    /// For example, a backpack slot keeps its locked/unlocked flag, but its item enum and count are cleared.
    /// Use this for clearing backpack contents without changing which slots are locked.
    /// </summary>
    public bool EmptyAllItems()
    {
        EnsureInitialized();

        bool changed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].ItemCount != 0 || !EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, default))
                changed = true;

            slots[i].SetItem(default, 0);
        }

        if (changed) OnContainerChanged?.Invoke();
        return changed;
    }

    /// <summary>
    /// Sets every slot count to zero while preserving item enum and slot metadata.
    /// Use this for fixed-slot systems like stores where an item should remain configured
    /// but its current stock should become zero, allowing UI states such as SoldOut.
    /// </summary>
    public bool SetAllCountsToZero()
    {
        EnsureInitialized();

        bool changed = false;
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].ItemCount != 0)
                changed = true;

            slots[i].SetCount(0);
        }

        if (changed) OnContainerChanged?.Invoke();
        return changed;
    }

    private int FindSlotIndexByEnum(TEnum itemEnum)
    {
        EnsureInitialized();
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && EqualityComparer<TEnum>.Default.Equals(slots[i].ItemEnum, itemEnum))
                return i;
        }
        return -1;
    }

    private int FindFirstEmptySlotIndex()
    {
        EnsureInitialized();
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i].IsEmpty) return i;
        }
        return -1;
    }

    private bool IsValidIndex(int index, out string failReason)
    {
        failReason = null;
        if (index >= 0 && index < slots.Count) return true;

        failReason = $"Slot index {index} is out of range.";
        return false;
    }

    private bool IsValidCount(int count, out string failReason)
    {
        failReason = null;
        if (count >= 0) return true;

        failReason = "Count cannot be negative.";
        return false;
    }

    private void ApplyCountAfterRemoval(int index, int left)
    {
        if (left > 0)
        {
            slots[index].SetCount(left);
            return;
        }

        if (slots[index].ClearWhenCountZero)
            slots[index] = new TSlot();
        else
            slots[index].SetCount(0);
    }
}
