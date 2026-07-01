using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Store-specific container UI that extends <see cref="UI_Container"/>.
///
/// Responsibilities:
///   - Instantiates <see cref="UI_StoreSlot"/> prefabs instead of the base
///     <see cref="UI_ContainerSlot"/> prefab so that store-specific overlays work.
///   - Intercepts clicks on <see cref="SlotState.Locked"/> slots before they
///     reach the selection system.
///   - Slot count is fixed to the <see cref="StoreInventoryProperty"/> slot count and
///     is initialised once when the store opens; it never changes at runtime.
/// </summary>
public class UI_StoreContainer : UI_Container
{
    [Header("Store Slot Prefab")]
    [Tooltip("Prefab that must have a UI_StoreSlot component. Used instead of the base slotPrefab.")]
    [SerializeField] private UI_StoreSlot storeSlotPrefab;

    // Typed parallel list kept in sync with the inherited slotUIs list.
    // Allows direct access to UI_StoreSlot members without repeated casting.
    private readonly List<UI_StoreSlot> _storeSlotUIs = new List<UI_StoreSlot>();

    // --- Init ---

    /// <summary>
    /// Creates (or trims) <see cref="UI_StoreSlot"/> instances to match
    /// <paramref name="slotCount"/> and marks all slots empty.
    /// Call once when the store panel opens.
    /// </summary>
    public override void InitSlots(int slotCount)
    {
        // Remove excess
        while (_storeSlotUIs.Count > slotCount)
        {
            int last = _storeSlotUIs.Count - 1;
            Destroy(_storeSlotUIs[last].gameObject);
            _storeSlotUIs.RemoveAt(last);
            slotUIs.RemoveAt(last);
        }

        // Add missing
        while (_storeSlotUIs.Count < slotCount)
        {
            UI_StoreSlot slot = Instantiate(storeSlotPrefab, slotParent);
            slot.SetClickCallback(HandleSlotClicked);
            _storeSlotUIs.Add(slot);
            slotUIs.Add(slot);
        }

        // Mark all empty initially
        for (int i = 0; i < _storeSlotUIs.Count; i++)
            _storeSlotUIs[i].SetEmpty(i);

        ClearSelection();
    }

    // --- Click interception ---

    /// <summary>
    /// Blocks selection for <see cref="SlotState.Locked"/> slots.
    /// Re-clicking the selected store slot keeps it selected instead of toggling
    /// it off, so keyboard submit cannot accidentally clear the current choice.
    /// </summary>
    protected override void HandleSlotClicked(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _storeSlotUIs.Count)
        {
            if (_storeSlotUIs[slotIndex].CurrentState == SlotState.Locked)
                return;
        }

        if (slotIndex == SelectedSlotIndex)
            return;

        base.HandleSlotClicked(slotIndex);
    }
}
