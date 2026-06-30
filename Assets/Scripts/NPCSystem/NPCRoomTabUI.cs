using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct NPCRoomTabData
{
    public bool HasRoom;
    public Vector3Int StableId;
}

public class NPCRoomTabUI : MonoBehaviour, INPCPanelTab
{
    [Header("Root")]
    [SerializeField] private GameObject tabRoot;

    [Header("Room Display")]
    [SerializeField] private TextMeshProUGUI roomStatusLabel;

    [Header("Buttons")]
    [SerializeField] private Button selectRoomButton;

    public event Action OnSelectRoomRequested;

    private NPCRoomTabData _data;

    private void Awake()
    {
        if (tabRoot == null)
            tabRoot = gameObject;

        if (selectRoomButton != null)
            selectRoomButton.onClick.AddListener(HandleSelectRoomClicked);
    }

    private void OnDestroy()
    {
        if (selectRoomButton != null)
            selectRoomButton.onClick.RemoveListener(HandleSelectRoomClicked);
    }

    public void SetData(NPCRoomTabData data)
    {
        _data = data;
    }

    public void Open()
    {
        if (tabRoot != null)
            tabRoot.SetActive(true);

        Refresh();
    }

    public void Close()
    {
        if (tabRoot != null)
            tabRoot.SetActive(false);
    }

    public void Refresh()
    {
        if (roomStatusLabel != null)
            roomStatusLabel.text = _data.HasRoom ? $"Room {_data.StableId}" : "Unassigned";
    }

    private void HandleSelectRoomClicked()
    {
        OnSelectRoomRequested?.Invoke();
    }
}
