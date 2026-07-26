using TMPro;
using UnityEngine;

/// <summary>Renders the text-only paper-strip notification used by quest events.</summary>
public class QuestPaperToastUI : NotificationToastUI
{
    [Header("Display")]
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI detailLabel;

    public override void Bind(NotificationRequest request)
    {
        base.Bind(request);

        if (titleLabel != null)
            titleLabel.text = request.title ?? string.Empty;

        if (detailLabel != null)
            detailLabel.text = request.detail ?? string.Empty;
    }
}
