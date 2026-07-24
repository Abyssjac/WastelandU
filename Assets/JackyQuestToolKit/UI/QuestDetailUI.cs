using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays one selected visible quest and all of its requirements.</summary>
public class QuestDetailUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject detailRoot;

    [Header("Quest Display")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI descriptionLabel;
    [SerializeField] private GameObject ongoingVisual;
    [SerializeField] private GameObject completedVisual;

    [Header("Requirement List")]
    [SerializeField] private Transform requirementRoot;
    [SerializeField] private QuestRequirementRowUI requirementRowPrefab;

    private readonly List<QuestRequirementRowUI> spawnedRows = new List<QuestRequirementRowUI>();

    private void Awake()
    {
        Clear();
    }

    public void Show(QuestManager manager, Key_Quest questKey)
    {
        if (manager == null
            || !manager.TryGetQuestProperty(questKey, out QuestProperty property)
            || !manager.TryGetQuestState(questKey, out QuestProperty.QuestState state)
            || state == QuestProperty.QuestState.Submitted)
        {
            Clear();
            return;
        }

        if (detailRoot != null)
            detailRoot.SetActive(true);

        if (iconImage != null)
            iconImage.sprite = property.icon;

        if (nameLabel != null)
            nameLabel.text = property.displayName;

        if (descriptionLabel != null)
            descriptionLabel.text = property.description;

        bool completed = state == QuestProperty.QuestState.Completed;
        if (ongoingVisual != null)
            ongoingVisual.SetActive(!completed);

        if (completedVisual != null)
            completedVisual.SetActive(completed);

        RebuildRequirementRows(manager.GetRequirementDisplayData(questKey));
    }

    public void Clear()
    {
        if (detailRoot != null)
            detailRoot.SetActive(false);

        ClearRequirementRows();
    }

    private void RebuildRequirementRows(List<QuestProperty.RequirementDisplayData> rows)
    {
        ClearRequirementRows();
        if (requirementRoot == null || requirementRowPrefab == null || rows == null)
            return;

        for (int i = 0; i < rows.Count; i++)
        {
            QuestRequirementRowUI row = Instantiate(requirementRowPrefab, requirementRoot);
            row.Bind(rows[i]);
            spawnedRows.Add(row);
        }
    }

    private void ClearRequirementRows()
    {
        for (int i = 0; i < spawnedRows.Count; i++)
        {
            if (spawnedRows[i] != null)
                Destroy(spawnedRows[i].gameObject);
        }

        spawnedRows.Clear();
    }
}
