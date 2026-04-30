/// <summary>
/// Runtime mutable state for a single NPC.
/// Owned and updated by <see cref="NPCBehaviour"/>.
/// Not serialized ¡ª rebuilt fresh each play session.
/// </summary>
public class NPCRuntimeData
{
    // ©¤©¤ Affinity dimensions ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    /// <summary>
    /// Computed from room furniture tags.
    /// Formula: Min(maxEnvAffinity, ¦² tagWeight ¡Á tagCount)
    /// </summary>
    public float LivingEnvironmentAffinity { get; set; }

    /// <summary>Placeholder ¡ª driven by daily interaction. Not yet implemented.</summary>
    public float DailyInteractionAffinity { get; set; }

    /// <summary>Placeholder ¡ª driven by gifting. Does not decay. Not yet implemented.</summary>
    public float FamiliarityAffinity { get; set; }

    /// <summary>Sum of all three affinity dimensions.</summary>
    public float TotalAffinity =>
        LivingEnvironmentAffinity + DailyInteractionAffinity + FamiliarityAffinity;
}
