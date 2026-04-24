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

    [Header("Panels")]
    [SerializeField] private GameObject weaponBuildPanel;
    [SerializeField] private GameObject weaponRecyclePanel;

    // Runtime
    private Dictionary<Key_BuildablePP, Key_ContainerItemPP> buildableToContainerMap;
    private ContainerItemDatabase containerItemDB;
    private Container<Key_BuildablePP> boundContainer;

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

        if (weaponBehaviour == null) { 
            weaponBehaviour = WeaponBehaviour.Instance;
        }
        if (weaponBehaviour == null)
            Debug.LogWarning("[UI_WeaponContainer] WeaponBehaviour reference not set and instance not found.");
    }

    private void OnEnable()
    {
        BindEvents();
        RefreshDisplay();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void Start()
    {
        BindEvents();
        RefreshDisplay();
    }

    private void BindEvents()
    {
        if (weaponBehaviour == null) return;

        weaponBehaviour.OnWeaponChanged -= OnWeaponChanged;
        weaponBehaviour.OnWeaponChanged += OnWeaponChanged;

        weaponBehaviour.OnWeaponModeChanged -= RefreshDisplayByMode;
        weaponBehaviour.OnWeaponModeChanged += RefreshDisplayByMode;

        RebindContainerIfNeeded();
    }

    private void UnbindEvents()
    {
        if (weaponBehaviour == null) return;

        weaponBehaviour.OnWeaponChanged -= OnWeaponChanged;
        weaponBehaviour.OnWeaponModeChanged -= RefreshDisplayByMode;

        if (boundContainer != null)
            boundContainer.OnContainerChanged -= OnContainerChanged;
        boundContainer = null;
    }

    private void RebindContainerIfNeeded()
    {
        if (weaponBehaviour == null) return;

        var currentContainer = weaponBehaviour.Container;
        if (ReferenceEquals(boundContainer, currentContainer))
            return;

        if (boundContainer != null)
            boundContainer.OnContainerChanged -= OnContainerChanged;

        boundContainer = currentContainer;

        if (boundContainer != null)
            boundContainer.OnContainerChanged += OnContainerChanged;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Event Handlers ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void OnWeaponChanged(Key_BuildablePP newBuildable)
    {
        RefreshDisplay();
    }

    private void OnContainerChanged()
    {
        Debug.Log($"[UI_WeaponContainer] Container Changed (bound={boundContainer != null})");
        RefreshDisplay();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Display ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Refresh both main and sub slots based on current weapon state.
    /// </summary>
    public void RefreshDisplay()
    {
        RebindContainerIfNeeded();

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

        Debug.Log($"[UI_WeaponContainer] RefreshDisplay - Current: {mainData} Next: {subData}");

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

    public void RefreshDisplayByMode(WeaponMode mode)
    {
        // Implementation for refreshing display based on weapon mode
        //weaponBuildPanel.SetActive(mode == WeaponMode.Build);
        weaponRecyclePanel.SetActive(mode == WeaponMode.Recycle);
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Internal ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Find the next non-empty buildable enum in the container after the current one (different from current).
    /// </summary>
    private Key_BuildablePP FindNextBuildableEnum(Container<Key_BuildablePP> container, Key_BuildablePP currentEnum)
    {
        return container.FindNextEnum(currentEnum);
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
