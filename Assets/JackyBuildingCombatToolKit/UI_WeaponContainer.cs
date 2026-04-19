using System;
using System.Collections.Generic;
using UnityEngine;
using JackyUtility;

/// <summary>
/// Maps a <see cref="Key_BuildablePP"/> to a <see cref="Key_ContainerItemPP"/>
/// so the weapon UI can look up display data (icon, name) from the container item system.
/// </summary>
[Serializable]
public struct BuildableToContainerItemMapping
{
    public Key_BuildablePP buildableKey;
    public Key_ContainerItemPP containerItemKey;
}

/// <summary>
/// Weapon HUD that displays the currently selected ammo (main slot) and the
/// next ammo that will be selected on switch (sub slot).
/// <para>
/// Reads from <see cref="WeaponBehaviour"/> and uses a configurable
/// <see cref="Key_BuildablePP"/> ¡ú <see cref="Key_ContainerItemPP"/> mapping
/// to resolve display data from the <see cref="ContainerItemDatabase"/>.
/// </para>
/// </summary>
public class UI_WeaponContainer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private WeaponBehaviour weaponBehaviour;

    [Header("Slots")]
    [Tooltip("UI slot showing the currently selected ammo.")]
    [SerializeField] private UI_ContainerSlot mainSlot;

    [Tooltip("UI slot showing the next ammo that will be selected on switch.")]
    [SerializeField] private UI_ContainerSlot subSlot;

    [Header("Mapping")]
    [Tooltip("Maps each BuildableProperty enum to a ContainerItemProperty enum for display lookup.")]
    [SerializeField] private BuildableToContainerItemMapping[] mappings = new BuildableToContainerItemMapping[0];

    // Runtime
    private Dictionary<Key_BuildablePP, Key_ContainerItemPP> buildableToContainerMap;
    private ContainerItemDatabase containerItemDB;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Awake()
    {
        // Build the lookup dictionary from inspector mappings
        buildableToContainerMap = new Dictionary<Key_BuildablePP, Key_ContainerItemPP>();
        for (int i = 0; i < mappings.Length; i++)
        {
            if (!buildableToContainerMap.ContainsKey(mappings[i].buildableKey))
                buildableToContainerMap[mappings[i].buildableKey] = mappings[i].containerItemKey;
        }

        var dbManager = PropertyDatabaseManager.Instance;
        if (dbManager != null)
            containerItemDB = dbManager.GetDatabase<ContainerItemDatabase>();

        if (containerItemDB == null)
            Debug.LogWarning("[UI_WeaponContainer] ContainerItemDatabase not found.");
    }

    private void OnEnable()
    {
        if (weaponBehaviour != null)
        {
            weaponBehaviour.OnWeaponChanged += OnWeaponChanged;
            weaponBehaviour.Container.OnContainerChanged += OnContainerChanged;
        }
    }

    private void OnDisable()
    {
        if (weaponBehaviour != null)
        {
            weaponBehaviour.OnWeaponChanged -= OnWeaponChanged;
            if (weaponBehaviour.Container != null)
                weaponBehaviour.Container.OnContainerChanged -= OnContainerChanged;
        }
    }

    private void Start()
    {
        RefreshDisplay();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Event Handlers ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void OnWeaponChanged(Key_BuildablePP newBuildable)
    {
        RefreshDisplay();
    }

    private void OnContainerChanged()
    {
        RefreshDisplay();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Display ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Refresh both main and sub slots based on current weapon state.
    /// </summary>
    public void RefreshDisplay()
    {
        if (weaponBehaviour == null) return;

        var container = weaponBehaviour.Container;
        if (container == null) return;

        Key_BuildablePP currentEnum = weaponBehaviour.CurrentBuildableEnum;

        // Find current and next buildable enums from the container
        Key_BuildablePP nextEnum = FindNextBuildableEnum(container, currentEnum);

        // Get item counts from weapon container
        int currentCount = currentEnum.Equals(Key_BuildablePP.None) ? 0 : container.GetItemCountByEnum(currentEnum);
        int nextCount = nextEnum.Equals(Key_BuildablePP.None) ? 0 : container.GetItemCountByEnum(nextEnum);

        // Resolve display data and update slots
        SlotDisplayData mainData = ResolveDisplayData(currentEnum, currentCount);
        SlotDisplayData subData = ResolveDisplayData(nextEnum, nextCount);

        if (mainSlot != null)
        {
            if (mainData.IsEmpty)
                mainSlot.SetEmpty(0);
            else
                mainSlot.SetSlot(0, mainData.icon, mainData.iconColor, mainData.count);
        }

        if (subSlot != null)
        {
            if (subData.IsEmpty)
                subSlot.SetEmpty(1);
            else
                subSlot.SetSlot(1, subData.icon, subData.iconColor, subData.count);
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Internal ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Find the next non-empty buildable enum in the container after the current one.
    /// Mirrors <see cref="WeaponBehaviour.SwitchToNextAmmo"/> logic.
    /// </summary>
    private Key_BuildablePP FindNextBuildableEnum(Container<Key_BuildablePP> container, Key_BuildablePP currentEnum)
    {
        var slots = container.Slots;
        int count = slots.Count;
        if (count == 0) return Key_BuildablePP.None;

        // Find the current slot index
        int startIndex = 0;
        for (int i = 0; i < count; i++)
        {
            if (!slots[i].IsEmpty && EqualityComparer<Key_BuildablePP>.Default.Equals(slots[i].ItemEnum, currentEnum))
            {
                startIndex = i + 1;
                break;
            }
        }

        // Search forward (wrapping) for the next non-empty slot that is different from current
        for (int offset = 0; offset < count; offset++)
        {
            int idx = (startIndex + offset) % count;
            if (!slots[idx].IsEmpty)
            {
                Key_BuildablePP candidate = slots[idx].ItemEnum;
                // Skip if it's the same as current (would happen when only one ammo type exists)
                if (!EqualityComparer<Key_BuildablePP>.Default.Equals(candidate, currentEnum))
                    return candidate;
            }
        }

        return Key_BuildablePP.None;
    }

    /// <summary>
    /// Convert a buildable enum + count into display data using the mapping ¡ú ContainerItemDB pipeline.
    /// </summary>
    private SlotDisplayData ResolveDisplayData(Key_BuildablePP buildableEnum, int itemCount)
    {
        if (buildableEnum.Equals(Key_BuildablePP.None) || itemCount <= 0)
            return SlotDisplayData.Empty;

        if (containerItemDB == null)
            return SlotDisplayData.Empty;

        // Map buildable key ¡ú container item key
        if (!buildableToContainerMap.TryGetValue(buildableEnum, out Key_ContainerItemPP containerKey))
            return SlotDisplayData.Empty;

        // Look up container item property for icon/display
        ContainerItemProperty itemProp = containerItemDB.GetByEnum(containerKey);
        if (itemProp == null)
            return SlotDisplayData.Empty;

        return itemProp.ToSlotDisplayData(itemCount);
    }
}
