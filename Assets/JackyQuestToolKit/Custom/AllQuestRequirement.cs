using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central home for quest-specific requirement checks. Add one switch case per quest that enables
/// QuestProperty.useCustomRequirement.
/// </summary>
[DisallowMultipleComponent]
public class AllQuestRequirement : MonoBehaviour
{
    /// <summary>Returns true only when this class contains a dedicated handler for the quest key.</summary>
    public bool HasCustomRequirement(Key_Quest questKey)
    {
        switch (questKey)
        {
            default:
                return false;
        }
    }

    /// <summary>
    /// Evaluates the custom portion of a quest. It must not mutate game state, consume items,
    /// grant rewards, change quest state, or save data.
    /// </summary>
    public bool IsSatisfied(Key_Quest questKey)
    {
        switch (questKey)
        {
            default:
                return true;
        }
    }

    /// <summary>
    /// Adds UI rows for a quest's custom requirements. Use the same values used by IsSatisfied.
    /// </summary>
    public void FillDisplayRows(
        Key_Quest questKey,
        List<QuestProperty.RequirementDisplayData> outputRows)
    {
        if (outputRows == null)
            return;

        switch (questKey)
        {
            default:
                return;
        }
    }
}
