using System;
using UnityEngine;
using Yarn.Unity;

/// <summary>
/// Registers quest-related Yarn commands on the DialogueRunner on this GameObject.
/// Yarn only supplies a stable <see cref="Key_Quest"/> enum name; this component
/// validates it and delegates the actual state change to <see cref="QuestManager"/>.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(DialogueRunner))]
public sealed class DialogueCommandRegister : MonoBehaviour
{
    /// <summary>The Yarn command used to accept a quest.</summary>
    public const string AcceptQuestCommandName = "accept_quest";
    /// <summary>The Yarn function used to query whether a quest was accepted.</summary>
    public const string IsQuestAcceptedFunctionName = "is_quest_accepted";

    private DialogueRunner dialogueRunner;
    private QuestManager questManager;

    private bool commandRegistered;

    private void Reset()
    {
        dialogueRunner = GetComponent<DialogueRunner>();
    }

    private void Awake()
    {
        questManager = GetQuestManager();

        if (dialogueRunner == null)
        {
            dialogueRunner = GetComponentInParent<DialogueRunner>();
        }

        if (dialogueRunner == null)
        {
            Debug.LogError($"[{nameof(DialogueCommandRegister)}] A {nameof(DialogueRunner)} is required.", this);
            return;
        }

        // Register locally on this runner. This avoids a globally attributed
        // command and keeps the dialogue-to-quest boundary explicit in the Inspector.
        dialogueRunner.AddCommandHandler<string>(AcceptQuestCommandName, AcceptQuest);
        dialogueRunner.AddFunction<string, bool>(IsQuestAcceptedFunctionName, IsQuestAccepted);
        commandRegistered = true;
    }

    private void OnDestroy()
    {
        if (commandRegistered && dialogueRunner != null)
        {
            dialogueRunner.RemoveCommandHandler(AcceptQuestCommandName);
            dialogueRunner.RemoveFunction(IsQuestAcceptedFunctionName);
        }
    }

    /// <summary>
    /// Handles: &lt;&lt;accept_quest "Quest_Tutorial_CollectResource"&gt;&gt;.
    /// </summary>
    private void AcceptQuest(string questKeyText)
    {
        if (!Enum.TryParse(questKeyText, true, out Key_Quest questKey)
            || questKey == Key_Quest.None)
        {
            Debug.LogWarning(
                $"[{nameof(DialogueCommandRegister)}] Invalid quest key '{questKeyText}'. " +
                $"Use a {nameof(Key_Quest)} enum name, such as \"{Key_Quest.Quest_Tutorial_CollectResource}\".",
                this);
            return;
        }

        QuestManager manager = GetQuestManager();
        if (manager == null)
        {
            Debug.LogWarning(
                $"[{nameof(DialogueCommandRegister)}] Cannot accept '{questKey}': {nameof(QuestManager)} is unavailable.",
                this);
            return;
        }

        // Dialogue can be revisited. Treat the same accept command as
        // idempotent so a repeat conversation does not create an error.
        if (manager.IsQuestAccepted(questKey))
        {
            return;
        }

        if (!manager.TryAcceptQuest(questKey))
        {
            Debug.LogWarning(
                $"[{nameof(DialogueCommandRegister)}] Quest '{questKey}' was not accepted. " +
                "Check that it is registered in QuestDatabase and has not already been accepted.",
                this);
        }
    }

    private bool IsQuestAccepted(string questKeyText)
    {
        if (!Enum.TryParse(questKeyText, true, out Key_Quest questKey)
            || questKey == Key_Quest.None)
        {
            Debug.LogWarning(
                $"[{nameof(DialogueCommandRegister)}] Invalid quest key '{questKeyText}'. " +
                $"Use a {nameof(Key_Quest)} enum name, such as \"{Key_Quest.Quest_Tutorial_CollectResource}\".",
                this);
            return false;
        }

        QuestManager manager = GetQuestManager();
        if (manager == null)
        {
            Debug.LogWarning(
                $"[{nameof(DialogueCommandRegister)}] Cannot query '{questKey}': {nameof(QuestManager)} is unavailable.",
                this);
            return false;
        }

        return manager.IsQuestAccepted(questKey);
    }

    private QuestManager GetQuestManager()
    {
        if (questManager == null)
        {
            questManager = QuestManager.Instance;
        }

        return questManager;
    }
}
