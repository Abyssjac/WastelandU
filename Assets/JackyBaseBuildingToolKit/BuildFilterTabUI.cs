using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages a row of filter tab buttons for the build container UI.
/// Each tab maps a <see cref="FurnitureTag"/> to a <see cref="Button"/>.
/// When clicked, the tab calls <see cref="BuildManagerUI.ApplyFilter"/>.
///
/// Usage:
/// 1. Add this component to a GameObject in the build UI panel.
/// 2. Assign the <see cref="BuildManagerUI"/> reference.
/// 3. Fill the <see cref="tabs"/> list ¡ª one entry per filter button.
///    Set <see cref="FilterTabEntry.tag"/> to <see cref="FurnitureTag.None"/> for the "Show All" tab.
/// </summary>
public class BuildFilterTabUI : MonoBehaviour
{
    // ©¤©¤ Nested type ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Serializable]
    public struct FilterTabEntry
    {
        [Tooltip("The button that triggers this filter.")]
        public Button button;

        [Tooltip("FurnitureTag to filter by. Use None for a 'Show All' tab.")]
        public FurnitureTag tag;
    }

    // ©¤©¤ Inspector ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    [Header("References")]
    [SerializeField] private BuildManagerUI buildManagerUI;

    [Header("Tabs")]
    [SerializeField] private FilterTabEntry[] tabs;

    // ©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
    private void Start()
    {
        if (buildManagerUI == null)
        {
            Debug.LogWarning("[BuildFilterTabUI] BuildManagerUI reference not assigned.");
            return;
        }

        for (int i = 0; i < tabs.Length; i++)
        {
            // Capture the tag by value so each lambda closes over its own copy.
            FurnitureTag capturedTag = tabs[i].tag;
            Button       btn         = tabs[i].button;

            if (btn != null)
                btn.onClick.AddListener(() => buildManagerUI.ApplyFilter(capturedTag));
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            if (tabs[i].button != null)
                tabs[i].button.onClick.RemoveAllListeners();
        }
    }
}
