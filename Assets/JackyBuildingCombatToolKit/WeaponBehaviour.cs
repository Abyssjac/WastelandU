using System;
using System;
using System.Collections.Generic;
using UnityEngine;
using JackyUtility;

public enum WeaponMode
{
    Build,
    Recycle,
}

/// <summary>
/// Manages the player's weapon: which buildable is selected, ammo container,
/// switching between ammo types, rotation control, shooting, and recycling.
/// Supports two modes: Build (place buildables) and Recycle (pick up placed buildables).
/// </summary>
public class WeaponBehaviour : MonoBehaviour
{

    public static WeaponBehaviour Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerShootPositionProvider shootProvider;
    [SerializeField] private PlayerShootPreviewController previewController;

    [Header("Input")]
    [Tooltip("Key to switch to the next available ammo type in the container.")]
    [SerializeField] private KeyCode switchKey = KeyCode.Tab;

    [Tooltip("Key to toggle between Build and Recycle mode.")]
    [SerializeField] private KeyCode modeToggleKey = KeyCode.Q;

    [Tooltip("Mouse button index for the primary action (shoot in Build mode, recycle in Recycle mode).")]
    [SerializeField] private int primaryMouseButton = 0;

    [Tooltip("Key to rotate the buildable before shooting.")]
    [SerializeField] private KeyCode rotateKey = KeyCode.R;

    [Header("Container")]
    [Tooltip("Number of ammo slots in the weapon container.")]
    [SerializeField] private int containerSlotCount = 6;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Runtime ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public Container<Key_BuildablePP> container;
    private BuildableDatabase buildableDB;

    private Key_BuildablePP curBuildableEnum = Key_BuildablePP.None;
    private BuildableProperty curBuildableProperty;
    private int currentRotationStep;
    private WeaponMode currentMode = WeaponMode.Build;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public Container<Key_BuildablePP> Container => container;
    public Key_BuildablePP CurrentBuildableEnum => curBuildableEnum;
    public BuildableProperty CurrentBuildableProperty => curBuildableProperty;
    public int CurrentRotationStep => currentRotationStep;
    public WeaponMode CurrentMode => currentMode;

    /// <summary>Fired when the selected ammo type changes.</summary>
    public event Action<Key_BuildablePP> OnWeaponChanged;

