using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;

/// <summary>
/// Persistent authority for accepted quest state. Quest definitions live in QuestDatabase;
/// this manager owns accepting, evaluating, submitting, events, and save restoration.
/// </summary>
[DisallowMultipleComponent]
public class QuestManager : MonoBehaviour
{
    public struct SubmitAvailability
    {
        public bool IsVisible;
        public bool IsEnabled;
        public List<QuestProperty.RequirementDisplayData> UnsatisfiedRequirements;
    }

    public static QuestManager Instance { get; private set; }

    [Header("Custom Quest Logic")]
    [SerializeField] private AllQuestRequirement allQuestRequirement;
    [SerializeField] private AllQuestReward allQuestReward;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled;

    private readonly Dictionary<Key_Quest, QuestRuntimeState> runtimeStates =
        new Dictionary<Key_Quest, QuestRuntimeState>();

    private QuestDatabase questDatabase;
    private MapNodeDatabase mapNodeDatabase;
    private InventoryManager boundInventoryManager;
    private FlightManager boundFlightManager;
    private bool isSubmitting;

    public event Action<Key_Quest> OnQuestAccepted;
    public event Action<Key_Quest> OnQuestProgressChanged;
    public event Action<Key_Quest, QuestProperty.QuestState, QuestProperty.QuestState> OnQuestStateChanged;
    public event Action<Key_Quest> OnQuestSubmitted;

    public QuestDatabase Database => questDatabase;
    public bool IsSubmitting => isSubmitting;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (allQuestRequirement == null)
            allQuestRequirement = GetComponent<AllQuestRequirement>();

