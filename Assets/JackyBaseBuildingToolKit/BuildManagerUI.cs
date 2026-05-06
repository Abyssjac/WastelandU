using System;
using UnityEngine;
using JackyUtility;

/// <summary>
/// UI counterpart of <see cref="BuildManager"/>.
/// Owns the <see cref="FilteredContainerView{TEnum}"/> for the build container,
/// drives <see cref="UI_Container"/> and <see cref="BuildItemInfoPanel"/>,
/// and forwards slot selections to <see cref="BuildManager.SelectBuildable"/>.
///
/// Place on the same GameObject as, or alongside, the Canvas root.
/// </summary>
public class BuildManagerUI : MonoBehaviour
{
    // ©¤©¤ Inspector ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Header("References")]
    [SerializeField] private BuildManager        buildManager;
    [SerializeField] private UI_Container        uiContainer;
    [SerializeField] private BuildItemInfoPanel  buildItemInfoPanel;

    // ©¤©¤ Runtime ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private BuildableDatabase                          _db;
    private ContainerViewSource<Key_BuildablePP>       _source;
    private FilteredContainerView<Key_BuildablePP>     _view;

    // ©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void Start()
    {
        var dbManager = PropertyDatabaseManager.Instance;
        if (dbManager == null)
        {
            Debug.LogWarning("[BuildManagerUI] PropertyDatabaseManager not found.");
            return;
        }

        _db = dbManager.GetDatabase<BuildableDatabase>();
        if (_db == null)
        {
            Debug.LogWarning("[BuildManagerUI] BuildableDatabase not found.");
            return;
        }

        if (buildManager == null || buildManager.BuildableContainer == null)
        {
            Debug.LogWarning("[BuildManagerUI] BuildManager or its BuildableContainer is not ready.");
            return;
        }

        _source = new ContainerViewSource<Key_BuildablePP>(buildManager.BuildableContainer);
        _view   = new FilteredContainerView<Key_BuildablePP>(_source, key => _db.GetByEnum(key));

        _view.OnViewChanged += RefreshUI;

        if (uiContainer != null)
            uiContainer.OnSelectionChanged += OnSlotSelected;

        buildManager.OnBuildModeChanged += HandleBuildModeChanged;
        buildManager.OnActionCancelled  += HandleActionCancelled;

        RefreshUI();
    }

    private void OnDestroy()
    {
        _view?.Dispose();
        _source?.Dispose();

        if (uiContainer != null)
            uiContainer.OnSelectionChanged -= OnSlotSelected;

        if (buildManager != null)
        {
            buildManager.OnBuildModeChanged -= HandleBuildModeChanged;
            buildManager.OnActionCancelled  -= HandleActionCancelled;
        }
    }

    // ©¤©¤ Public Filter API ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Apply a <see cref="FurnitureTag"/> filter to the container view.
    /// Pass <see cref="FurnitureTag.None"/> to show all items.
    /// Wire this to a <see cref="BuildFilterTabUI"/> or call it directly.
    /// </summary>
    public void ApplyFilter(FurnitureTag tag)
    {
        _view?.SetFilter(tag == FurnitureTag.None
            ? null
            : (IContainerFilter<Key_BuildablePP>) new FurnitureTagFilter(tag));
    }

    /// <summary>Remove any active filter and show all items.</summary>
    public void ClearFilter()
    {
        _view?.SetFilter(null);
    }

    // ©¤©¤ Private ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void HandleBuildModeChanged(bool entering)
    {
        if (entering)
        {
            uiContainer?.Open();
            buildItemInfoPanel?.Open();
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

        if (buildManager == null || !buildManager.IsBuildModeActive) return;

        if (_view == null || !_view.TryGetKeyAtIndex(index, out Key_BuildablePP key)) return;

        BuildableProperty prop = _db?.GetByEnum(key);
        if (prop != null)
            buildItemInfoPanel?.Show(prop);

        buildManager.SelectBuildable(key);
    }

    private void RefreshUI()
    {
        if (_view == null || uiContainer == null) return;

        SlotDisplayData[] data = _view.GetDisplayData();
        uiContainer.InitSlots(data.Length);
        uiContainer.Refresh(data);
    }
}
