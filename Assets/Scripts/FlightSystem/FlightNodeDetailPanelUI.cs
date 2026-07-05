using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class FlightNodeDetailPanelUI : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI detailText;

    [Header("Start Location")]
    [SerializeField] private Sprite startIcon;
    [SerializeField] private string startName = "Mobile Base";
    [TextArea(2, 5)]
    [SerializeField] private string startDescription = "Current route origin.";

    [Header("Empty")]
    [SerializeField] private string emptyName = "";
    [SerializeField] private string emptyDescription = "";

    public void ShowStartLocation()
    {
        SetIcon(startIcon);
        SetText(nameText, startName);
        SetText(detailText, startDescription);
    }

    public void ShowNode(MapNodeRuntime node)
    {
        if (node == null)
        {
            ShowEmpty();
            return;
        }

        MapNodeProperty property = node.Property;
        SetIcon(property != null ? property.icon : null);
        SetText(nameText, node.DisplayName);
        SetText(detailText, property != null ? property.description : "");
    }

    public void ShowEmpty()
    {
        SetIcon(null);
        SetText(nameText, emptyName);
        SetText(detailText, emptyDescription);
    }

    private void SetIcon(Sprite sprite)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = sprite;
        iconImage.enabled = sprite != null;
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null)
            target.text = value ?? "";
    }
}
