using UnityEngine;

/// <summary>
/// Action: this item can be dropped into the world as a pick-up.
/// </summary>
[CreateAssetMenu(fileName = "Action_Drop_", menuName = "ContainerItemActions/DropAction")]
public class ContainerItemDropAction : ContainerItemAction
{
    public override string ActionName => "Drop";

    [Header("Drop Data")]
    [Tooltip("The world prefab spawned when the item is dropped.")]
    public GameObject dropPrefab;

    [Tooltip("How many items are dropped per action. 0 = drop entire stack.")]
    public int dropCountPerAction = 1;
}
