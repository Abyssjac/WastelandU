using System;
using UnityEngine;
using UnityEngine.UI;
using Yarn.Unity;
using JackyUtility;

/// <summary>
/// Updates a single dialogue portrait from the #portrait:Key_NPC metadata tag
/// on Yarn lines. For example: "Eli: Hello. #portrait:Eli_Guide".
///
/// Use #portrait:None to hide the configured panel root. A line with no
/// portrait tag keeps the previously displayed portrait unchanged.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialoguePortraitPresenter : DialoguePresenterBase
{
    private const string PortraitTagPrefix = "portrait:";

    [Header("UI")]
    [Tooltip("The complete portrait panel. This object is activated for an NPC portrait and deactivated for #portrait:None.")]
    [SerializeField] private GameObject portraitPanelRoot;

    [Tooltip("The Image inside Portrait Panel Root that receives NPCProperty.portrait.")]
    [SerializeField] private Image portraitImage;

    private NPCDatabase npcDatabase;

    private bool hasWarnedAboutMissingDatabase;
    private void Start()
    {
        GetDatabase();
    }
    public override YarnTask OnDialogueStartedAsync()
    {
        if (!HasValidUIReferences())
        {
            return YarnTask.CompletedTask;
        }

        // Clear a sprite left over from a previous dialogue before the first
        // #portrait tag of this dialogue arrives. The panel itself is visible
        // throughout the dialogue session, as requested.
        portraitImage.sprite = null;
        portraitPanelRoot.SetActive(true);

        return YarnTask.CompletedTask;
    }

    public override YarnTask OnDialogueCompleteAsync()
    {
        if (!HasValidUIReferences())
        {
            return YarnTask.CompletedTask;
        }

        portraitPanelRoot.SetActive(false);
        portraitImage.sprite = null;

        return YarnTask.CompletedTask;
    }

    public override YarnTask RunLineAsync(LocalizedLine line, LineCancellationToken token)
    {
        if (!TryGetMetadataValue(line.Metadata, PortraitTagPrefix, out string portraitKey))
        {
            // No tag deliberately means "keep the current portrait".
            return YarnTask.CompletedTask;
        }

        if (!HasValidUIReferences())
        {
            return YarnTask.CompletedTask;
        }

        if (string.Equals(portraitKey, Key_NPC.None.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            HidePortrait();
            return YarnTask.CompletedTask;
        }

        if (!Enum.TryParse(portraitKey, true, out Key_NPC npcKey) || npcKey == Key_NPC.None)
        {
            Debug.LogWarning($"[DialoguePortraitPresenter] Unknown portrait key '{portraitKey}' on line '{line.TextID}'. " +
                             $"Use a {nameof(Key_NPC)} enum name, such as '#portrait:Botanist', or '#portrait:None'.",
                             this);
            HidePortrait();
            return YarnTask.CompletedTask;
        }

        NPCProperty property = npcDatabase.GetByEnum(npcKey);
        if (property == null)
        {
            Debug.LogWarning($"[DialoguePortraitPresenter] NPCDatabase has no NPCProperty registered for '{npcKey}'.", this);
            HidePortrait();
            return YarnTask.CompletedTask;
        }

        if (property.portrait == null)
        {
            Debug.LogWarning($"[DialoguePortraitPresenter] NPCProperty '{npcKey}' has no portrait sprite assigned.", property);
            HidePortrait();
            return YarnTask.CompletedTask;
        }

        portraitImage.sprite = property.portrait;
        portraitPanelRoot.SetActive(true);

        return YarnTask.CompletedTask;
    }

    public override YarnTask<DialogueOption> RunOptionsAsync(
        DialogueOption[] dialogueOptions,
        LineCancellationToken cancellationToken)
    {
        if (HasValidUIReferences())
        {
            HidePortrait();
        }
        return YarnTask<DialogueOption>.FromResult(null);
    }

    private NPCDatabase GetDatabase()
    {
        if (npcDatabase != null)
        {
            return npcDatabase;
        }

        PropertyDatabaseManager databaseManager = PropertyDatabaseManager.Instance;
        if (databaseManager != null)
        {
            npcDatabase = databaseManager.GetDatabase<NPCDatabase>();
        }

        if (npcDatabase == null && !hasWarnedAboutMissingDatabase)
        {
            hasWarnedAboutMissingDatabase = true;
            Debug.LogWarning("[DialoguePortraitPresenter] NPCDatabase is not assigned and could not be resolved from PropertyDatabaseManager.", this);
        }

        return npcDatabase;
    }

    private void HidePortrait()
    {
        portraitPanelRoot.SetActive(false);
    }

    private bool HasValidUIReferences()
    {
        if (portraitPanelRoot != null && portraitImage != null)
        {
            return true;
        }

        Debug.LogWarning("[DialoguePortraitPresenter] Assign Portrait Panel Root and Portrait Image in the Inspector.", this);
        return false;
    }

    private static bool TryGetMetadataValue(string[] metadata, string prefix, out string value)
    {
        foreach (string tag in metadata)
        {
            string cleanTag = tag.Trim().TrimStart('#');
            if (cleanTag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = cleanTag.Substring(prefix.Length);
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        value = null;
        return false;
    }
}
