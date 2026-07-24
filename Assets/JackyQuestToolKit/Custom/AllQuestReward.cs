using UnityEngine;

/// <summary>
/// Central home for quest-specific rewards. Add one switch case per quest that enables
/// QuestProperty.useCustomReward.
/// </summary>
[DisallowMultipleComponent]
public class AllQuestReward : MonoBehaviour
{
    /// <summary>Returns true only when this class contains a dedicated reward handler for the quest key.</summary>
    public bool HasCustomReward(Key_Quest questKey)
    {
        switch (questKey)
        {
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether a quest-specific reward can be granted. Do not mutate state here.
    /// </summary>
    public bool CanGrantCustom(Key_Quest questKey, out string failReason)
    {
        failReason = string.Empty;

        switch (questKey)
        {
            default:
                return true;
        }
    }

    /// <summary>
    /// Executes a quest-specific reward after QuestManager has validated requirements and applied
    /// the quest's basic rewards. Do not set quest state or save data here.
    /// </summary>
    public void GrantCustom(Key_Quest questKey)
    {
        switch (questKey)
        {
            default:
                return;
        }
    }
}
