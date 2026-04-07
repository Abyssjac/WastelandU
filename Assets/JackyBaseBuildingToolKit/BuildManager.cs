using UnityEngine;
using JackyUtility;
using System.Collections.Generic;
/// <summary>
/// Central manager for the base-building system.
/// Owns the BuildGrid3D and orchestrates place / move / remove flow.
/// Supports layered placement (World ¡ú Platform ¡ú Room) with parent-child relationships.
/// Optionally bridges with a Container of buildable items so that
/// selecting a slot triggers placement and confirming a build consumes items.
/// </summary>
public class BuildManager : MonoBehaviour, IDebuggable
{
    public static BuildManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private BuildPositionProvider positionProvider;
    [SerializeField] private BuildPreviewController previewController;

    [Header("Grid Bounds (in cells)")]
    [SerializeField] private Vector3Int gridMin = new Vector3Int(-50, -1, -50);
    [SerializeField] private Vector3Int gridMax = new Vector3Int(50, 10, 50);

    [Header("Container Integration")]
    [SerializeField] private UI_Container uiContainer;
    [SerializeField] private int containerSlotCount = 6;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;

    // ---- IDebuggable ----
    public string DebugId => "buildmgr";
    public bool DebugEnabled
    {
        get => enableDebug;
        set => enableDebug = value;
    }

    [SerializeField] private BuildGrid3D grid;
    public BuildGrid3D Grid => grid;
    public BuildState CurrentState { get; private set; } = BuildState.Idle;

    // currently active context
    private BuildableProperty selectedProperty;
    private int currentRotationStep;
    private PlacedBuildableData movingData;   // non-null when in Moving state
    private List<PlacedBuildableData> movingChildren; // children being moved along with movingData

    private int instanceCounter;

    // ©¤©¤©¤ Container Integration ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private Container<Key_ContainerItemPP> container;
    private ContainerPropertyLookup<ContainerItemProperty, Key_ContainerItemPP> containerLookup;
    private ContainerItemDatabase containerItemDB;
    private BuildableDatabase buildableDB;

    /// <summary>The build container. Null if databases are missing.</summary>
    public Container<Key_ContainerItemPP> Container => container;

    // Tracks which slot / action triggered the current placement so we can consume items on confirm.
    private int pendingSlotIndex = -1;
    private ContainerItemBuildAction pendingBuildAction;

    // debug cache (updated every frame for GUI display)
    private string debugCanPlaceReason = "";
    private bool debugCanPlace;

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        grid = new BuildGrid3D(gridMin, gridMax);
        grid.Initialize();

