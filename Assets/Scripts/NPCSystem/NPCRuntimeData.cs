/// <summary>
/// Runtime mutable state for a single NPC.
/// Owned and updated by <see cref="NPCBehaviour"/>.
/// Not serialized ¡ª rebuilt fresh each play session.
/// </summary>
public class NPCRuntimeData
{

    public NPCRuntimeData()
    {
        LivingEnvironmentAffinity = 0f;
        DailyInteractionAffinity = 0f;
        FamiliarityAffinity      = 0f;
    }   
    public NPCRuntimeData(float livingEnvironmentAffinity, float dailyInteractionAffinity, float familiarityAffinity)
    {
        LivingEnvironmentAffinity = livingEnvironmentAffinity;
        DailyInteractionAffinity = dailyInteractionAffinity;
        FamiliarityAffinity      = familiarityAffinity;
    }   
    // ©¤©¤ Affinity dimensions ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Computed from room furniture tags.
    /// Formula: Min(maxEnvAffinity, ¦² tagWeight ¡Á tagCount)
    /// </summary>
    public float LivingEnvironmentAffinity { get; set; }

    /// <summary>Driven by daily interaction button. Decays when interaction lapses.</summary>
    public float DailyInteractionAffinity { get; set; }

    /// <summary>
    /// The in-game day number on which the player last interacted with this NPC.
    /// -1 means the player has never interacted.
    /// </summary>
    public int LastInteractionDay { get; set; } = -1;

    /// <summary>
    /// Whether the player has already interacted with this NPC today.
    /// Reset to false at the start of each new day by <see cref="NPCBehaviour"/>.
    /// </summary>
    public bool InteractedToday { get; set; } = false;

    /// <summary>Placeholder ¡ª driven by gifting. Does not decay. Not yet implemented.</summary>
    public float FamiliarityAffinity { get; set; }

    /// <summary>Sum of all three affinity dimensions.</summary>
    public float TotalAffinity =>
        LivingEnvironmentAffinity + DailyInteractionAffinity + FamiliarityAffinity;
}
