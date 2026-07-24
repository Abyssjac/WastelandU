using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One selectable quest entry in QuestPanelUI's list.</summary>
public class QuestListItemUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private Button selectButton;

    [Header("Display")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI categoryLabel;

    [Header("State Visuals")]
    [SerializeField] private GameObject ongoingVisual;
    [SerializeField] private GameObject completedVisual;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Color ongoingColor = Color.white;
    [SerializeField] private Color completedColor = new Color(0.4f, 1f, 0.55f, 1f);

    private Key_Quest questKey;
    private Action<Key_Quest> onSelected;

    private void Awake()
    {
        if (selectButton != null)
            selectButton.onClick.AddListener(HandleClicked);
    }

    private void OnDestroy()
    {
        if (selectButton != null)
            selectButton.onClick.RemoveListener(HandleClicked);
    }

    public void Bind(
        Key_Quest key,
        QuestProperty property,
        QuestProperty.QuestState state,
        Action<Key_Quest> selectedCallback)
    {
        questKey = key;
        onSelected = selectedCallback;

        if (iconImage != null)
            iconImage.sprite = property != null ? property.icon : null;

        if (nameLabel != null)
            nameLabel.text = property != null ? property.displayName : key.ToString();

        if (categoryLabel != null)
            categoryLabel.text = property != null && property.category == QuestProperty.QuestCategory.Main
                ? "主线"
                : "支线";

        bool completed = state == QuestProperty.QuestState.Completed;
        if (ongoingVisual != null)
            ongoingVisual.SetActive(!completed);

        if (completedVisual != null)
            completedVisual.SetActive(completed);

        if (backgroundImage != null)
            backgroundImage.color = completed ? completedColor : ongoingColor;
    }

    private void HandleClicked()
    {
        onSelected?.Invoke(questKey);
    }
}