        if (allQuestReward == null)
            allQuestReward = GetComponent<AllQuestReward>();
    }

    private void Start()
    {
        ResolveDatabases();
        TryBindExternalManagers();
    }

    private void Update()
    {
        if (boundInventoryManager == null || boundFlightManager == null)
            TryBindExternalManagers();
    }

    private void OnDestroy()
    {
        UnbindExternalManagers();

        if (Instance == this)
            Instance = null;
    }

    public bool TryAcceptQuest(Key_Quest questKey)
    {
        if (!TryGetQuestProperty(questKey, out QuestProperty property))
            return false;

        if (runtimeStates.ContainsKey(questKey))
        {
            Debug.LogWarning($"[{nameof(QuestManager)}] Quest '{questKey}' was already accepted or submitted.", this);
            return false;
        }

        CheckUnexpectedCurrentIslandOnAccept(property);

        runtimeStates.Add(questKey, new QuestRuntimeState
        {
            questKey = questKey,
            state = QuestProperty.QuestState.Ongoing,
        });

        RefreshQuestState(questKey);
        OnQuestAccepted?.Invoke(questKey);
        OnQuestProgressChanged?.Invoke(questKey);
        DebugLog($"Accepted quest '{questKey}'.");
        return true;
    }

    public bool IsQuestAccepted(Key_Quest questKey)
    {
        return runtimeStates.ContainsKey(questKey);
    }

    public bool IsQuestSubmitted(Key_Quest questKey)
    {
        return runtimeStates.TryGetValue(questKey, out QuestRuntimeState state)
            && state != null
            && state.state == QuestProperty.QuestState.Submitted;
    }

    public bool TryGetQuestState(Key_Quest questKey, out QuestProperty.QuestState state)
    {
        if (runtimeStates.TryGetValue(questKey, out QuestRuntimeState runtimeState) && runtimeState != null)
        {
            state = runtimeState.state;
            return true;
        }

        state = QuestProperty.QuestState.Ongoing;
        return false;
    }

    public bool TryGetQuestRuntimeState(Key_Quest questKey, out QuestRuntimeState state)
    {
        return runtimeStates.TryGetValue(questKey, out state) && state != null;
    }

    public bool TryGetQuestProperty(Key_Quest questKey, out QuestProperty property)
    {
        ResolveDatabases();
        property = null;

        if (questKey == Key_Quest.None)
        {
            Debug.LogWarning($"[{nameof(QuestManager)}] Quest key cannot be None.", this);
            return false;
        }

        if (questDatabase == null)
        {
            Debug.LogWarning($"[{nameof(QuestManager)}] {nameof(QuestDatabase)} is not ready.", this);
            return false;
        }

        property = questDatabase.GetByEnum(questKey);
        if (property != null)
            return true;

        Debug.LogWarning($"[{nameof(QuestManager)}] No QuestProperty found for key '{questKey}'.", this);
        return false;
    }

    public List<Key_Quest> GetVisibleQuestKeys()
    {
        var result = new List<Key_Quest>();

        foreach (KeyValuePair<Key_Quest, QuestRuntimeState> pair in runtimeStates)
        {
            if (pair.Value == null || pair.Value.state == QuestProperty.QuestState.Submitted)
                continue;

            result.Add(pair.Key);
        }

        result.Sort((a, b) => a.CompareTo(b));
        return result;
    }

    public List<Key_Quest> GetAllRuntimeQuestKeys()
    {
        var result = new List<Key_Quest>(runtimeStates.Keys);
        result.Sort((a, b) => a.CompareTo(b));
        return result;
    }

    public void RefreshAllQuestStates()
    {
        var keys = new List<Key_Quest>(runtimeStates.Keys);
        for (int i = 0; i < keys.Count; i++)
            RefreshQuestState(keys[i]);
    }

    public void RefreshQuestState(Key_Quest questKey)
    {
        if (!runtimeStates.TryGetValue(questKey, out QuestRuntimeState runtimeState) || runtimeState == null)
            return;

        if (runtimeState.state == QuestProperty.QuestState.Submitted)
            return;

        if (!TryGetQuestProperty(questKey, out QuestProperty property))
            return;

        QuestProperty.QuestState previousState = runtimeState.state;
        runtimeState.state = AreAllRequirementsSatisfied(property, runtimeState)
            ? QuestProperty.QuestState.Completed
            : QuestProperty.QuestState.Ongoing;

        if (previousState != runtimeState.state)
        {
            OnQuestStateChanged?.Invoke(questKey, previousState, runtimeState.state);
            DebugLog($"Quest '{questKey}' changed from {previousState} to {runtimeState.state}.");
        }

        OnQuestProgressChanged?.Invoke(questKey);
    }

    public void ReportIslandArrived(Key_MapNodePP islandKey)
    {
        if (islandKey == Key_MapNodePP.None)
            return;

        foreach (KeyValuePair<Key_Quest, QuestRuntimeState> pair in runtimeStates)
        {
            QuestRuntimeState runtimeState = pair.Value;
            if (runtimeState == null || runtimeState.state == QuestProperty.QuestState.Submitted)
                continue;

            if (!TryGetQuestProperty(pair.Key, out QuestProperty property)
                || !QuestRequiresIsland(property, islandKey))
                continue;

            runtimeState.arrivedRequiredIslands ??= new List<Key_MapNodePP>();
            if (!runtimeState.arrivedRequiredIslands.Contains(islandKey))
                runtimeState.arrivedRequiredIslands.Add(islandKey);

            RefreshQuestState(pair.Key);
        }
    }

    public SubmitAvailability GetSubmitAvailability(Key_Quest questKey)
    {
        var availability = new SubmitAvailability
        {
            IsVisible = false,
            IsEnabled = false,
            UnsatisfiedRequirements = new List<QuestProperty.RequirementDisplayData>(),
        };

        if (!runtimeStates.TryGetValue(questKey, out QuestRuntimeState runtimeState) || runtimeState == null)
            return availability;

        RefreshQuestState(questKey);
        if (runtimeState.state == QuestProperty.QuestState.Submitted)
            return availability;

        availability.IsVisible = true;
        availability.IsEnabled = runtimeState.state == QuestProperty.QuestState.Completed;

        if (!availability.IsEnabled)
            availability.UnsatisfiedRequirements = GetUnsatisfiedRequirementDisplayData(questKey);

        return availability;
    }

    public bool TrySubmitQuest(Key_Quest questKey)
    {
        if (isSubmitting)
        {
            Debug.LogWarning($"[{nameof(QuestManager)}] Ignored submit request for '{questKey}' while another submit is in progress.", this);
            return false;
        }

        if (!runtimeStates.TryGetValue(questKey, out QuestRuntimeState runtimeState) || runtimeState == null)
            return false;

        RefreshQuestState(questKey);
        if (runtimeState.state != QuestProperty.QuestState.Completed)
            return false;

        if (!TryGetQuestProperty(questKey, out QuestProperty property))
            return false;

        if (!TryResolveInventory(out InventoryManager inventoryManager))
            return false;

        if (!HasAllSubmissionItems(property, inventoryManager, out string failReason))
        {
            Debug.LogWarning($"[{nameof(QuestManager)}] Cannot submit '{questKey}': {failReason}", this);
            return false;
        }

        if (!ValidateCustomReward(property, out failReason))
        {
            Debug.LogWarning($"[{nameof(QuestManager)}] Cannot submit '{questKey}': {failReason}", this);
            return false;
        }

        isSubmitting = true;
        try
        {
            if (!RemoveSubmissionItems(property, inventoryManager, out failReason))
            {
                Debug.LogError($"[{nameof(QuestManager)}] Submission removal unexpectedly failed for '{questKey}': {failReason}", this);
                return false;
            }

            GrantBasicItemRewards(questKey, property, inventoryManager);
            GrantBasicCurrencyRewards(questKey, property);

            if (property.useCustomReward)
                allQuestReward.GrantCustom(questKey);

            QuestProperty.QuestState previousState = runtimeState.state;
            runtimeState.state = QuestProperty.QuestState.Submitted;
            OnQuestStateChanged?.Invoke(questKey, previousState, runtimeState.state);
            OnQuestProgressChanged?.Invoke(questKey);
            OnQuestSubmitted?.Invoke(questKey);
            DebugLog($"Submitted quest '{questKey}'.");
            return true;
        }
        finally
        {
            isSubmitting = false;
        }
    }

    public List<QuestProperty.RequirementDisplayData> GetRequirementDisplayData(Key_Quest questKey)
    {
        var result = new List<QuestProperty.RequirementDisplayData>();

        if (!runtimeStates.TryGetValue(questKey, out QuestRuntimeState runtimeState)
            || runtimeState == null
            || !TryGetQuestProperty(questKey, out QuestProperty property))
        {
            return result;
        }

        AddItemRequirementDisplayData(property, result);
        AddIslandRequirementDisplayData(property, runtimeState, result);

        if (property.useCustomRequirement && allQuestRequirement != null)
            allQuestRequirement.FillDisplayRows(questKey, result);

        return result;
    }

    public List<QuestProperty.RequirementDisplayData> GetUnsatisfiedRequirementDisplayData(Key_Quest questKey)
    {
        List<QuestProperty.RequirementDisplayData> allRows = GetRequirementDisplayData(questKey);
        allRows.RemoveAll(row => row != null && row.isSatisfied);
        return allRows;
    }

    public List<QuestSaveEntry> CaptureSaveEntries()
    {
        var entries = new List<QuestSaveEntry>();
        foreach (KeyValuePair<Key_Quest, QuestRuntimeState> pair in runtimeStates)
        {
            QuestRuntimeState runtimeState = pair.Value;
            if (runtimeState == null)
                continue;

            entries.Add(new QuestSaveEntry
            {
                questKey = runtimeState.questKey,
                state = runtimeState.state,
                arrivedRequiredIslands = runtimeState.arrivedRequiredIslands != null
                    ? new List<Key_MapNodePP>(runtimeState.arrivedRequiredIslands)
                    : new List<Key_MapNodePP>(),
            });
        }

        return entries;
    }

    public void RestoreSaveEntries(List<QuestSaveEntry> entries)
    {
        runtimeStates.Clear();

        if (entries != null)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                QuestSaveEntry entry = entries[i];
                if (entry == null || entry.questKey == Key_Quest.None)
                    continue;

                if (!TryGetQuestProperty(entry.questKey, out _))
                    continue;

                runtimeStates[entry.questKey] = new QuestRuntimeState
                {
                    questKey = entry.questKey,
                    state = entry.state,
                    arrivedRequiredIslands = entry.arrivedRequiredIslands != null
                        ? new List<Key_MapNodePP>(entry.arrivedRequiredIslands)
                        : new List<Key_MapNodePP>(),
                };
            }
        }

        RefreshAllQuestStates();
    }

    public void ClearRuntimeStateForDebug()
    {
        runtimeStates.Clear();
    }

    private bool AreAllRequirementsSatisfied(QuestProperty property, QuestRuntimeState runtimeState)
    {
        if (!AreItemRequirementsSatisfied(property))
            return false;

        if (!AreIslandRequirementsSatisfied(property, runtimeState))
            return false;

        return AreCustomRequirementsSatisfied(property);
    }

    private bool AreItemRequirementsSatisfied(QuestProperty property)
    {
        if (property.itemRequirements == null || property.itemRequirements.Count == 0)
            return true;

        if (!TryResolveInventory(out InventoryManager inventoryManager))
            return false;

        for (int i = 0; i < property.itemRequirements.Count; i++)
        {
            QuestProperty.ItemRequirement requirement = property.itemRequirements[i];
            if (requirement == null
                || requirement.itemKey == Key_ItemDefinitionPP.None
                || requirement.requiredCount <= 0
                || inventoryManager.GetCount(requirement.itemKey) < requirement.requiredCount)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreIslandRequirementsSatisfied(QuestProperty property, QuestRuntimeState runtimeState)
    {
        if (property.islandRequirements == null || property.islandRequirements.Count == 0)
            return true;

        if (runtimeState.arrivedRequiredIslands == null)
            return false;

        for (int i = 0; i < property.islandRequirements.Count; i++)
        {
            QuestProperty.IslandRequirement requirement = property.islandRequirements[i];
            if (requirement == null
                || requirement.islandKey == Key_MapNodePP.None
                || !runtimeState.arrivedRequiredIslands.Contains(requirement.islandKey))
            {
                return false;
            }
        }

        return true;
    }

    private bool AreCustomRequirementsSatisfied(QuestProperty property)
    {
        if (!property.useCustomRequirement)
            return true;

        if (allQuestRequirement == null)
        {
            Debug.LogError($"[{nameof(QuestManager)}] Quest '{property.EnumKey}' enables custom requirements but {nameof(AllQuestRequirement)} is missing.", this);
            return false;
        }

        if (!allQuestRequirement.HasCustomRequirement(property.EnumKey))
        {
            Debug.LogError($"[{nameof(QuestManager)}] Quest '{property.EnumKey}' enables custom requirements but has no handler in {nameof(AllQuestRequirement)}.", this);
            return false;
        }

        return allQuestRequirement.IsSatisfied(property.EnumKey);
    }

    private bool ValidateCustomReward(QuestProperty property, out string failReason)
    {
        failReason = string.Empty;
        if (!property.useCustomReward)
            return true;

        if (allQuestReward == null)
        {
            failReason = $"{nameof(AllQuestReward)} is missing.";
            return false;
        }

        if (!allQuestReward.HasCustomReward(property.EnumKey))
        {
            failReason = $"Quest '{property.EnumKey}' enables a custom reward but has no handler in {nameof(AllQuestReward)}.";
            return false;
        }

        return allQuestReward.CanGrantCustom(property.EnumKey, out failReason);
    }

    private static bool HasAllSubmissionItems(QuestProperty property, InventoryManager inventoryManager, out string failReason)
    {
        failReason = string.Empty;
        var totalRequiredByItem = new Dictionary<Key_ItemDefinitionPP, int>();

        if (property.itemRequirements != null)
        {
            for (int i = 0; i < property.itemRequirements.Count; i++)
            {
                QuestProperty.ItemRequirement requirement = property.itemRequirements[i];
                if (requirement == null || requirement.itemKey == Key_ItemDefinitionPP.None || requirement.requiredCount <= 0)
                {
                    failReason = "Quest contains an invalid item requirement.";
                    return false;
                }

                totalRequiredByItem.TryGetValue(requirement.itemKey, out int currentRequired);
                totalRequiredByItem[requirement.itemKey] = currentRequired + requirement.requiredCount;
            }
        }

        foreach (KeyValuePair<Key_ItemDefinitionPP, int> pair in totalRequiredByItem)
        {
            if (inventoryManager.GetCount(pair.Key) < pair.Value)
            {
                failReason = $"Missing {pair.Key}: needs {pair.Value}, has {inventoryManager.GetCount(pair.Key)}.";
                return false;
            }
        }

        return true;
    }

    private static bool RemoveSubmissionItems(QuestProperty property, InventoryManager inventoryManager, out string failReason)
    {
        failReason = string.Empty;
        if (property.itemRequirements == null)
            return true;

        for (int i = 0; i < property.itemRequirements.Count; i++)
        {
            QuestProperty.ItemRequirement requirement = property.itemRequirements[i];
            if (requirement == null)
                continue;

            if (!inventoryManager.TryRemoveItem(requirement.itemKey, requirement.requiredCount, out failReason))
                return false;
        }

        return true;
    }

    private void GrantBasicItemRewards(
        Key_Quest questKey,
        QuestProperty property,
        InventoryManager inventoryManager)
    {
        if (property.itemRewards == null)
            return;

        for (int i = 0; i < property.itemRewards.Count; i++)
        {
            QuestProperty.ItemReward reward = property.itemRewards[i];
            if (reward == null || reward.itemKey == Key_ItemDefinitionPP.None || reward.amount <= 0)
            {
                Debug.LogError($"[{nameof(QuestManager)}] Quest '{questKey}' has an invalid item reward.", this);
                continue;
            }

            bool anyAdded = inventoryManager.TryAddReturnExcess(reward.itemKey, reward.amount, out int excess, out string failReason);
            if (!anyAdded || excess > 0)
            {
                Debug.LogError(
                    $"[{nameof(QuestManager)}] Item reward for quest '{questKey}' overflowed or failed: " +
                    $"{reward.itemKey} x{reward.amount}, excess={excess}, reason='{failReason}'.",
                    this);
            }
        }
    }

    private void GrantBasicCurrencyRewards(Key_Quest questKey, QuestProperty property)
    {
        if (property.currencyRewards == null || property.currencyRewards.Count == 0)
            return;

        if (EconomyManager.Instance == null)
        {
            Debug.LogError($"[{nameof(QuestManager)}] Cannot grant currency reward for '{questKey}': {nameof(EconomyManager)} is missing.", this);
            return;
        }

        for (int i = 0; i < property.currencyRewards.Count; i++)
        {
            QuestProperty.CurrencyReward reward = property.currencyRewards[i];
            if (reward == null || reward.amount <= 0f)
            {
                Debug.LogError($"[{nameof(QuestManager)}] Quest '{questKey}' has an invalid currency reward.", this);
                continue;
            }

            EconomyManager.Instance.AddCurrency(reward.currencyType, reward.amount);
        }
    }

    private void AddItemRequirementDisplayData(
        QuestProperty property,
        List<QuestProperty.RequirementDisplayData> output)
    {
        if (property.itemRequirements == null)
            return;

        TryResolveInventory(out InventoryManager inventoryManager);

        for (int i = 0; i < property.itemRequirements.Count; i++)
        {
            QuestProperty.ItemRequirement requirement = property.itemRequirements[i];
            if (requirement == null)
                continue;

            ItemDefinitionSO itemDefinition = null;
            inventoryManager?.TryGetItemDefinition(requirement.itemKey, out itemDefinition, out _);
            int currentCount = inventoryManager != null ? inventoryManager.GetCount(requirement.itemKey) : 0;

            output.Add(new QuestProperty.RequirementDisplayData
            {
                icon = itemDefinition != null ? itemDefinition.Icon : null,
                title = itemDefinition != null ? itemDefinition.DisplayName : requirement.itemKey.ToString(),
                detail = $"{currentCount} / {requirement.requiredCount}",
                isSatisfied = currentCount >= requirement.requiredCount && requirement.requiredCount > 0,
            });
        }
    }

    private void AddIslandRequirementDisplayData(
        QuestProperty property,
        QuestRuntimeState runtimeState,
        List<QuestProperty.RequirementDisplayData> output)
    {
        if (property.islandRequirements == null)
            return;

        ResolveDatabases();

        for (int i = 0; i < property.islandRequirements.Count; i++)
        {
            QuestProperty.IslandRequirement requirement = property.islandRequirements[i];
            if (requirement == null)
                continue;

            MapNodeProperty mapNode = mapNodeDatabase != null
                ? mapNodeDatabase.GetByEnum(requirement.islandKey)
                : null;

            bool arrived = runtimeState.arrivedRequiredIslands != null
                && runtimeState.arrivedRequiredIslands.Contains(requirement.islandKey);

            output.Add(new QuestProperty.RequirementDisplayData
            {
                icon = mapNode != null ? mapNode.icon : null,
                title = mapNode != null ? mapNode.displayName : requirement.islandKey.ToString(),
                detail = arrived ? "已抵达" : "尚未抵达",
                isSatisfied = arrived,
            });
        }
    }

    private void HandleInventoryChanged()
    {
        if (isSubmitting)
            return;

        RefreshAllQuestStates();
    }

    private void HandleCurrentIslandChanged(Key_MapNodePP islandKey)
    {
        if (islandKey != Key_MapNodePP.None)
            ReportIslandArrived(islandKey);
    }

    private void TryBindExternalManagers()
    {
        if (boundInventoryManager == null && InventoryManager.Instance != null)
        {
            boundInventoryManager = InventoryManager.Instance;
            boundInventoryManager.OnInventoryChanged += HandleInventoryChanged;
        }

        if (boundFlightManager == null && FlightManager.Instance != null)
        {
            boundFlightManager = FlightManager.Instance;
            boundFlightManager.OnCurrentIslandChanged += HandleCurrentIslandChanged;
        }
    }

    private void UnbindExternalManagers()
    {
        if (boundInventoryManager != null)
            boundInventoryManager.OnInventoryChanged -= HandleInventoryChanged;

        if (boundFlightManager != null)
            boundFlightManager.OnCurrentIslandChanged -= HandleCurrentIslandChanged;

        boundInventoryManager = null;
        boundFlightManager = null;
    }

    private bool TryResolveInventory(out InventoryManager inventoryManager)
    {
        TryBindExternalManagers();
        inventoryManager = boundInventoryManager != null ? boundInventoryManager : InventoryManager.Instance;
        if (inventoryManager != null)
            return true;

        Debug.LogWarning($"[{nameof(QuestManager)}] {nameof(InventoryManager)} is not available.", this);
        return false;
    }

    private void ResolveDatabases()
    {
        PropertyDatabaseManager databaseManager = PropertyDatabaseManager.Instance;
        if (databaseManager == null)
            return;

        if (questDatabase == null)
            questDatabase = databaseManager.GetDatabase<QuestDatabase>();

        if (mapNodeDatabase == null)
            mapNodeDatabase = databaseManager.GetDatabase<MapNodeDatabase>();
    }

    private void CheckUnexpectedCurrentIslandOnAccept(QuestProperty property)
    {
        Key_MapNodePP currentIslandKey = FlightManager.Instance != null
            ? FlightManager.Instance.CurrentIslandKey
            : Key_MapNodePP.None;

        if (currentIslandKey == Key_MapNodePP.None || property.islandRequirements == null)
            return;

        for (int i = 0; i < property.islandRequirements.Count; i++)
        {
            QuestProperty.IslandRequirement requirement = property.islandRequirements[i];
            if (requirement != null && requirement.islandKey == currentIslandKey)
            {
                Debug.LogError(
                    $"[{nameof(QuestManager)}] Quest '{property.EnumKey}' was accepted while the player is already on its required island '{currentIslandKey}'. " +
                    "This does not complete the requirement; leave and arrive again after correcting the task flow.",
                    this);
                return;
            }
        }
    }

    private static bool QuestRequiresIsland(QuestProperty property, Key_MapNodePP islandKey)
    {
        if (property.islandRequirements == null)
            return false;

        for (int i = 0; i < property.islandRequirements.Count; i++)
        {
            QuestProperty.IslandRequirement requirement = property.islandRequirements[i];
            if (requirement != null && requirement.islandKey == islandKey)
                return true;
        }

        return false;
    }

    private void DebugLog(string message)
    {
        if (debugEnabled)
            Debug.Log($"[{nameof(QuestManager)}] {message}", this);
    }
}
