using UnityEngine;

/// <summary>
/// Action: this item can be used to build / place a structure.
/// Holds a reference to the build-related data (prefab, cost, preview, etc.)
/// that a BuildManager would need.
/// </summary>
[CreateAssetMenu(fileName = "Action_Build_", menuName = "ContainerItemActions/BuildAction")]
public class ContainerItemBuildAction : ContainerItemAction
{
    public override string ActionName => "Build";

    [Header("Build Data")]
    public Key_BuildablePP buildableKey;

    [Tooltip("How many container items are consumed per build. Minimum 1.")]
    public int costPerBuild = 1;
}
