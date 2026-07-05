using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public struct FlightNodeStateOverlayEntry
{
    public FlightNodeState state;
    public GameObject overlay;
}

public class FlightMapNodeUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private List<FlightNodeStateOverlayEntry> stateOverlays = new List<FlightNodeStateOverlayEntry>();

    private MapNodeRuntime node;
    private Action<int> onClicked;

    public int RuntimeId => node != null ? node.RuntimeId : -1;
    public MapNodeRuntime Node => node;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();

        if (button != null)
            button.onClick.AddListener(HandleClicked);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClicked);
    }

    public void Bind(MapNodeRuntime runtimeNode)
    {
        node = runtimeNode;

        if (iconImage != null)
        {
            Sprite icon = runtimeNode != null && runtimeNode.Property != null
                ? runtimeNode.Property.icon
                : null;

            iconImage.sprite = icon;
            iconImage.enabled = icon != null;
        }
    }

    public void SetClickCallback(Action<int> callback)
    {
        onClicked = callback;
    }

    public void SetInteractable(bool interactable)
    {
        if (button != null)
            button.interactable = interactable;
    }

    public void SetState(FlightNodeState state)
    {
        for (int i = 0; i < stateOverlays.Count; i++)
        {
            GameObject overlay = stateOverlays[i].overlay;
            if (overlay != null)
                overlay.SetActive(stateOverlays[i].state == state);
        }
    }

    private void HandleClicked()
    {
        if (node != null)
            onClicked?.Invoke(node.RuntimeId);
    }
}
