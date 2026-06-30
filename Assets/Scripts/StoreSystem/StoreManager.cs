using System;
using UnityEngine;
using UnityEngine.UI;
using JackyUtility;

/// <summary>
/// Central manager for the store system.
/// Implements <see cref="IGeneralPanelOwner"/> so the store panel participates
/// in the centralised UI stack managed by <see cref="AllUIManager"/>.
///
/// Responsibilities:
///   - Own the runtime <see cref="StoreContainer"/> copied from <see cref="StoreInventorySO"/>.
///   - Build <see cref="SlotDisplayData"/> arrays that encode item state
///     (Default / SoldOut / Empty / Locked) for <see cref="UI_StoreContainer"/>.
///   - Show selected item details without purchasing on click.
///   - Confirm purchases with Enter and add bought items to the build container.
/// </summary>
public class StoreManager : MonoBehaviour, IGeneralPanelOwner
{
    public static StoreManager Instance { get; private set; }

    // --- Inspector ---

    [Header("Data")]
    [Tooltip("ScriptableObject that defines the fixed slot list for this store.")]
    [SerializeField] private StoreInventorySO inventory;

    [Header("References")]
    [SerializeField] private UI_StoreContainer uiContainer;
    [SerializeField] private StoreItemDetailPanelUI detailPanel;

    [Header("Open Button")]
    [Tooltip("Button in the HUD that requests the store to open via AllUIManager.")]
    [SerializeField] private Button enterStoreButton;

    [Header("Currency")]
    [Tooltip("Currency used for all purchases in this store.")]
    [SerializeField] private CurrencyType purchaseCurrency = CurrencyType.Credits;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled;

    // --- Runtime state ---

    private BuildableDatabase _db;
    private StoreContainer _runtimeStoreContainer;
    private bool _isStoreOpen;

    /// <summary>
    /// Fired when the store panel opens (true) or closes (false).
    /// </summary>
    public event Action<bool> OnStoreModeChanged;

    // --- Lifecycle ---

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        var dbManager = PropertyDatabaseManager.Instance;
        if (dbManager != null)
            _db = dbManager.GetDatabase<BuildableDatabase>();

        if (_db == null)
            Debug.LogWarning("[StoreManager] BuildableDatabase not found via PropertyDatabaseManager.");

        InitRuntimeInventory();

        if (uiContainer != null)
            uiContainer.OnSelectionChanged += HandleSlotSelected;

        if (enterStoreButton != null)
            enterStoreButton.onClick.AddListener(OnEnterStoreButtonClicked);