        InitContainer();
    }

    private void Start()
    {
        DebugConsoleManager.Instance.RegisterDebugTarget(this);
        RegisterDebugCommands();
    }

    private void OnDestroy()
    {
        if (DebugConsoleManager.Instance != null)
            DebugConsoleManager.Instance.UnregisterDebugTarget(this);

        if (uiContainer != null)
            uiContainer.OnSelectionChanged -= OnContainerSelectionChanged;

        containerLookup?.UnBindUIContainer();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Container Init ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void InitContainer()
    {
        var dbManager = PropertyDatabaseManager.Instance;
        if (dbManager == null) return;

        containerItemDB = dbManager.GetDatabase<ContainerItemDatabase>();
        buildableDB = dbManager.GetDatabase<BuildableDatabase>();

        if (containerItemDB == null || buildableDB == null)
        {
            Debug.LogWarning("[BuildManager] ContainerItemDatabase or BuildableDatabase not found. Container integration disabled.");
            return;
        }

        container = new Container<Key_ContainerItemPP>(containerSlotCount, key =>
        {
            var prop = containerItemDB.GetByEnum(key);
            return prop != null ? prop.maxStackCount : int.MaxValue;
        });

        containerLookup = new ContainerPropertyLookup<ContainerItemProperty, Key_ContainerItemPP>(container, containerItemDB);

        if (uiContainer != null)
        {
            containerLookup.BindUIContainer(uiContainer);
            uiContainer.OnSelectionChanged += OnContainerSelectionChanged;
        }
    }

    private void OnContainerSelectionChanged(int slotIndex)
    {
        if (slotIndex < 0)
        {
            // Deselected ¡ª cancel if we were placing from container
            if (CurrentState == BuildState.Placing && pendingSlotIndex >= 0)
                CancelCurrentAction();
            return;
        }

        SelectSlotForBuild(slotIndex);
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Public API ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Called from UI when the player picks a buildable to place.
    /// </summary>
    public void BeginPlacing(BuildableProperty property)
    {
        if (property == null) return;

        selectedProperty = property;
        currentRotationStep = 0;
        CurrentState = BuildState.Placing;

        previewController.ShowPreview(selectedProperty, currentRotationStep);
    }

    /// <summary>
    /// Called when the player selects a container slot to build.
    /// Resolves the slot ¡ú ContainerItemProperty ¡ú BuildAction ¡ú BuildableProperty chain,
    /// then enters Placing state. On confirm the item is consumed from the container.
    /// </summary>
    public bool SelectSlotForBuild(int slotIndex)
    {
        if (containerLookup == null || buildableDB == null)
        {
            Debug.LogWarning("[BuildManager] Container integration not initialised.");
            return false;
        }

        // 1. Get the ContainerItemProperty for this slot
        var itemProp = containerLookup.GetPropertyByIndex(slotIndex);
        if (itemProp == null)
        {
            Debug.LogWarning($"[BuildManager] Slot {slotIndex} is empty or has no property.");
            return false;
        }

        // 2. Check if this item has a build action
        if (!itemProp.TryGetAction<ContainerItemBuildAction>(out var buildAction))
        {
            Debug.LogWarning($"[BuildManager] Item '{itemProp.EnumKey}' has no ContainerItemBuildAction.");
            return false;
        }

        // 3. Resolve the BuildableProperty from the action's key
        var buildProp = buildableDB.GetByEnum(buildAction.buildableKey);
        if (buildProp == null)
        {
            Debug.LogWarning($"[BuildManager] No BuildableProperty found for key '{buildAction.buildableKey}'.");
            return false;
        }

        if (buildProp.prefab == null)
        {
            Debug.LogWarning($"[BuildManager] BuildableProperty '{buildAction.buildableKey}' has no prefab.");
            return false;
        }

        // 4. Cancel any in-progress action, then start placing
        if (CurrentState != BuildState.Idle)
            CancelCurrentAction();

        pendingSlotIndex = slotIndex;
        pendingBuildAction = buildAction;
        BeginPlacing(buildProp);
        return true;
    }

    /// <summary>
    /// Called from UI / hotkey to cancel current operation.
    /// </summary>
    public void CancelCurrentAction()
    {
        if (CurrentState == BuildState.Moving && movingData != null)
        {
            // rollback: re-place parent and all children at original positions
            Grid.TryMoveWithChildren(movingData.InstanceId, movingData.AnchorCell, movingData.RotationStep, out _);
            SetGameObjectActiveRecursive(movingData, true);
            movingData = null;
            movingChildren = null;
        }

        selectedProperty = null;
        pendingSlotIndex = -1;
        pendingBuildAction = null;
        CurrentState = BuildState.Idle;
        previewController.HidePreview();
        debugCanPlaceReason = "";

        if (uiContainer != null)
            uiContainer.ClearSelection();
    }

    /// <summary>
    /// Player wants to pick up and move an existing buildable (with its children).
    /// </summary>
    public void BeginMoving(PlacedBuildableData data)
    {
        if (data == null || !data.Property.canMove) return;

        movingData = data;
        currentRotationStep = data.RotationStep;

        // Collect children for display purposes
        movingChildren = new List<PlacedBuildableData>();
        CollectChildren(data, movingChildren);

        // temporarily remove from grid (parent + children) so own cells don't block new position
        for (int i = 0; i < movingChildren.Count; i++)
            Grid.ForceRemoveFromGrid(movingChildren[i]);
        Grid.ForceRemoveFromGrid(data);

        SetGameObjectActiveRecursive(data, false);

        CurrentState = BuildState.Moving;
        previewController.ShowPreview(data.Property, currentRotationStep);
    }

    /// <summary>
    /// Remove a buildable from the base entirely.
    /// Will fail if it has children ¡ª remove children first.
    /// </summary>
    public bool RemoveBuildable(PlacedBuildableData data)
    {
        if (data == null) return false;

        if (!Grid.TryRemove(data.InstanceId, out string reason))
        {
            Debug.LogWarning($"[BuildManager] Cannot remove: {reason}");
            return false;
        }

        if (data.SpawnedObject != null) Destroy(data.SpawnedObject);
        return true;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Update Loop ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void Update()
    {
        switch (CurrentState)
        {
            case BuildState.Idle:
                HandleIdleInput();
                break;
            case BuildState.Placing:
                HandlePlacingUpdate();
                break;
            case BuildState.Moving:
                HandleMovingUpdate();
                break;
        }
    }

    private void HandleIdleInput()
    {
        // left-click on an existing buildable to begin moving
        if (Input.GetMouseButtonDown(0) && positionProvider.HasValidHit)
        {
            PlacedBuildableData occupant = Grid.GetTopmostOccupant(positionProvider.CurrentCell);
            if (occupant != null && occupant.Property.canMove)
            {
                BeginMoving(occupant);
            }
        }
    }

    private void HandlePlacingUpdate()
    {
        if (!positionProvider.HasValidHit)
        {
            previewController.SetPreviewValid(false);
            debugCanPlace = false;
            debugCanPlaceReason = "No valid raycast hit";
            return;
        }

        // Rotate
        if (Input.GetKeyDown(KeyCode.R) && selectedProperty.canRotate)
        {
            currentRotationStep = (currentRotationStep + 1) % 4;
            previewController.UpdateRotation(currentRotationStep);
        }

        Vector3Int anchor = positionProvider.CurrentCell;
        debugCanPlace = Grid.CanPlace(selectedProperty, anchor, currentRotationStep, out string reason);
        debugCanPlaceReason = reason ?? "OK";

        Vector3Int[] footprintOffsets = selectedProperty.GetRotatedFootprint(currentRotationStep);

        previewController.UpdatePreviewPosition(
            positionProvider.CurrentSnappedWorldPositionCenter,
            anchor,
            footprintOffsets,
            debugCanPlace
        );

        // Confirm
        if (Input.GetMouseButtonDown(0) && debugCanPlace)
        {
            ConfirmPlace(anchor);
        }

        // Cancel
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            CancelCurrentAction();
        }
    }

    private void HandleMovingUpdate()
    {
        if (!positionProvider.HasValidHit)
        {
            previewController.SetPreviewValid(false);
            debugCanPlace = false;
            debugCanPlaceReason = "No valid raycast hit";
            return;
        }

        // Rotate
        if (Input.GetKeyDown(KeyCode.R) && movingData.Property.canRotate)
        {
            currentRotationStep = (currentRotationStep + 1) % 4;
            previewController.UpdateRotation(currentRotationStep);
        }

        Vector3Int anchor = positionProvider.CurrentCell;

        // For move preview validation we need to temporarily check
        // as if the moving set doesn't exist (they are already removed from grid)
        debugCanPlace = Grid.CanPlace(movingData.Property, anchor, currentRotationStep, out string reason);
        debugCanPlaceReason = reason ?? "OK";

        Vector3Int[] footprintOffsets = movingData.Property.GetRotatedFootprint(currentRotationStep);

        previewController.UpdatePreviewPosition(
            positionProvider.CurrentSnappedWorldPositionCenter,
            anchor,
            footprintOffsets,
            debugCanPlace
        );

        // Confirm move
        if (Input.GetMouseButtonDown(0) && debugCanPlace)
        {
            ConfirmMove(anchor);
        }

        // Cancel move ¡ª rollback
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            // Re-place everything at old positions for rollback
            Grid.ForcePlaceIntoGrid(movingData);
            for (int i = 0; i < movingChildren.Count; i++)
                Grid.ForcePlaceIntoGrid(movingChildren[i]);
            SetGameObjectActiveRecursive(movingData, true);

            movingData = null;
            movingChildren = null;
            CurrentState = BuildState.Idle;
            previewController.HidePreview();
            debugCanPlaceReason = "";
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Internal ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void ConfirmPlace(Vector3Int anchor)
    {
        // ©¤©¤ If this placement came from a container slot, consume the item first ©¤©¤
        if (pendingSlotIndex >= 0 && pendingBuildAction != null && container != null)
        {
            var slot = container.GetItemInfoByIndex(pendingSlotIndex);
            int cost = Mathf.Max(1, pendingBuildAction.costPerBuild);

            if (slot.IsEmpty || slot.ItemCount < cost)
            {
                Debug.LogWarning($"[BuildManager] Slot {pendingSlotIndex} no longer has enough items to build (need {cost}).");
                CancelCurrentAction();
                return;
            }

            if (!container.TryRemoveItem(slot.ItemEnum, cost, out string removeReason))
            {
                Debug.LogWarning($"[BuildManager] Failed to consume items: {removeReason}");
                CancelCurrentAction();
                return;
            }
        }

        PlacedBuildableData data = new PlacedBuildableData
        {
            InstanceId = $"build_{instanceCounter++}",
            Property = selectedProperty,
            AnchorCell = anchor,
            RotationStep = currentRotationStep,
        };

        if (!Grid.TryPlace(data))
        {
            Debug.LogError($"[BuildManager] TryPlace failed at {anchor} ¡ª should not happen after CanPlace check.");
            return;
        }

        // ©¤©¤ Establish parent-child relationship ©¤©¤
        if (selectedProperty.requiredSurface != BuildSurfaceType.None)
        {
            PlacedBuildableData parent = Grid.FindParentAt(anchor, selectedProperty.requiredSurface);
            if (parent != null)
            {
                data.SetParent(parent);
            }
        }

        // Spawn real object
        Vector3 worldPos = positionProvider.CellToWorldCenter(anchor);
        float yaw = selectedProperty.GetRotationDegrees(currentRotationStep);
        GameObject go = Instantiate(selectedProperty.prefab, worldPos, Quaternion.Euler(0f, yaw, 0f));
        data.SpawnedObject = go;

        // Parent the GameObject under the parent's GO for automatic transform following
        if (data.ParentId != null && Grid.AllPlaced.TryGetValue(data.ParentId, out PlacedBuildableData parentData))
        {
            if (parentData.SpawnedObject != null)
            {
                go.transform.SetParent(parentData.SpawnedObject.transform, worldPositionStays: true);
            }
        }

        previewController.HidePreview();
        CurrentState = BuildState.Idle;
        selectedProperty = null;
        pendingSlotIndex = -1;
        pendingBuildAction = null;
        debugCanPlaceReason = "";

        if (uiContainer != null)
            uiContainer.ClearSelection();
    }

    private void ConfirmMove(Vector3Int newAnchor)
    {
        Vector3Int delta = newAnchor - movingData.AnchorCell;

        // Place parent at new anchor
        movingData.AnchorCell = newAnchor;
        movingData.RotationStep = currentRotationStep;
        Grid.ForcePlaceIntoGrid(movingData);

        // Place children at shifted positions
        for (int i = 0; i < movingChildren.Count; i++)
        {
            movingChildren[i].AnchorCell += delta;
            Grid.ForcePlaceIntoGrid(movingChildren[i]);
        }

        // Update visual transforms
        SetGameObjectActiveRecursive(movingData, true);
        SyncGameObjectTransform(movingData);
        // Children follow automatically via Unity transform hierarchy,
        // but we still need to sync the grid data's anchor cells (already done above).

        previewController.HidePreview();
        movingData = null;
        movingChildren = null;
        CurrentState = BuildState.Idle;
        debugCanPlaceReason = "";
    }

    private void SyncGameObjectTransform(PlacedBuildableData data)
    {
        if (data.SpawnedObject == null) return;
        Vector3 worldPos = positionProvider.CellToWorldCenter(data.AnchorCell);
        float yaw = data.Property.GetRotationDegrees(data.RotationStep);
        data.SpawnedObject.transform.SetPositionAndRotation(worldPos, Quaternion.Euler(0f, yaw, 0f));
    }

    /// <summary>
    /// Show/hide a buildable and all its children's GameObjects.
    /// </summary>
    private void SetGameObjectActiveRecursive(PlacedBuildableData data, bool active)
    {
        if (data.SpawnedObject != null)
            data.SpawnedObject.SetActive(active);

        for (int i = 0; i < data.ChildrenIds.Count; i++)
        {
            if (Grid.AllPlaced.TryGetValue(data.ChildrenIds[i], out PlacedBuildableData child))
            {
                if (child.SpawnedObject != null)
                    child.SpawnedObject.SetActive(active);
            }
        }
    }

    /// <summary>
    /// Collect all direct + indirect children of a PlacedBuildableData.
    /// </summary>
    private void CollectChildren(PlacedBuildableData parent, List<PlacedBuildableData> result)
    {
        for (int i = 0; i < parent.ChildrenIds.Count; i++)
        {
            if (Grid.AllPlaced.TryGetValue(parent.ChildrenIds[i], out PlacedBuildableData child))
            {
                result.Add(child);
                CollectChildren(child, result);
            }
        }
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Debug Commands ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void RegisterDebugCommands()
    {
        if (DebugConsoleManager.Instance == null) return;

        DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
            "buildinfo",
            "Show build grid occupancy stats.",
            args =>
            {
                Debug.Log($"[BuildManager] Placed count: {Grid.AllPlaced.Count}, State: {CurrentState}");
                foreach (var kvp in Grid.AllPlaced)
                {
                    var d = kvp.Value;
                    Debug.Log($"  {d.InstanceId}: {d.Property.EnumKey} layer={d.Property.buildLayer} " +
                              $"anchor={d.AnchorCell} parent={d.ParentId ?? "none"} children={d.ChildrenIds.Count}");
                }
            }
        ));

        DebugConsoleManager.Instance.RegisterCommand(new DebugCommand(
            "buildmgr-select",
            "Enter placement mode by string key. Usage: buildmgr-select <stringKey>  |  buildmgr-select ls",
            args =>
            {
                if (args.Length == 0)
                {
                    Debug.LogWarning("[buildmgr-select] Usage: buildmgr-select <stringKey>  |  buildmgr-select ls");
                    return;
                }

                if (buildableDB == null)
                {
                    Debug.LogError("[buildmgr-select] BuildableDatabase not available.");
                    return;
                }

                // Sub-command: list all entries in the database
                if (args[0].Equals("ls", System.StringComparison.OrdinalIgnoreCase))
                {
                    var entries = buildableDB.Entries;
                    if (entries.Count == 0)
                    {
                        Debug.Log("[buildmgr-select] Database is empty.");
                        return;
                    }
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        Debug.Log($"  {e.StringKey} ¡ú {e.EnumKey}  layer={e.buildLayer}  required={e.requiredSurface}  provides={e.providedSurface}");
                    }
                    return;
                }

                // Look up by string key
                var prop = buildableDB.GetByString(args[0]);
                if (prop == null)
                {
                    Debug.LogWarning($"[buildmgr-select] No BuildableProperty found for key '{args[0]}'. Use 'buildmgr-select ls' to list all.");
                    return;
                }

                if (prop.prefab == null)
                {
                    Debug.LogWarning($"[buildmgr-select] BuildableProperty '{args[0]}' has no prefab assigned.");
                    return;
                }

                if (CurrentState != BuildState.Idle)
                    CancelCurrentAction();

                BeginPlacing(prop);
                Debug.Log($"[buildmgr-select] Entering placement mode: {prop.EnumKey} ({prop.displayName})  layer={prop.buildLayer}");
            }
        ));
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Debug GUI (Game view top-left) ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void OnGUI()
    {
        if (!enableDebug) return;

        var panel = DebugGUIPanel.Begin(new Vector2(10f, 10f), 420f, 16);

        panel.DrawLine("<b>¨T¨T¨T BuildManager Debug ¨T¨T¨T</b>");

        string stateColor = CurrentState == BuildState.Idle ? "white" :
                            CurrentState == BuildState.Placing ? "cyan" : "yellow";
        panel.DrawLine($"State: <color={stateColor}><b>{CurrentState}</b></color>");

        string propName = selectedProperty != null ? $"{selectedProperty.EnumKey} ({selectedProperty.displayName})"
                        : movingData != null ? $"{movingData.Property.EnumKey} (moving)"
                        : "None";
        panel.DrawLine($"Property: <color=orange>{propName}</color>");

        if (selectedProperty != null)
            panel.DrawLine($"Layer: {selectedProperty.buildLayer}  Required: {selectedProperty.requiredSurface}  Provides: {selectedProperty.providedSurface}");
        else if (movingData != null)
            panel.DrawLine($"Layer: {movingData.Property.buildLayer}  Children: {movingData.ChildrenIds.Count}");

        panel.DrawLine($"RotationStep: {currentRotationStep}  ({currentRotationStep * 90}¡ã)");

        panel.DrawLine($"HasValidHit: {positionProvider.HasValidHit}");
        if (positionProvider.HasValidHit)
        {
            panel.DrawLine($"HitWorld: {positionProvider.CurrentHitWorldPosition:F3}");
            panel.DrawLine($"Cell: {positionProvider.CurrentCell}");
            panel.DrawLine($"Snapped: {positionProvider.CurrentSnappedWorldPosition:F2}");
        }
        else
        {
            panel.DrawLine("HitWorld: ---");
            panel.DrawLine("Cell: ---");
            panel.DrawLine("Snapped: ---");
        }

        string canPlaceColor = debugCanPlace ? "lime" : "red";
        panel.DrawLine($"CanPlace: <color={canPlaceColor}><b>{debugCanPlace}</b></color>");
        panel.DrawLine($"Reason: <color={canPlaceColor}>{debugCanPlaceReason}</color>");

        panel.DrawLine($"Grid Bounds: {Grid.GridMin} ¡ú {Grid.GridMax}");
        panel.DrawLine($"Placed Objects: {Grid.AllPlaced.Count}");

        panel.End();
    }
}

public enum BuildState
{
    Idle,
    Selecting, // UI choosing which buildable
    Placing,   // dragging preview, about to place
    Moving,    // dragging an existing buildable to a new position
}