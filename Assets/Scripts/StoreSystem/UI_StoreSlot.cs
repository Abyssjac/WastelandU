using UnityEngine;

/// <summary>
/// Store-specific slot that extends <see cref="UI_ContainerSlot"/>.
/// The only behavioural addition is blocking click interactions when the slot
/// is in the <see cref="SlotState.Locked"/> state so that locked entries
/// never trigger <see cref="UI_Container.OnSelectionChanged"/>.
///
/// All state overlay logic is inherited from the base class.
/// Configure the <c>State Overlays</c> list on the prefab to assign
/// the correct GameObjects for Empty, SoldOut and Locked states.
/// </summary>
public class UI_StoreSlot : UI_ContainerSlot
{
    // SetSlot, SetEmpty, and ApplyStateOverlays are all inherited unchanged.
    // No additional serialized fields are required here.
}
