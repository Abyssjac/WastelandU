using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Visualizes one item, island, or custom requirement supplied by QuestManager.</summary>
public class QuestRequirementRowUI : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI detailLabel;

    [Header("State Visuals")]
    [SerializeField] private GameObject satisfiedVisual;
    [SerializeField] private GameObject unsatisfiedVisual;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color satisfiedColor = new Color(0.4f, 1f, 0.55f, 1f);
    [SerializeField] private Color unsatisfiedColor = Color.white;

    public void Bind(QuestProperty.RequirementDisplayData data)
    {
        if (iconImage != null)
            iconImage.sprite = data != null ? data.icon : null;

        if (titleLabel != null)
            titleLabel.text = data != null ? data.title : string.Empty;

        if (detailLabel != null)
            detailLabel.text = data != null ? data.detail : string.Empty;

        bool satisfied = data != null && data.isSatisfied;
        if (satisfiedVisual != null)
            satisfiedVisual.SetActive(satisfied);

        if (unsatisfiedVisual != null)
            unsatisfiedVisual.SetActive(!satisfied);

        if (backgroundImage != null)
            backgroundImage.color = satisfied ? satisfiedColor : unsatisfiedColor;
    }
}
