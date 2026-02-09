using UnityEngine;

public class CarriageView : MonoBehaviour
{
    [Header("Slot Anchors (must match slotTypes length)")]
    public Transform[] slotAnchors;

    [Header("Slot Types (must match slotAnchors length)")]
    public ModuleSlotType[] slotTypes;

    public int SlotCount => slotAnchors != null ? slotAnchors.Length : 0;

    public Transform GetAnchor(int slotIndex)
    {
        if (slotAnchors == null || slotIndex < 0 || slotIndex >= slotAnchors.Length) return null;
        return slotAnchors[slotIndex];
    }

    public ModuleSlotType GetSlotType(int slotIndex)
    {
        if (slotTypes == null || slotIndex < 0 || slotIndex >= slotTypes.Length) return ModuleSlotType.Weapon;
        return slotTypes[slotIndex];
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (slotAnchors != null && slotTypes != null && slotAnchors.Length != slotTypes.Length)
        {
            Debug.LogWarning($"[CarriageView] slotAnchors length != slotTypes length on {name}", this);
        }
    }
#endif
}
