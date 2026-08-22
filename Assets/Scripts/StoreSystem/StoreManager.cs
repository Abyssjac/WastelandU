using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JackyUtility;

public enum StoreTradeMode
{
    Buy = 0,
    Sell = 1
}

/// <summary>
/// Runs Buy and Sell transactions between one store and the player's inventory.
/// </summary>
public class StoreManager : MonoBehaviour, IGeneralPanelOwner
{
    public static StoreManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private Key_StoreInventory inventoryKey = Key_StoreInventory.None;

    [Header("References")]
    [SerializeField] private UI_StoreContainer uiContainer;
    [SerializeField] private StoreItemDetailPanelUI detailPanel;

    [Header("Trade Tabs")]
    [SerializeField] private Button _buyTabButton;
    [SerializeField] private Button _sellTabButton;
    [SerializeField] private GameObject _buyTabSelectedVisual;
    [SerializeField] private GameObject _sellTabSelectedVisual;

    [Header("Open Button")]
    [SerializeField] private Button enterStoreButton;

    [Header("Currency")]
    [SerializeField] private CurrencyType purchaseCurrency = CurrencyType.Credits;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled;

    private ItemDefinitionDatabase itemDatabase;
    private SellableDatabase sellableDatabase;
    private StoreInventoryDatabase inventoryDatabase;
    private StoreInventoryProperty inventoryProperty;
    private StoreContainer runtimeStoreContainer;
    private bool isStoreOpen;
    private StoreTradeMode _currentTradeMode = StoreTradeMode.Buy;
    private InventoryManager _boundInventoryManager;
    private bool _isExecutingTransaction;

    // StoreInventoryProperty is immutable authored data. This dictionary holds
    // only the values that change while playing and are therefore saveable.
    private readonly Dictionary<Key_StoreInventory, Dictionary<Key_ItemDefinitionPP, StoreItemRuntimeState>> _storeRuntimeStates =
        new Dictionary<Key_StoreInventory, Dictionary<Key_ItemDefinitionPP, StoreItemRuntimeState>>();

    private sealed class StoreItemRuntimeState
    {
        public int RemainingCount;
        public bool IsLocked;
    }

    public event Action<bool> OnStoreModeChanged;
    public event Action<StoreTradeMode> OnTradeModeChanged;
    /// <summary>
    /// Fired once when a currently open store is fully closed. NPC interactions
    /// use this to release their owning Interact session.
    /// </summary>
    public event Action OnStoreClosed;

    public StoreTradeMode CurrentTradeMode => _currentTradeMode;

    /// <summary>
    /// Opens a store selected by an NPC interaction. The inventory property
    /// remains static; only its enum key is used to build this manager's runtime
    /// container for the current store session.
    /// </summary>
    public bool OpenStore(StoreInventoryProperty storeInventoryProperty)
    {
        if (storeInventoryProperty == null)
        {
            Debug.LogWarning($"[{nameof(StoreManager)}] Cannot open a null {nameof(StoreInventoryProperty)}.", this);
            return false;
        }

        return OpenStore(storeInventoryProperty.EnumKey);
    }

    /// <summary>Opens the configured UI with the requested inventory.</summary>
    public bool OpenStore(Key_StoreInventory storeInventoryKey)
    {
        if (storeInventoryKey == Key_StoreInventory.None)
        {
            Debug.LogWarning($"[{nameof(StoreManager)}] Cannot open store key None.", this);
            return false;
        }

        if (inventoryDatabase == null && PropertyDatabaseManager.Instance != null)
            inventoryDatabase = PropertyDatabaseManager.Instance.GetDatabase<StoreInventoryDatabase>();

        if (inventoryDatabase == null || inventoryDatabase.GetByEnum(storeInventoryKey) == null)
        {
            Debug.LogWarning($"[{nameof(StoreManager)}] Store inventory '{storeInventoryKey}' is not registered.", this);
            return false;
        }

        SynchronizeCurrentStoreRuntimeState();

        inventoryKey = storeInventoryKey;
        InitializeRuntimeInventory();

        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestOpen(this, PanelOpenType.Override);
        else
            OnPanelOpenRequested();

        return true;
    }

