using System;
using System.Collections.Generic;
using JackyUtility;
using UnityEngine;

/// <summary>
/// Static configuration for one quest. Runtime state is owned by <see cref="QuestManager"/>.
/// </summary>
[CreateAssetMenu(fileName = "QuestPP_", menuName = "AllProperties/QuestProperty")]
public class QuestProperty : EnumStringKeyedProperty<Key_Quest>
{
    public enum QuestCategory
    {
        Main = 0,
        Side = 1,
    }

    public enum QuestState
    {
        Ongoing = 0,
        Completed = 1,
        Submitted = 2,
    }

    [Serializable]
    public class ItemRequirement
    {
        public Key_ItemDefinitionPP itemKey = Key_ItemDefinitionPP.None;

        [Min(1)] public int requiredCount = 1;
    }

    [Serializable]
    public class IslandRequirement
    {
        public Key_MapNodePP islandKey = Key_MapNodePP.None;
    }

    [Serializable]
    public class ItemReward
    {
        public Key_ItemDefinitionPP itemKey = Key_ItemDefinitionPP.None;

        [Min(1)] public int amount = 1;
    }

    [Serializable]
    public class CurrencyReward
    {
        public CurrencyType currencyType = CurrencyType.Credits;

        [Min(0f)] public float amount;
    }

    /// <summary>UI-only data describing one currently evaluated requirement.</summary>
    [Serializable]
    public class RequirementDisplayData
    {
        public Sprite icon;
        public string title;
        public string detail;
        public bool isSatisfied;
    }

    [Header("Display")]
    public Sprite icon;
    public string displayName;

    [TextArea(2, 6)] public string description;
    public QuestCategory category = QuestCategory.Side;

    [Header("Basic Requirements")]
    public List<ItemRequirement> itemRequirements = new List<ItemRequirement>();
    public List<IslandRequirement> islandRequirements = new List<IslandRequirement>();

    [Tooltip("When enabled, AllQuestRequirement must contain a handler for this quest key.")]
    public bool useCustomRequirement;

    [Header("Basic Rewards")]
    public List<ItemReward> itemRewards = new List<ItemReward>();
    public List<CurrencyReward> currencyRewards = new List<CurrencyReward>();

    [Tooltip("When enabled, AllQuestReward must contain a handler for this quest key.")]
    public bool useCustomReward;

    private void OnValidate()
    {
        EnsureLists();
    }

    private void EnsureLists()
    {
        itemRequirements ??= new List<ItemRequirement>();
        islandRequirements ??= new List<IslandRequirement>();
        itemRewards ??= new List<ItemReward>();
        currencyRewards ??= new List<CurrencyReward>();
    }
}
