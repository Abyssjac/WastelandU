using UnityEngine;

/// <summary>
/// A repeatable interaction that grants one ItemDefinition item per successful use.
/// It can optionally become unavailable or destroy its assigned root after a limit.
/// </summary>
[DisallowMultipleComponent]
public class ResourceInteractable : BaseInteractable
{
    public enum DepletionAction
    {
        DisableInteraction = 0,
        DestroyResourceRoot = 1,
    }

    [Header("Resource")]
    [SerializeField] private Key_ItemDefinitionPP itemKey = Key_ItemDefinitionPP.None;

    [Header("Collection Limit")]
    [SerializeField] private bool useCollectionLimit;
    [Min(1)]
    [SerializeField] private int maxCollectionCount = 1;

    [Header("Depletion")]
    [SerializeField] private DepletionAction depletionAction = DepletionAction.DisableInteraction;
    [Tooltip("Destroyed when the limit is reached. Falls back to this GameObject when empty.")]
    [SerializeField] private GameObject resourceRoot;

    private int collectedCount;

    public Key_ItemDefinitionPP ItemKey => itemKey;
    public int CollectedCount => collectedCount;
    public bool IsDepleted => useCollectionLimit && collectedCount >= maxCollectionCount;
    public override bool CanInteract => itemKey != Key_ItemDefinitionPP.None && !IsDepleted;

    public override void OnFocused()
    {
        if (!CanInteract)
            return;

        base.OnFocused();
    }

    public override string GetInteractPrompt()
    {
        return CanInteract ? base.GetInteractPrompt() : string.Empty;
    }

    public override void Interact(InteractorTargetDetector caller)
    {
        if (caller == null)
            return;

        if (!CanInteract)
        {
            caller.EndInteraction();
            return;
        }

        InventoryManager inventoryManager = InventoryManager.Instance;
        if (inventoryManager == null)
        {
            Debug.LogWarning("[ResourceInteractable] InventoryManager is not available.", this);
            caller.EndInteraction();
            return;
        }

        if (!inventoryManager.TryAddItem(itemKey, 1, out string failReason))
        {
            Debug.LogWarning(
                "[ResourceInteractable] Could not collect '" + itemKey + "': " + failReason,
                this);
            caller.EndInteraction();
            return;
        }

        collectedCount++;
        bool becameDepleted = IsDepleted;

        // End first so the detector can release focus before an optional destroy.
        caller.EndInteraction();

        if (becameDepleted && depletionAction == DepletionAction.DestroyResourceRoot)
        {
            GameObject rootToDestroy = resourceRoot != null ? resourceRoot : gameObject;
            Destroy(rootToDestroy);
        }
    }

    private void OnValidate()
    {
        maxCollectionCount = Mathf.Max(1, maxCollectionCount);
    }
}