    /// <summary>Closes the currently open store without changing its static inventory configuration.</summary>
    public void CloseStore()
    {
        if (AllUIManager.Instance != null)
            AllUIManager.Instance.RequestClose(this);
        else if (isStoreOpen)
            OnPanelCloseRequested();
    }

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
        PropertyDatabaseManager databaseManager = PropertyDatabaseManager.Instance;
        if (databaseManager != null)
        {
            itemDatabase = databaseManager.GetDatabase<ItemDefinitionDatabase>();
            sellableDatabase = databaseManager.GetDatabase<SellableDatabase>();
            inventoryDatabase = databaseManager.GetDatabase<StoreInventoryDatabase>();
        }

        if (itemDatabase == null)
            Debug.LogWarning("[StoreManager] ItemDefinitionDatabase not found via PropertyDatabaseManager.");
        if (sellableDatabase == null)
            Debug.LogWarning("[StoreManager] SellableDatabase not found via PropertyDatabaseManager.");
        if (inventoryDatabase == null)
            Debug.LogWarning("[StoreManager] StoreInventoryDatabase not found via PropertyDatabaseManager.");

        InitializeRuntimeInventory();

        if (uiContainer != null)
            uiContainer.OnSelectionChanged += HandleSlotSelected;
        if (enterStoreButton != null)
            enterStoreButton.onClick.AddListener(OnEnterStoreButtonClicked);
        if (_buyTabButton != null)
            _buyTabButton.onClick.AddListener(ShowBuyTab);
        if (_sellTabButton != null)
            _sellTabButton.onClick.AddListener(ShowSellTab);