        detailPanel?.ShowEmpty();
    }

    private void Update()
    {
        if (!_isStoreOpen) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ConfirmSelectedPurchase();
    }

    private void OnDestroy()
    {
        if (uiContainer != null)
            uiContainer.OnSelectionChanged -= HandleSlotSelected;

        if (enterStoreButton != null)
            enterStoreButton.onClick.RemoveListener(OnEnterStoreButtonClicked);
    }

    // --- IGeneralPanelOwner ---

    public void OnPanelOpenRequested()
    {
        _isStoreOpen = true;

        uiContainer?.Open();
        uiContainer?.InitSlots(_runtimeStoreContainer != null ? _runtimeStoreContainer.MaxSlots : 0);
        RefreshUI();
        SelectFirstPurchasableSlot();
        OnStoreModeChanged?.Invoke(true);

        if (debugEnabled)
            Debug.Log("[StoreManager] Store opened.");
    }

    public void OnPanelCloseRequested()
    {
        _isStoreOpen = false;

        uiContainer?.ClearSelection();
        detailPanel?.ShowEmpty();
        uiContainer?.Close();
        OnStoreModeChanged?.Invoke(false);

        if (debugEnabled)
            Debug.Log("[StoreManager] Store closed.");
    }

    // --- Public API ---

    /// <summary>
    /// Filters are disabled for the current store flow.
    /// This no-op keeps older filter tab bindings compiling.
    /// </summary>
    public void ApplyFilter(FurnitureTag tag)
    {
        if (debugEnabled)
            Debug.Log($"[StoreManager] Store filters are disabled. Ignored filter: {tag}");
    }

    /// <summary>
    /// Attempt to purchase the item in slot <paramref name="slotIndex"/>.
    /// Deducts the item price from <see cref="EconomyManager"/> and decrements
    /// the runtime stock.  Returns false if the purchase could not be completed.
    /// </summary>
    public bool TryPurchase(int slotIndex)
    {
        if (!TryGetSlotAndProperty(slotIndex, out StoreSlot slot, out BuildableProperty prop))
            return false;

        if (slot.isLocked)
        {
            if (debugEnabled)
                Debug.Log($"[StoreManager] Slot {slotIndex} is locked.");
            return false;
        }

        if (slot.ItemCount <= 0)
        {
            if (debugEnabled)
                Debug.Log($"[StoreManager] Slot {slotIndex} is sold out.");
            return false;
        }

        if (EconomyManager.Instance == null)
        {
            Debug.LogWarning("[StoreManager] EconomyManager not found.");
            return false;
        }

        Container<Key_BuildablePP> buildContainer = BuildManager.Instance != null
            ? BuildManager.Instance.BuildableContainer
            : null;

        if (!CanAddToBuildContainer(buildContainer, slot.ItemEnum, out string failReason))
        {
            if (debugEnabled)
                Debug.Log($"[StoreManager] Buildable container cannot accept {slot.ItemEnum}: {failReason}");
            return false;
        }

        float price = prop.storePrice;
        if (!EconomyManager.Instance.TrySpend(purchaseCurrency, price))
        {
            if (debugEnabled)
                Debug.Log($"[StoreManager] Not enough {purchaseCurrency} to buy {prop.displayName} ({price}).");
            return false;
        }

        if (!_runtimeStoreContainer.TryRemoveCountAtIndex(slotIndex, 1, out failReason))
        {
            EconomyManager.Instance.AddCurrency(purchaseCurrency, price);

            if (debugEnabled)
                Debug.Log($"[StoreManager] Purchase failed at slot {slotIndex}: {failReason}");
            return false;
        }

        if (!buildContainer.TryAddItem(slot.ItemEnum, 1, out failReason))
        {
            EconomyManager.Instance.AddCurrency(purchaseCurrency, price);
            _runtimeStoreContainer.TryAddCountAtIndex(slotIndex, 1, out _);

            Debug.LogWarning($"[StoreManager] Purchased {slot.ItemEnum}, but failed to add it to build container: {failReason}");
            RefreshUI();
            RefreshSelectedDetail();
            return false;
        }

        if (debugEnabled)
            Debug.Log($"[StoreManager] Purchased {prop.displayName}. Stock remaining: {slot.ItemCount}");

        RefreshUI();
        RefreshSelectedDetail();
        return true;
    }

    // --- Private ---

    private void InitRuntimeInventory()
    {
        if (inventory == null)
        {
            _runtimeStoreContainer = new StoreContainer(0);
            return;
        }

        _runtimeStoreContainer = inventory.CreateRuntimeContainer();
    }

    private void RefreshUI()
    {
        if (uiContainer == null || _runtimeStoreContainer == null) return;

        SlotDisplayData[] data = BuildDisplayData();
        uiContainer.Refresh(data);
    }

    private SlotDisplayData[] BuildDisplayData()
    {
        int count = _runtimeStoreContainer != null ? _runtimeStoreContainer.MaxSlots : 0;
        SlotDisplayData[] result = new SlotDisplayData[count];

        for (int i = 0; i < count; i++)
        {
            StoreSlot slot = _runtimeStoreContainer.GetSlotByIndex(i);
            if (slot == null || slot.ItemEnum == Key_BuildablePP.None)
            {
                result[i] = SlotDisplayData.Empty;
                continue;
            }

            // Locked slot — always shown as locked regardless of filter
            if (slot.isLocked)
            {
                result[i] = new SlotDisplayData(null, Color.clear, 0, "", SlotState.Locked);
                continue;
            }

            BuildableProperty prop = _db?.GetByEnum(slot.ItemEnum);
            if (prop == null)
            {
                result[i] = SlotDisplayData.Empty;
                continue;
            }

            int stock = slot.ItemCount;

            if (stock > 0)
            {
                // Available: show icon + name + stock count + price as label
                string priceLabel = $"{prop.storePrice:F0} {purchaseCurrency}";
                result[i] = new SlotDisplayData(prop.iconSprite, Color.white, stock, priceLabel, SlotState.Default);
            }
            else
            {
                // Sold out: show icon + name but state = SoldOut
                result[i] = new SlotDisplayData(prop.iconSprite, Color.white, 0, prop.displayName, SlotState.SoldOut);
            }
        }

        return result;
    }

    private void HandleSlotSelected(int slotIndex)
    {
        ShowSlotDetail(slotIndex);
    }

    private void ConfirmSelectedPurchase()
    {
        if (uiContainer == null || !uiContainer.HasSelection) return;

        TryPurchase(uiContainer.SelectedSlotIndex);
    }

    private void SelectFirstPurchasableSlot()
    {
        if (uiContainer == null || _runtimeStoreContainer == null)
        {
            detailPanel?.ShowEmpty();
            return;
        }

        for (int i = 0; i < _runtimeStoreContainer.MaxSlots; i++)
        {
            if (IsPurchasableSlot(i))
            {
                uiContainer.SetSelection(i);
                return;
            }
        }

        uiContainer.ClearSelection();
        detailPanel?.ShowEmpty();
    }

    private bool IsPurchasableSlot(int slotIndex)
    {
        if (!TryGetSlotAndProperty(slotIndex, out StoreSlot slot, out _))
            return false;

        return !slot.isLocked && slot.ItemCount > 0;
    }

    private void ShowSlotDetail(int slotIndex)
    {
        if (!TryGetSlotAndProperty(slotIndex, out StoreSlot slot, out BuildableProperty prop) || slot.isLocked)
        {
            detailPanel?.ShowEmpty();
            return;
        }

        detailPanel?.Show(prop, purchaseCurrency);
    }

    private void RefreshSelectedDetail()
    {
        if (uiContainer == null || !uiContainer.HasSelection)
        {
            detailPanel?.ShowEmpty();
            return;
        }

        ShowSlotDetail(uiContainer.SelectedSlotIndex);
    }

    private bool TryGetSlotAndProperty(int slotIndex, out StoreSlot slot, out BuildableProperty prop)
    {
        slot = null;
        prop = null;

        if (_runtimeStoreContainer == null) return false;
        if (slotIndex < 0 || slotIndex >= _runtimeStoreContainer.MaxSlots) return false;

        slot = _runtimeStoreContainer.GetSlotByIndex(slotIndex);
        if (slot == null || slot.ItemEnum == Key_BuildablePP.None) return false;

        prop = _db?.GetByEnum(slot.ItemEnum);
        if (prop == null)
        {
            Debug.LogWarning($"[StoreManager] No BuildableProperty found for key {slot.ItemEnum}.");
            return false;
        }

        return true;
    }

    private bool CanAddToBuildContainer(Container<Key_BuildablePP> buildContainer, Key_BuildablePP itemKey, out string failReason)
    {
        failReason = null;

        if (buildContainer == null)
        {
            failReason = "BuildableContainer is not ready.";
            return false;
        }

        if (itemKey == Key_BuildablePP.None)
        {
            failReason = "Item key is None.";
            return false;
        }

        var slots = buildContainer.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!slots[i].IsEmpty && slots[i].ItemEnum.Equals(itemKey))
                return true;
        }

        if (buildContainer.FreeSlots > 0)
            return true;

        failReason = "No empty slot or matching stack available.";
        return false;
    }

    private void OnEnterStoreButtonClicked()
    {
        AllUIManager.Instance?.RequestOpen(this, PanelOpenType.Override);
    }
}
