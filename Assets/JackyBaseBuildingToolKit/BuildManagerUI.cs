using UnityEngine;
using JackyUtility;

/// <summary>
/// Presents buildable items from the global inventory and forwards selections to BuildManager.
/// </summary>
public class BuildManagerUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BuildManager buildManager;
    [SerializeField] private UI_Container uiContainer;
    [SerializeField] private BuildItemInfoPanel buildItemInfoPanel;

    [Header("Build Mode Buttons")]
    [SerializeField] private UnityEngine.UI.Button enterBuildButton;
    [SerializeField] private UnityEngine.UI.Button exitBuildButton;

    private ItemDefinitionDatabase itemDatabase;
    private SContainerViewSource<InventorySlot, Key_ItemDefinitionPP> source;
    private FilteredContainerView<Key_ItemDefinitionPP> view;

    private void Start()
    {
        PropertyDatabaseManager databaseManager = PropertyDatabaseManager.Instance;
        if (databaseManager == null)
        {
            Debug.LogWarning("[BuildManagerUI] PropertyDatabaseManager not found.");
            return;
        }

        itemDatabase = databaseManager.GetDatabase<ItemDefinitionDatabase>();
        if (itemDatabase == null)
        {
            Debug.LogWarning("[BuildManagerUI] ItemDefinitionDatabase not found.");
            return;
        }

        InventoryManager inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            Debug.LogWarning("[BuildManagerUI] InventoryManager not found.");
            return;
        }

        if (buildManager == null)
        {
            Debug.LogWarning("[BuildManagerUI] BuildManager reference is missing.");
            return;
        }

        source = new SContainerViewSource<InventorySlot, Key_ItemDefinitionPP>(inventoryManager.Inventory);
        view = new FilteredContainerView<Key_ItemDefinitionPP>(
            source,
            key => itemDatabase.GetByEnum(key),
            new FurnitureTagFilter(FurnitureTag.None));
        view.OnViewChanged += RefreshUI;

        if (uiContainer != null)
            uiContainer.OnSelectionChanged += OnSlotSelected;

        buildManager.OnBuildModeChanged += HandleBuildModeChanged;
        buildManager.OnActionCancelled += HandleActionCancelled;

        if (enterBuildButton != null)
            enterBuildButton.onClick.AddListener(OnEnterBuildButtonClicked);
        if (exitBuildButton != null)
            exitBuildButton.onClick.AddListener(OnExitBuildButtonClicked);

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (view != null)
            view.OnViewChanged -= RefreshUI;
        view?.Dispose();
        source?.Dispose();

        if (uiContainer != null)
            uiContainer.OnSelectionChanged -= OnSlotSelected;

        if (buildManager != null)
        {
            buildManager.OnBuildModeChanged -= HandleBuildModeChanged;
            buildManager.OnActionCancelled -= HandleActionCancelled;
        }

        if (enterBuildButton != null)
            enterBuildButton.onClick.RemoveListener(OnEnterBuildButtonClicked);
        if (exitBuildButton != null)
            exitBuildButton.onClick.RemoveListener(OnExitBuildButtonClicked);
    }

    public void ApplyFilter(FurnitureTag tag)
    {
        view?.SetFilter(new FurnitureTagFilter(tag));
    }

    public void ClearFilter()
    {
        view?.SetFilter(new FurnitureTagFilter(FurnitureTag.None));
    }

    private void OnEnterBuildButtonClicked()
    {
        if (buildManager != null)
            AllUIManager.Instance?.RequestOpen(buildManager, PanelOpenType.Override);
    }

    private void OnExitBuildButtonClicked()
    {
        if (buildManager != null)
            AllUIManager.Instance?.RequestClose(buildManager);
    }

    private void HandleBuildModeChanged(bool entering)
    {
        if (entering)
        {
            uiContainer?.Open();
            buildItemInfoPanel?.Open();
            RefreshUI();
        }
        else
        {
            uiContainer?.Close();
            buildItemInfoPanel?.Close();
        }
    }

    private void HandleActionCancelled()
    {
        uiContainer?.ClearSelection();
        buildItemInfoPanel?.ShowEmpty();
    }

    private void OnSlotSelected(int index)
    {
        if (index < 0)
        {
            buildItemInfoPanel?.ShowEmpty();
            return;
        }

        if (buildManager == null || !buildManager.IsBuildModeActive || view == null)
            return;

        if (!view.TryGetKeyAtIndex(index, out Key_ItemDefinitionPP itemKey))
            return;

        ItemDefinitionSO item = itemDatabase.GetByEnum(itemKey);
        if (item != null)
            buildItemInfoPanel?.Show(item);

        buildManager.SelectBuildableItem(itemKey);
    }

    private void RefreshUI()
    {
        if (view == null || uiContainer == null)
            return;

        SlotDisplayData[] data = view.GetDisplayData();
        uiContainer.InitSlots(data.Length);
        uiContainer.Refresh(data);
    }
}