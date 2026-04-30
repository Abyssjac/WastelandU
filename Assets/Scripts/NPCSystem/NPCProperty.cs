using JackyUtility;
using UnityEngine;

/// <summary>
/// One entry in the tag-affinity weight table.
/// Defines how much each furniture tag contributes to this NPC's living environment affinity.
/// </summary>
[System.Serializable]
public struct TagAffinityWeight
{
    [Tooltip("The furniture tag this weight applies to.")]
    public FurnitureTag tag;

    [Tooltip("Affinity gained per piece of furniture carrying this tag in the NPC's room.")]
    public float weight;
}

/// <summary>
/// Static configuration for a single NPC type.
/// Create via  Assets ¡ú Create ¡ú AllProperties ¡ú NPCProperty.
/// </summary>
[CreateAssetMenu(fileName = "NPCPP_", menuName = "AllProperties/NPCProperty")]
public class NPCProperty : EnumStringKeyedProperty<Key_NPC>
{
    [Header("Display")]
    public string displayName;
    public Sprite portrait;

    [Header("Environment Affinity")]
    [Tooltip("Hard cap on living environment affinity for this NPC.")]
    public float maxEnvAffinity = 100f;

    [Tooltip("Per-tag weights used in:  EnvAffinity = Min(maxEnvAffinity, ¦² weight ¡Á tagCount)")]
    public TagAffinityWeight[] tagWeights = new TagAffinityWeight[0];
}
