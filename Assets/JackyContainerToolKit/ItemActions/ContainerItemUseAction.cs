using UnityEngine;

/// <summary>
/// Action: this item can be consumed / used directly (e.g. healing, buffs, etc.).
/// </summary>
[CreateAssetMenu(fileName = "Action_Use_", menuName = "ContainerItemActions/UseAction")]
public class ContainerItemUseAction : ContainerItemAction
{
    public override string ActionName => "Use";

    [Header("Use Data")]
    [Tooltip("How many items are consumed per use.")]
    public int consumeCount = 1;

    [Tooltip("Optional: a VFX prefab spawned on the player when the item is used.")]
    public GameObject useEffectPrefab;

    [Tooltip("Cooldown in seconds between uses. 0 = no cooldown.")]
    public float cooldown;
}
