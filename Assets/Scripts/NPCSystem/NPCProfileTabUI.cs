using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct NPCProfileTabData
{
    public string DisplayName;
    public Sprite Portrait;
}

public class NPCProfileTabUI : MonoBehaviour, INPCPanelTab
{
    [Header("Root")]
    [SerializeField] private GameObject tabRoot;

    [Header("Profile Display")]
    [SerializeField] private TextMeshProUGUI displayNameLabel;
    [SerializeField] private Image portraitImage;

    private NPCProfileTabData _data;

    private void Awake()
    {
        if (tabRoot == null)
            tabRoot = gameObject;
    }

    public void SetData(NPCProfileTabData data)
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
        if (displayNameLabel != null)
            displayNameLabel.text = _data.DisplayName;

        if (portraitImage != null)
            portraitImage.sprite = _data.Portrait;
    }
}