        detailPanel?.ShowEmpty();
        UpdateTradeTabVisuals();
    }

    private void Update()
    {
        if (!isStoreOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ConfirmSelectedTransaction();
    }

    private void OnDestroy()
    {
        SynchronizeCurrentStoreRuntimeState();

        if (uiContainer != null)
            uiContainer.OnSelectionChanged -= HandleSlotSelected;
        if (enterStoreButton != null)
            enterStoreButton.onClick.RemoveListener(OnEnterStoreButtonClicked);
        if (_buyTabButton != null)
            _buyTabButton.onClick.RemoveListener(ShowBuyTab);
        if (_sellTabButton != null)
            _sellTabButton.onClick.RemoveListener(ShowSellTab);
        UnbindInventoryChanges();
        if (Instance == this)
            Instance = null;
    }

    public void OnPanelOpenRequested()
    {
        isStoreOpen = true;
        uiContainer?.Open();
        _currentTradeMode = StoreTradeMode.Buy;
        BindInventoryChanges();
        UpdateTradeTabVisuals();
        RebuildTradeView();
        OnStoreModeChanged?.Invoke(true);
    }

    public void OnPanelCloseRequested()
    {
        bool wasStoreOpen = isStoreOpen;
        isStoreOpen = false;
        UnbindInventoryChanges();
        uiContainer?.ClearSelection();
        detailPanel?.ShowEmpty();
        uiContainer?.Close();
        OnStoreModeChanged?.Invoke(false);

        if (wasStoreOpen)
            OnStoreClosed?.Invoke();
    }

    public void ApplyFilter(FurnitureTag tag)
    {
        if (debugEnabled)
            Debug.Log("[StoreManager] Store filters are disabled. Ignored filter: " + tag);
    }

    public void ShowBuyTab()
    {
        SetTradeMode(StoreTradeMode.Buy);
    }

    public void ShowSellTab()
    {
        SetTradeMode(StoreTradeMode.Sell);
    }

    public void SetTradeMode(StoreTradeMode tradeMode)
    {
        if (_currentTradeMode == tradeMode)
        {
            UpdateTradeTabVisuals();
            return;
        }

        _currentTradeMode = tradeMode;
        BindInventoryChanges();
        UpdateTradeTabVisuals();

        if (isStoreOpen)
            RebuildTradeView();
    }

    public bool TryPurchase(int slotIndex)
    {
        if (!TryGetSlotAndProperties(slotIndex, out StoreSlot slot, out ItemDefinitionSO item, out SellableProperty sellable))
            return false;
        if (slot.isLocked || slot.ItemCount <= 0)
            return false;
        if (EconomyManager.Instance == null)
        {
            Debug.LogWarning("[StoreManager] EconomyManager not found.");
            return false;
        }

        InventoryManager inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            Debug.LogWarning("[StoreManager] InventoryManager not found.");
            return false;
        }

        if (!inventoryManager.CanAddItem(slot.ItemEnum, 1, out string failReason))
        {
            if (debugEnabled)
                Debug.Log("[StoreManager] Inventory cannot accept " + slot.ItemEnum + ": " + failReason);
            return false;
        }

        int price = ResolvePrice(slot.ItemEnum, sellable);
        if (!EconomyManager.Instance.TrySpend(purchaseCurrency, price))
        {
            if (debugEnabled)
                Debug.Log("[StoreManager] Not enough " + purchaseCurrency + " to buy " + item.DisplayName + ".");
            return false;
        }

        if (!runtimeStoreContainer.TryRemoveCountAtIndex(slotIndex, 1, out failReason))
        {
            EconomyManager.Instance.AddCurrency(purchaseCurrency, price);
            return false;
        }

        if (!inventoryManager.TryAddItem(slot.ItemEnum, 1, out failReason))
        {
            EconomyManager.Instance.AddCurrency(purchaseCurrency, price);
            runtimeStoreContainer.TryAddCountAtIndex(slotIndex, 1, out _);
            Debug.LogWarning("[StoreManager] Purchased item could not be added to inventory: " + failReason);
            RefreshCurrentTradeUI();
            RefreshSelectedDetail();
            return false;
        }

        if (debugEnabled)
            Debug.Log("[StoreManager] Purchased " + item.DisplayName + ". Stock remaining: " + slot.ItemCount);

        SynchronizeCurrentStoreRuntimeState();
        RefreshCurrentTradeUI();
        RefreshSelectedDetail();
        return true;
    }

    /// <summary>
    /// Sells one item from one exact physical inventory slot. Sold items are
    /// removed from the player and do not enter the merchant's Buy inventory.
    /// </summary>
    public bool TrySell(int inventorySlotIndex)
    {
        if (!TryGetInventorySlotAndProperties(inventorySlotIndex, out InventorySlot inventorySlot, out ItemDefinitionSO item, out SellableProperty sellable))
            return false;

        if (EconomyManager.Instance == null)
        {
            Debug.LogWarning("[StoreManager] EconomyManager not found.");
            return false;
        }

        InventoryManager inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            Debug.LogWarning("[StoreManager] InventoryManager not found.");
            return false;
        }

        int price = ResolvePrice(inventorySlot.ItemEnum, sellable);

        _isExecutingTransaction = true;
        try
        {
            if (!inventoryManager.TryRemoveItemAtSlot(inventorySlotIndex, inventorySlot.ItemEnum, 1, out string failReason))
            {
                if (debugEnabled)
                    Debug.Log("[StoreManager] Could not sell " + inventorySlot.ItemEnum + ": " + failReason);
                return false;
            }

            EconomyManager.Instance.AddCurrency(purchaseCurrency, price);
        }
        finally
        {
            _isExecutingTransaction = false;
        }

        if (debugEnabled)
            Debug.Log("[StoreManager] Sold " + item.DisplayName + " for " + price + " " + purchaseCurrency + ".");

        RefreshCurrentTradeUI();
        RefreshSelectedDetail();
        return true;
    }

    /// <summary>
    /// Captures all mutable inventory state. Static slot content and prices
    /// remain authored in StoreInventoryProperty and are intentionally omitted.
    /// </summary>
    public List<StoreSaveEntry> CaptureSaveEntries()
    {
        SynchronizeCurrentStoreRuntimeState();

        var entries = new List<StoreSaveEntry>(_storeRuntimeStates.Count);
        foreach (KeyValuePair<Key_StoreInventory, Dictionary<Key_ItemDefinitionPP, StoreItemRuntimeState>> storePair in _storeRuntimeStates)
        {
            if (storePair.Key == Key_StoreInventory.None || storePair.Value == null)
                continue;

            var entry = new StoreSaveEntry { storeKey = storePair.Key };
            foreach (KeyValuePair<Key_ItemDefinitionPP, StoreItemRuntimeState> itemPair in storePair.Value)
            {
                if (itemPair.Key == Key_ItemDefinitionPP.None || itemPair.Value == null)
                    continue;

                entry.items.Add(new StoreItemSaveEntry
                {
                    itemKey = itemPair.Key,
                    remainingCount = Mathf.Max(0, itemPair.Value.RemainingCount),
                    isLocked = itemPair.Value.IsLocked
                });
            }

            entry.items.Sort((left, right) => left.itemKey.CompareTo(right.itemKey));
            entries.Add(entry);
        }

        entries.Sort((left, right) => left.storeKey.CompareTo(right.storeKey));
        return entries;
    }

    /// <summary>
    /// Restores mutable store state without opening any store UI. The authored
    /// inventory is used automatically for stores absent from this save data.
    /// </summary>
    public void RestoreSaveEntries(List<StoreSaveEntry> entries)
    {
        _storeRuntimeStates.Clear();

        if (entries == null)
        {
            ApplyRuntimeStateToCurrentStore();
            return;
        }

        foreach (StoreSaveEntry entry in entries)
        {
            if (entry == null || entry.storeKey == Key_StoreInventory.None || entry.items == null)
                continue;

            Dictionary<Key_ItemDefinitionPP, StoreItemRuntimeState> itemStates = GetOrCreateStoreRuntimeState(entry.storeKey);
            foreach (StoreItemSaveEntry itemEntry in entry.items)
            {
                if (itemEntry == null || itemEntry.itemKey == Key_ItemDefinitionPP.None)
                    continue;

                itemStates[itemEntry.itemKey] = new StoreItemRuntimeState
                {
                    RemainingCount = Mathf.Max(0, itemEntry.remainingCount),
                    IsLocked = itemEntry.isLocked
                };
            }
        }

        ApplyRuntimeStateToCurrentStore();
    }

    private void InitializeRuntimeInventory()
    {
        inventoryProperty = null;
        if (inventoryDatabase == null || inventoryKey == Key_StoreInventory.None)
        {
            runtimeStoreContainer = new StoreContainer(0);
            return;
        }

        inventoryProperty = inventoryDatabase.GetByEnum(inventoryKey);
        if (inventoryProperty == null)
        {
            Debug.LogWarning("[StoreManager] No StoreInventoryProperty found for key " + inventoryKey + ".");
            runtimeStoreContainer = new StoreContainer(0);
            return;
        }

        if (!inventoryProperty.HasUniqueItemDefinitions(out Key_ItemDefinitionPP duplicateItemKey))
        {
            Debug.LogError($"[{nameof(StoreManager)}] Store '{inventoryKey}' contains duplicate item '{duplicateItemKey}'. " +
                           "Each store item must be configured only once so its inventory can be saved safely.", this);
            runtimeStoreContainer = new StoreContainer(0);
            return;
        }

        runtimeStoreContainer = inventoryProperty.CreateRuntimeContainer();
        ApplyRuntimeStateToCurrentStore();
    }

    private Dictionary<Key_ItemDefinitionPP, StoreItemRuntimeState> GetOrCreateStoreRuntimeState(Key_StoreInventory storeKey)
    {
        if (!_storeRuntimeStates.TryGetValue(storeKey, out Dictionary<Key_ItemDefinitionPP, StoreItemRuntimeState> itemStates))
        {
            itemStates = new Dictionary<Key_ItemDefinitionPP, StoreItemRuntimeState>();
            _storeRuntimeStates.Add(storeKey, itemStates);
        }

        return itemStates;
    }

    private void SynchronizeCurrentStoreRuntimeState()
    {
        if (inventoryKey == Key_StoreInventory.None || runtimeStoreContainer == null)
            return;

        Dictionary<Key_ItemDefinitionPP, StoreItemRuntimeState> itemStates = GetOrCreateStoreRuntimeState(inventoryKey);
        for (int i = 0; i < runtimeStoreContainer.MaxSlots; i++)
        {
            StoreSlot slot = runtimeStoreContainer.GetSlotByIndex(i);
            if (slot == null || slot.ItemEnum == Key_ItemDefinitionPP.None)
                continue;

            itemStates[slot.ItemEnum] = new StoreItemRuntimeState
            {
                RemainingCount = Mathf.Max(0, slot.ItemCount),
                IsLocked = slot.isLocked
            };
        }
    }

    private void ApplyRuntimeStateToCurrentStore()
    {
        if (inventoryKey == Key_StoreInventory.None
            || runtimeStoreContainer == null
            || !_storeRuntimeStates.TryGetValue(inventoryKey, out Dictionary<Key_ItemDefinitionPP, StoreItemRuntimeState> itemStates))
        {
            return;
        }

        for (int i = 0; i < runtimeStoreContainer.MaxSlots; i++)
        {
            StoreSlot slot = runtimeStoreContainer.GetSlotByIndex(i);
            if (slot == null
                || slot.ItemEnum == Key_ItemDefinitionPP.None
                || !itemStates.TryGetValue(slot.ItemEnum, out StoreItemRuntimeState itemState)
                || itemState == null)
            {
                continue;
            }

            if (!runtimeStoreContainer.TrySetSlotAtIndex(i, slot.ItemEnum, Mathf.Max(0, itemState.RemainingCount), out string failReason))
            {
                Debug.LogWarning($"[{nameof(StoreManager)}] Could not restore '{slot.ItemEnum}' in store '{inventoryKey}': {failReason}", this);
                continue;
            }

            StoreSlot restoredSlot = runtimeStoreContainer.GetSlotByIndex(i);
            if (restoredSlot != null)
                restoredSlot.isLocked = itemState.IsLocked;
        }
    }

    private void RebuildTradeView()
    {
        if (uiContainer == null)
        {
            detailPanel?.ShowEmpty();
            return;
        }

        uiContainer.InitSlots(GetCurrentTradeSlotCount());
        RefreshCurrentTradeUI();
        SelectFirstAvailableSlot();
    }

    private int GetCurrentTradeSlotCount()
    {
        if (_currentTradeMode == StoreTradeMode.Buy)
            return runtimeStoreContainer != null ? runtimeStoreContainer.MaxSlots : 0;

        InventoryContainer playerInventory = InventoryManager.Instance != null
            ? InventoryManager.Instance.Inventory
            : null;
        return playerInventory != null ? playerInventory.MaxSlots : 0;
    }

    private void RefreshCurrentTradeUI()
    {
        if (uiContainer == null)
            return;

        uiContainer.Refresh(_currentTradeMode == StoreTradeMode.Buy
            ? BuildBuyDisplayData()
            : BuildSellDisplayData());
    }

    private StoreSlotDisplayData[] BuildBuyDisplayData()
    {
        int count = runtimeStoreContainer != null ? runtimeStoreContainer.MaxSlots : 0;
        var result = new StoreSlotDisplayData[count];

        for (int i = 0; i < count; i++)
        {
            StoreSlot slot = runtimeStoreContainer.GetSlotByIndex(i);
            if (slot == null || slot.ItemEnum == Key_ItemDefinitionPP.None)
            {
                result[i] = new StoreSlotDisplayData(null, Color.clear, 0, "", SlotState.Empty);
                continue;
            }

            float displayMultiplier = GetItemPriceMultiplierForDisplay(slot.ItemEnum);
            if (slot.isLocked)
            {
                result[i] = new StoreSlotDisplayData(null, Color.clear, 0, "", SlotState.Locked, displayMultiplier);
                continue;
            }

            if (!TryGetSlotAndProperties(i, out _, out ItemDefinitionSO item, out SellableProperty sellable))
            {
                result[i] = new StoreSlotDisplayData(null, Color.clear, 0, "", SlotState.Empty);
                continue;
            }

            if (slot.ItemCount > 0)
            {
                int price = ResolvePrice(slot.ItemEnum, sellable);
                result[i] = new StoreSlotDisplayData(
                    item.Icon,
                    Color.white,
                    slot.ItemCount,
                    price + " " + purchaseCurrency,
                    SlotState.Default,
                    displayMultiplier);
            }
            else
            {
                result[i] = new StoreSlotDisplayData(
                    item.Icon,
                    Color.white,
                    0,
                    item.DisplayName,
                    SlotState.SoldOut,
                    displayMultiplier);
            }
        }

        return result;
    }

    private StoreSlotDisplayData[] BuildSellDisplayData()
    {
        InventoryContainer playerInventory = InventoryManager.Instance != null
            ? InventoryManager.Instance.Inventory
            : null;
        int count = playerInventory != null ? playerInventory.MaxSlots : 0;
        var result = new StoreSlotDisplayData[count];

        for (int i = 0; i < count; i++)
        {
            InventorySlot inventorySlot = playerInventory.GetSlotByIndex(i);
            if (inventorySlot == null
                || inventorySlot.IsEmpty
                || !TryGetItemAndSellable(inventorySlot.ItemEnum, out ItemDefinitionSO item, out SellableProperty sellable))
            {
                result[i] = new StoreSlotDisplayData(null, Color.clear, 0, "", SlotState.Empty);
                continue;
            }

            int price = ResolvePrice(inventorySlot.ItemEnum, sellable);
            result[i] = new StoreSlotDisplayData(
                item.Icon,
                Color.white,
                inventorySlot.ItemCount,
                price + " " + purchaseCurrency,
                SlotState.Default,
                GetItemPriceMultiplierForDisplay(inventorySlot.ItemEnum));
        }

        return result;
    }

    private void HandleSlotSelected(int slotIndex)
    {
        ShowSelectedSlotDetail(slotIndex);
    }

    private void ConfirmSelectedTransaction()
    {
        if (uiContainer == null || !uiContainer.HasSelection)
            return;

        if (_currentTradeMode == StoreTradeMode.Buy)
            TryPurchase(uiContainer.SelectedSlotIndex);
        else
            TrySell(uiContainer.SelectedSlotIndex);
    }

    private void SelectFirstAvailableSlot()
    {
        if (uiContainer == null)
        {
            detailPanel?.ShowEmpty();
            return;
        }

        int count = GetCurrentTradeSlotCount();
        for (int i = 0; i < count; i++)
        {
            bool isAvailable = _currentTradeMode == StoreTradeMode.Buy
                ? IsPurchasableSlot(i)
                : IsSellableInventorySlot(i);
            if (!isAvailable)
                continue;

            uiContainer.SetSelection(i);
            return;
        }

        uiContainer.ClearSelection();
        detailPanel?.ShowEmpty();
    }

    private bool IsPurchasableSlot(int slotIndex)
    {
        return TryGetSlotAndProperties(slotIndex, out StoreSlot slot, out _, out _)
               && !slot.isLocked
               && slot.ItemCount > 0;
    }

    private bool IsSellableInventorySlot(int slotIndex)
    {
        return TryGetInventorySlotAndProperties(slotIndex, out _, out _, out _);
    }

    private void ShowSelectedSlotDetail(int slotIndex)
    {
        if (_currentTradeMode == StoreTradeMode.Buy)
        {
            if (!TryGetSlotAndProperties(slotIndex, out StoreSlot storeSlot, out ItemDefinitionSO buyItem, out SellableProperty buySellable)
                || storeSlot.isLocked)
            {
                detailPanel?.ShowEmpty();
                return;
            }

            detailPanel?.Show(buyItem, buySellable, ResolvePrice(storeSlot.ItemEnum, buySellable), purchaseCurrency);
            return;
        }

        if (!TryGetInventorySlotAndProperties(slotIndex, out InventorySlot inventorySlot, out ItemDefinitionSO sellItem, out SellableProperty sellSellable))
        {
            detailPanel?.ShowEmpty();
            return;
        }

        detailPanel?.Show(sellItem, sellSellable, ResolvePrice(inventorySlot.ItemEnum, sellSellable), purchaseCurrency);
    }

    private void RefreshSelectedDetail()
    {
        if (uiContainer == null || !uiContainer.HasSelection)
        {
            detailPanel?.ShowEmpty();
            return;
        }

        ShowSelectedSlotDetail(uiContainer.SelectedSlotIndex);
    }

    private bool TryGetSlotAndProperties(int slotIndex, out StoreSlot slot, out ItemDefinitionSO item, out SellableProperty sellable)
    {
        slot = null;
        item = null;
        sellable = null;

        if (runtimeStoreContainer == null || slotIndex < 0 || slotIndex >= runtimeStoreContainer.MaxSlots)
            return false;

        slot = runtimeStoreContainer.GetSlotByIndex(slotIndex);
        return slot != null
               && slot.ItemEnum != Key_ItemDefinitionPP.None
               && TryGetItemAndSellable(slot.ItemEnum, out item, out sellable);
    }

    private bool TryGetInventorySlotAndProperties(int slotIndex, out InventorySlot slot, out ItemDefinitionSO item, out SellableProperty sellable)
    {
        slot = null;
        item = null;
        sellable = null;

        InventoryContainer playerInventory = InventoryManager.Instance != null
            ? InventoryManager.Instance.Inventory
            : null;
        if (playerInventory == null || slotIndex < 0 || slotIndex >= playerInventory.MaxSlots)
            return false;

        slot = playerInventory.GetSlotByIndex(slotIndex);
        return slot != null
               && !slot.IsEmpty
               && slot.ItemEnum != Key_ItemDefinitionPP.None
               && TryGetItemAndSellable(slot.ItemEnum, out item, out sellable);
    }

    private bool TryGetItemAndSellable(Key_ItemDefinitionPP itemKey, out ItemDefinitionSO item, out SellableProperty sellable)
    {
        item = null;
        sellable = null;

        if (itemKey == Key_ItemDefinitionPP.None || itemDatabase == null || sellableDatabase == null)
            return false;

        item = itemDatabase.GetByEnum(itemKey);
        if (item == null || item.SellableKey == Key_SellablePP.None)
            return false;

        sellable = sellableDatabase.GetByEnum(item.SellableKey);
        return sellable != null;
    }

    private int ResolvePrice(Key_ItemDefinitionPP itemKey, SellableProperty sellable)
    {
        int basePrice = sellable != null ? sellable.Price : 0;
        return inventoryProperty != null
            ? inventoryProperty.ResolvePrice(itemKey, basePrice)
            : Mathf.Max(1, basePrice);
    }

    private float GetItemPriceMultiplierForDisplay(Key_ItemDefinitionPP itemKey)
    {
        return inventoryProperty != null
            ? inventoryProperty.GetItemPriceMultiplierForDisplay(itemKey)
            : 1f;
    }

    private void BindInventoryChanges()
    {
        InventoryManager inventoryManager = InventoryManager.Instance;
        if (_boundInventoryManager == inventoryManager)
            return;

        UnbindInventoryChanges();
        _boundInventoryManager = inventoryManager;
        if (_boundInventoryManager != null)
            _boundInventoryManager.OnInventoryChanged += HandleInventoryChanged;
    }

    private void UnbindInventoryChanges()
    {
        if (_boundInventoryManager != null)
            _boundInventoryManager.OnInventoryChanged -= HandleInventoryChanged;

        _boundInventoryManager = null;
    }

    private void HandleInventoryChanged(Key_ItemDefinitionPP _, int __)
    {
        if (!isStoreOpen || _currentTradeMode != StoreTradeMode.Sell || _isExecutingTransaction)
            return;

        RefreshCurrentTradeUI();
        RefreshSelectedDetail();
    }

    private void UpdateTradeTabVisuals()
    {
        if (_buyTabSelectedVisual != null)
            _buyTabSelectedVisual.SetActive(_currentTradeMode == StoreTradeMode.Buy);
        if (_sellTabSelectedVisual != null)
            _sellTabSelectedVisual.SetActive(_currentTradeMode == StoreTradeMode.Sell);

        OnTradeModeChanged?.Invoke(_currentTradeMode);
    }

    private void OnEnterStoreButtonClicked()
    {
        OpenStore(inventoryKey);
    }
}