    /// <summary>Fired when the weapon mode changes (Build ? Recycle).</summary>
    public event Action<WeaponMode> OnWeaponModeChanged;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning($"[WeaponBehaviour] Multiple instances detected. Destroying duplicate on '{gameObject.name}'.");
            Destroy(this);
            return;
        }
        Instance = this;

        container = new Container<Key_BuildablePP>(containerSlotCount);

        var dbManager = PropertyDatabaseManager.Instance;
        if (dbManager != null)
            buildableDB = dbManager.GetDatabase<BuildableDatabase>();

        if (buildableDB == null)
            Debug.LogWarning("[WeaponBehaviour] BuildableDatabase not found.");

        container.OnContainerChanged += OnContainerChangedRefreshSelection;
    }

    private void OnDestroy()
    {
        if (container != null)
            container.OnContainerChanged -= OnContainerChangedRefreshSelection;
    }

    /// <summary>
    /// Called whenever the container changes. Keeps curBuildableEnum in sync:
    /// - Container empty ¡ú select None
    /// - Current enum no longer in container ¡ú select first available
    /// - Container was empty and now has items ¡ú select first available
    /// </summary>
    private void OnContainerChangedRefreshSelection()
    {
        if (container == null) return;

        if (container.IsEmpty)
        {
            SelectAmmo(Key_BuildablePP.None);
            return;
        }

        bool currentStillValid = !EqualityComparer<Key_BuildablePP>.Default.Equals(curBuildableEnum, Key_BuildablePP.None)
                                 && container.GetItemCountByEnum(curBuildableEnum) > 0;
        if (!currentStillValid)
        {
            Key_BuildablePP first = container.FindNextEnum(Key_BuildablePP.None);
            SelectAmmo(first);
        }
    }

    private void Start()
    {
        SetMode(WeaponMode.Recycle);
        OnContainerChangedRefreshSelection();
    }

    public void RefreshSelectionFromContainer()
    {
        OnContainerChangedRefreshSelection();
    }
    private void Update()
    {
        HandleInput();
        UpdatePreview();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Input ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void HandleInput()
    {
        // Toggle mode
        if (Input.GetKeyDown(modeToggleKey))
        {
            ToggleMode();
        }

        // Switch ammo (only in Build mode)
        if (Input.GetKeyDown(switchKey) && currentMode == WeaponMode.Build)
        {
            SwitchToNextAmmo();
        }

        // Rotate (only in Build mode)
        if (Input.GetKeyDown(rotateKey) && currentMode == WeaponMode.Build
            && curBuildableProperty != null && curBuildableProperty.canRotate)
        {
            currentRotationStep = (currentRotationStep + 1) % 4;
        }

        // Primary action
        if (Input.GetMouseButtonDown(primaryMouseButton))
        {
            if (currentMode == WeaponMode.Build)
                TryShoot();
            else
                TryRecycle();
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Mode ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    public void ToggleMode()
    {
        SetMode(currentMode == WeaponMode.Build ? WeaponMode.Recycle : WeaponMode.Build);
    }

    public void SetMode(WeaponMode mode)
    {
        if (currentMode == mode) return;
        currentMode = mode;

        // Force hide preview on mode switch so the correct preview type rebuilds
        if (previewController != null)
            previewController.HidePreview();

        OnWeaponModeChanged?.Invoke(currentMode);

        if (enableDebug)
            Debug.Log($"[WeaponBehaviour] Mode switched to {currentMode}");
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Ammo Switching ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Switch to the next non-empty ammo type in the container.
    /// Cycles through slots starting after the current one.
    /// </summary>
    public void SwitchToNextAmmo()
    {
        if (container == null) return;

        Key_BuildablePP next = container.CycleNextEnum(curBuildableEnum);
        SelectAmmo(next);
    }

    /// <summary>
    /// Directly select a specific ammo type.
    /// </summary>
    public void SelectAmmo(Key_BuildablePP buildableEnum)
    {
        if (EqualityComparer<Key_BuildablePP>.Default.Equals(curBuildableEnum, buildableEnum))
            return;

        curBuildableEnum = buildableEnum;
        currentRotationStep = 0;

        if (buildableEnum.Equals(Key_BuildablePP.None) || buildableDB == null)
        {
            curBuildableProperty = null;
        }
        else
        {
            curBuildableProperty = buildableDB.GetByEnum(buildableEnum);
            if (curBuildableProperty == null)
                Debug.LogWarning($"[WeaponBehaviour] No BuildableProperty found for '{buildableEnum}'.");
        }

        OnWeaponChanged?.Invoke(curBuildableEnum);

        if (enableDebug)
            Debug.Log($"[WeaponBehaviour] Selected ammo: {curBuildableEnum}" +
                      (curBuildableProperty != null ? $" ({curBuildableProperty.displayName})" : ""));
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Shooting ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void TryShoot()
    {
        if (curBuildableProperty == null) return;
        if (shootProvider == null || !shootProvider.HasValidHit) return;

        // Snapshot current selection because container callbacks may change current fields
        // during ammo consumption (e.g. last item removed -> auto-select None/other).
        Key_BuildablePP selectedEnum = curBuildableEnum;
        BuildableProperty selectedProperty = curBuildableProperty;
        int selectedRotationStep = currentRotationStep;

        WeaponHitResult hit = shootProvider.CurrentHitResult;
        EnemyGridBehaviour enemy = hit.HitEnemyGridBehaviour;
        if (enemy == null) return;

        Vector3Int anchorCell = hit.HitCell;

        // Check placement
        if (!enemy.Grid.CanPlace(selectedProperty, anchorCell, selectedRotationStep))
        {
            if (enableDebug)
                Debug.Log($"[WeaponBehaviour] Cannot place '{selectedEnum}' at cell {anchorCell}.");
            return;
        }

        // Consume ammo from container
        if (!container.TryRemoveItem(selectedEnum, 1, out string removeReason))
        {
            if (enableDebug)
                Debug.Log($"[WeaponBehaviour] No ammo left for '{selectedEnum}': {removeReason}");
            return;
        }

        // Place on enemy grid
        bool placed = enemy.TryPlace(selectedProperty, anchorCell, selectedRotationStep, out GameObject spawnedObj);
        if (!placed)
        {
            // Rollback ammo consumption on unexpected failure
            container.TryAddItem(selectedEnum, 1, out _);
            Debug.LogError($"[WeaponBehaviour] TryPlace failed unexpectedly at cell {anchorCell} after CanPlace succeeded.");
            return;
        }

        if (enableDebug)
            Debug.Log($"[WeaponBehaviour] Placed '{selectedEnum}' on '{enemy.gameObject.name}' at cell {anchorCell}.");
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Recycling ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void TryRecycle()
    {
        if (shootProvider == null || !shootProvider.HasValidHit) return;

        WeaponHitResult hit = shootProvider.CurrentHitResult;
        BuildableBehaviour buildable = hit.HitBuildable;
        if (buildable == null || buildable.Data == null) return;

        PlacedBuildableData data = buildable.Data;
        BuildableProperty prop = data.Property;

        // Check canMove flag
        if (!prop.canMove)
        {
            if (enableDebug)
                Debug.Log($"[WeaponBehaviour] Cannot recycle '{prop.EnumKey}': canMove is false.");
            return;
        }

        // Find the enemy grid this buildable belongs to
        EnemyGridBehaviour enemy = buildable.GetComponentInParent<EnemyGridBehaviour>();
        if (enemy == null)
        {
            if (enableDebug)
                Debug.Log($"[WeaponBehaviour] Cannot recycle '{prop.EnumKey}': no EnemyGridBehaviour found in parents.");
            return;
        }

        // Check container capacity before removing
        Key_BuildablePP recycleEnum = prop.EnumKey;
        if (!container.TryAddItem(recycleEnum, 1, out string addReason))
        {
            if (enableDebug)
                Debug.Log($"[WeaponBehaviour] Cannot recycle '{recycleEnum}': container full. {addReason}");
            return;
        }

        // Remove from enemy grid (also destroys the GameObject)
        if (!enemy.TryRemove(data.InstanceId))
        {
            // Rollback container add
            container.TryRemoveItem(recycleEnum, 1, out _);
            Debug.LogError($"[WeaponBehaviour] TryRemove failed for '{data.InstanceId}' after container add succeeded.");
            return;
        }

        if (enableDebug)
            Debug.Log($"[WeaponBehaviour] Recycled '{recycleEnum}' from '{enemy.gameObject.name}'. Returned to container.");
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Preview ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void UpdatePreview()
    {
        if (previewController == null) return;

        if (shootProvider == null)
        {
            previewController.HidePreview();
            return;
        }

        if (currentMode == WeaponMode.Build)
        {
            previewController.UpdateBuildPreview(
                shootProvider.HasValidHit,
                shootProvider.CurrentHitResult,
                curBuildableProperty,
                currentRotationStep);
        }
        else
        {
            previewController.UpdateRecyclePreview(
                shootProvider.HasValidHit,
                shootProvider.CurrentHitResult);
        }
    }
}
