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
/// Create via  Assets �� Create �� AllProperties �� NPCProperty.
/// </summary>
[CreateAssetMenu(fileName = "NPCPP_", menuName = "AllProperties/NPCProperty")]
public class NPCProperty : EnumStringKeyedProperty<Key_NPC>
{
    [Header("Prefab")]
    [Tooltip("The GameObject prefab instantiated by NPCManager.SpawnNPC().")]
    public GameObject prefab;

    [Header("Display")]
    public string displayName;
    public Sprite portrait;

    [Header("Environment Affinity")]
    [Tooltip("Hard cap on living environment affinity for this NPC.")]
    public float maxEnvAffinity = 100f;

    [Tooltip("Per-tag weights used in:  EnvAffinity = Min(maxEnvAffinity, �� weight �� tagCount)")]
    public TagAffinityWeight[] tagWeights = new TagAffinityWeight[0];
}


/// <summary>
/// Key identifying an NPC type. Should correspond to entries in <see cref="NPCDatabase"/>.
/// </summary>
public enum Key_NPC
{
    None = 0,
    Artist = 1,   // ������
    Botanist = 2,   // ֲ��ѧ��
    Athlete = 3,   // �˶�Ա
    Engineer = 4,

    Eli_Guide = 10,
    Nara_Captain = 11,
    Silas_Merchant = 12,
    Ivo_Engineer = 13,


    Bob_Merchant_FairWindDock = 50,
    Mira_Merchant_MistweilMarket = 51,
    Orren_Merchant_BrassBell = 52,
}