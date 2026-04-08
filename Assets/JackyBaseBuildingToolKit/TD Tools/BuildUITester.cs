using System;
using UnityEngine;

/// <summary>
/// Temporary keyboard-driven tester for the build-container integration.
/// Press Alpha1~9 to add predefined items into BuildManager's container.
/// Actual building is triggered by clicking slots in the UI_Container.
/// </summary>
public class BuildUITester : MonoBehaviour
{
    [Serializable]
    public struct ItemAddSlot
    {
        [Tooltip("The ContainerItemKey to add.")]
        public Key_ContainerItemPP itemKey;
        [Tooltip("How many to add per key press.")]
        public int count;
    }

    [Header("Quick-Add Slots (max 9, keys Alpha1~9)")]
    [SerializeField]
    private ItemAddSlot[] addSlots = new ItemAddSlot[0];

    private void Start()
    {
        if (BuildManager.Instance == null || BuildManager.Instance.Container == null)
        {
            Debug.LogWarning("[BuildUITester] BuildManager or its Container not ready. " +
                             "Make sure BuildManager is in the scene and databases are configured.");
            enabled = false;
            return;
        }

        Debug.Log($"[BuildUITester] Ready. Press 1~{Mathf.Min(addSlots.Length, 9)} to add items to build container. " +
                  $"Click a container slot to begin placing.");
    }

    private void Update()
    {
        for (int i = 0; i < addSlots.Length && i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                TryAddItem(addSlots[i]);
                return;
            }
        }
    }

    private void TryAddItem(ItemAddSlot slot)
    {
        var container = BuildManager.Instance.Container;
        if (container == null)
        {
            Debug.LogError("[BuildUITester] BuildManager.Container is null.");
            return;
        }

        int count = Mathf.Max(1, slot.count);
        if (container.TryAddItem(slot.itemKey, count, out string reason))
        {
            Debug.Log($"[BuildUITester] Added {count}¡Á {slot.itemKey} to build container.");
        }
        else
        {
            Debug.LogWarning($"[BuildUITester] Failed to add {count}¡Á {slot.itemKey}: {reason}");
        }
    }
}