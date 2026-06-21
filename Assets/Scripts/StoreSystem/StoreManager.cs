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
///   - Own the runtime stock array derived from <see cref="StoreInventorySO"/>.
///   - Build <see cref="SlotDisplayData"/> arrays that encode item state
///     (Default / SoldOut / Empty / Locked) for <see cref="UI_StoreContainer"/>.
///   - Apply <see cref="FurnitureTag"/> filters: non-matching unlocked slots
///     are shown as <see cref="SlotState.Empty"/>.
///   - Process purchases via <see cref="EconomyManager.TrySpend"/>.
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
    private int[] _currentStock;
    private IContainerFilter<Key_BuildablePP> _currentFilter;

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

        InitStock();

        if (uiContainer != null)
            uiContainer.OnSelectionChanged += HandleSlotSelected;

        if (enterStoreButton != null)
            enterStoreButton.onClick.AddListener(OnEnterStoreButtonClicked);
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
        uiContainer?.Open();
        uiContainer?.InitSlots(inventory != null ? inventory.SlotCount : 0);
        RefreshUI();
        OnStoreModeChanged?.Invoke(true);

        if (debugEnabled)
            Debug.Log("[StoreManager] Store opened.");
    }

    public void OnPanelCloseRequested()
    {
        uiContainer?.ClearSelection();
        uiContainer?.Close();
        OnStoreModeChanged?.Invoke(false);

        if (debugEnabled)
            Debug.Log("[StoreManager] Store closed.");
    }

    // --- Public API ---

    /// <summary>
    /// Apply a <see cref="FurnitureTag"/> filter. Slots whose item does not match
    /// the tag are shown as <see cref="SlotState.Empty"/> (but remain in position).
    /// Pass <see cref="FurnitureTag.None"/> to show all items.
    /// </summary>
    public void ApplyFilter(FurnitureTag tag)
    {
        _currentFilter = tag == FurnitureTag.None
            ? null
            : new FurnitureTagFilter(tag);

        RefreshUI();

        if (debugEnabled)
            Debug.Log($"[StoreManager] Filter applied: {tag}");
    }

    /// <summary>
    /// Attempt to purchase the item in slot <paramref name="slotIndex"/>.
    /// Deducts the item price from <see cref="EconomyManager"/> and decrements
    /// the runtime stock.  Returns false if the purchase could not be completed.
    /// </summary>
    public bool TryPurchase(int slotIndex)
    {
        if (inventory == null || _currentStock == null) return false;
        if (slotIndex < 0 || slotIndex >= inventory.SlotCount) return false;

        StoreItemEntry entry = inventory.Entries[slotIndex];

        if (entry.isLocked)
        {
            if (debugEnabled)
                Debug.Log($"[StoreManager] Slot {slotIndex} is locked.");
            return false;
        }

        if (_currentStock[slotIndex] <= 0)
        {
            if (debugEnabled)
                Debug.Log($"[StoreManager] Slot {slotIndex} is sold out.");
            return false;
        }

        BuildableProperty prop = _db?.GetByEnum(entry.itemKey);
        if (prop == null)
        {
            Debug.LogWarning($"[StoreManager] No BuildableProperty found for key {entry.itemKey}.");
            return false;
        }

        float price = prop.storePrice;

        if (EconomyManager.Instance == null)
        {
            Debug.LogWarning("[StoreManager] EconomyManager not found.");
            return false;
        }

        if (!EconomyManager.Instance.TrySpend(purchaseCurrency, price))
        {
            if (debugEnabled)
                Debug.Log($"[StoreManager] Not enough {purchaseCurrency} to buy {prop.displayName} ({price}).");
            return false;
        }

        _currentStock[slotIndex]--;

        if (debugEnabled)
            Debug.Log($"[StoreManager] Purchased {prop.displayName}. Stock remaining: {_currentStock[slotIndex]}");

        RefreshUI();
        return true;
    }

    // --- Private ---

    private void InitStock()
    {
        if (inventory == null)
        {
            _currentStock = Array.Empty<int>();
            return;
        }

        _currentStock = new int[inventory.SlotCount];
        for (int i = 0; i < inventory.SlotCount; i++)
            _currentStock[i] = inventory.Entries[i].initialStock;
    }

    private void RefreshUI()
    {
        if (uiContainer == null || inventory == null) return;

        SlotDisplayData[] data = BuildDisplayData();
        uiContainer.Refresh(data);
    }

    private SlotDisplayData[] BuildDisplayData()
    {
        int count = inventory.SlotCount;
        SlotDisplayData[] result = new SlotDisplayData[count];

        for (int i = 0; i < count; i++)
        {
            StoreItemEntry entry = inventory.Entries[i];

            // Locked slot — always shown as locked regardless of filter
            if (entry.isLocked)
            {
                result[i] = new SlotDisplayData(null, Color.clear, 0, "", SlotState.Locked);
                continue;
            }

            // Filter active — non-matching slots show as empty
            if (_currentFilter != null && !_currentFilter.Matches(entry.itemKey))
            {
                result[i] = new SlotDisplayData(null, Color.clear, 0, "", SlotState.Empty);
                continue;
            }

            BuildableProperty prop = _db?.GetByEnum(entry.itemKey);
            if (prop == null)
            {
                result[i] = SlotDisplayData.Empty;
                continue;
            }

            int stock = _currentStock[i];

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
        if (slotIndex < 0) return;

        TryPurchase(slotIndex);
        uiContainer?.ClearSelection();
    }

    private void OnEnterStoreButtonClicked()
    {
        AllUIManager.Instance?.RequestOpen(this, PanelOpenType.Override);
    }
}
