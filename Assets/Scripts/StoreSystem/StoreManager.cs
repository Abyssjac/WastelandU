using System;
using UnityEngine;
using UnityEngine.UI;
using JackyUtility;

/// <summary>
/// Sells buildable ItemDefinitions and grants purchased items to InventoryManager.
/// </summary>
public class StoreManager : MonoBehaviour, IGeneralPanelOwner
{
    public static StoreManager Instance { get; private set; }

    [Header("Data")]
    [SerializeField] private Key_StoreInventory inventoryKey = Key_StoreInventory.None;

    [Header("References")]
    [SerializeField] private UI_StoreContainer uiContainer;
    [SerializeField] private StoreItemDetailPanelUI detailPanel;

    [Header("Open Button")]
    [SerializeField] private Button enterStoreButton;

    [Header("Currency")]
    [SerializeField] private CurrencyType purchaseCurrency = CurrencyType.Credits;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled;

    private ItemDefinitionDatabase itemDatabase;
    private BuildableDatabase buildableDatabase;
    private StoreInventoryDatabase inventoryDatabase;
    private StoreInventoryProperty inventoryProperty;
    private StoreContainer runtimeStoreContainer;
    private bool isStoreOpen;

    public event Action<bool> OnStoreModeChanged;

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
            buildableDatabase = databaseManager.GetDatabase<BuildableDatabase>();
            inventoryDatabase = databaseManager.GetDatabase<StoreInventoryDatabase>();
        }

        if (itemDatabase == null)
            Debug.LogWarning("[StoreManager] ItemDefinitionDatabase not found via PropertyDatabaseManager.");
        if (buildableDatabase == null)
            Debug.LogWarning("[StoreManager] BuildableDatabase not found via PropertyDatabaseManager.");
        if (inventoryDatabase == null)
            Debug.LogWarning("[StoreManager] StoreInventoryDatabase not found via PropertyDatabaseManager.");

        InitializeRuntimeInventory();

        if (uiContainer != null)
            uiContainer.OnSelectionChanged += HandleSlotSelected;
        if (enterStoreButton != null)
            enterStoreButton.onClick.AddListener(OnEnterStoreButtonClicked);

        detailPanel?.ShowEmpty();
    }

    private void Update()
    {
        if (!isStoreOpen)
            return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ConfirmSelectedPurchase();
    }

    private void OnDestroy()
    {
        if (uiContainer != null)
            uiContainer.OnSelectionChanged -= HandleSlotSelected;
        if (enterStoreButton != null)
            enterStoreButton.onClick.RemoveListener(OnEnterStoreButtonClicked);
        if (Instance == this)
            Instance = null;
    }

    public void OnPanelOpenRequested()
    {
        isStoreOpen = true;
        uiContainer?.Open();
        uiContainer?.InitSlots(runtimeStoreContainer != null ? runtimeStoreContainer.MaxSlots : 0);
        RefreshUI();
        SelectFirstPurchasableSlot();
        OnStoreModeChanged?.Invoke(true);
    }

    public void OnPanelCloseRequested()
    {
        isStoreOpen = false;
        uiContainer?.ClearSelection();
        detailPanel?.ShowEmpty();
        uiContainer?.Close();
        OnStoreModeChanged?.Invoke(false);
    }

    public void ApplyFilter(FurnitureTag tag)
    {
        if (debugEnabled)
            Debug.Log("[StoreManager] Store filters are disabled. Ignored filter: " + tag);
    }

    public bool TryPurchase(int slotIndex)
    {
        if (!TryGetSlotAndProperties(slotIndex, out StoreSlot slot, out ItemDefinitionSO item, out BuildableProperty buildable))
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

        float price = buildable.storePrice;
        if (!EconomyManager.Instance.TrySpend(purchaseCurrency, price))
        {
            if (debugEnabled)
                Debug.Log("[StoreManager] Not enough " + purchaseCurrency + " to buy " + item.StringKey + ".");
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
            RefreshUI();
            RefreshSelectedDetail();
            return false;
        }

        if (debugEnabled)
            Debug.Log("[StoreManager] Purchased " + item.StringKey + ". Stock remaining: " + slot.ItemCount);

        RefreshUI();
        RefreshSelectedDetail();
        return true;
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

        runtimeStoreContainer = inventoryProperty.CreateRuntimeContainer();
    }

    private void RefreshUI()
    {
        if (uiContainer == null || runtimeStoreContainer == null)
            return;

        uiContainer.Refresh(BuildDisplayData());
    }

    private SlotDisplayData[] BuildDisplayData()
    {
        int count = runtimeStoreContainer != null ? runtimeStoreContainer.MaxSlots : 0;
        var result = new SlotDisplayData[count];

        for (int i = 0; i < count; i++)
        {
            StoreSlot slot = runtimeStoreContainer.GetSlotByIndex(i);
            if (slot == null || slot.ItemEnum == Key_ItemDefinitionPP.None)
            {
                result[i] = SlotDisplayData.Empty;
                continue;
            }

            if (slot.isLocked)
            {
                result[i] = new SlotDisplayData(null, Color.clear, 0, "", SlotState.Locked);
                continue;
            }

            if (!TryGetSlotAndProperties(i, out _, out ItemDefinitionSO item, out BuildableProperty buildable))
            {
                result[i] = SlotDisplayData.Empty;
                continue;
            }

            if (slot.ItemCount > 0)
            {
                string priceLabel = buildable.storePrice.ToString("F0") + " " + purchaseCurrency;
                result[i] = new SlotDisplayData(item.Icon, Color.white, slot.ItemCount, priceLabel, SlotState.Default);
            }
            else
            {
                result[i] = new SlotDisplayData(item.Icon, Color.white, 0, item.StringKey, SlotState.SoldOut);
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
        if (uiContainer != null && uiContainer.HasSelection)
            TryPurchase(uiContainer.SelectedSlotIndex);
    }

    private void SelectFirstPurchasableSlot()
    {
        if (uiContainer == null || runtimeStoreContainer == null)
        {
            detailPanel?.ShowEmpty();
            return;
        }

        for (int i = 0; i < runtimeStoreContainer.MaxSlots; i++)
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
        return TryGetSlotAndProperties(slotIndex, out StoreSlot slot, out _, out _) && !slot.isLocked && slot.ItemCount > 0;
    }

    private void ShowSlotDetail(int slotIndex)
    {
        if (!TryGetSlotAndProperties(slotIndex, out StoreSlot slot, out _, out BuildableProperty buildable) || slot.isLocked)
        {
            detailPanel?.ShowEmpty();
            return;
        }

        detailPanel?.Show(buildable, purchaseCurrency);
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

    private bool TryGetSlotAndProperties(int slotIndex, out StoreSlot slot, out ItemDefinitionSO item, out BuildableProperty buildable)
    {
        slot = null;
        item = null;
        buildable = null;

        if (runtimeStoreContainer == null || itemDatabase == null || buildableDatabase == null)
            return false;
        if (slotIndex < 0 || slotIndex >= runtimeStoreContainer.MaxSlots)
            return false;

        slot = runtimeStoreContainer.GetSlotByIndex(slotIndex);
        if (slot == null || slot.ItemEnum == Key_ItemDefinitionPP.None)
            return false;

        item = itemDatabase.GetByEnum(slot.ItemEnum);
        if (item == null || !item.IsBuildable)
            return false;

        buildable = buildableDatabase.GetByEnum(item.BuildableKey);
        return buildable != null;
    }

    private void OnEnterStoreButtonClicked()
    {
        AllUIManager.Instance?.RequestOpen(this, PanelOpenType.Override);
    }
}