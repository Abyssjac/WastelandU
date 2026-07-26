using UnityEngine;

/// <summary>Converts detailed InventoryManager changes into low-priority ItemDelta notifications.</summary>
[DisallowMultipleComponent]
public class InventoryNotificationBridge : MonoBehaviour
{
    private InventoryManager boundInventoryManager;

    private void OnEnable()
    {
        TryBindInventoryManager();
    }

    private void Start()
    {
        TryBindInventoryManager();
    }

    private void Update()
    {
        if (boundInventoryManager == null)
            TryBindInventoryManager();
    }

    private void OnDisable()
    {
        UnbindInventoryManager();
    }

    private void HandleInventoryChanged(Key_ItemDefinitionPP itemKey, int delta)
    {
        if (delta == 0 || NotificationManager.Instance == null)
            return;

        ItemDefinitionSO item = null;
        if (boundInventoryManager != null)
            boundInventoryManager.TryGetItemDefinition(itemKey, out item, out _);

        string itemName = item != null && !string.IsNullOrWhiteSpace(item.DisplayName)
            ? item.DisplayName
            : itemKey.ToString();
        Sprite icon = item != null ? item.Icon : null;

        NotificationManager.Instance.Show(
            NotificationRequest.CreateItemDelta(icon, itemName, delta));
    }

    private void TryBindInventoryManager()
    {
        if (boundInventoryManager != null || InventoryManager.Instance == null)
            return;

        boundInventoryManager = InventoryManager.Instance;
        boundInventoryManager.OnInventoryChanged += HandleInventoryChanged;
    }

    private void UnbindInventoryManager()
    {
        if (boundInventoryManager == null)
            return;

        boundInventoryManager.OnInventoryChanged -= HandleInventoryChanged;
        boundInventoryManager = null;
    }
}
