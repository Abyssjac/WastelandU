using JackyUtility;
using UnityEditor;
using UnityEngine;

/// <summary>Play-mode debugging window for QuestManager.</summary>
public class QuestManagerDebugWindow : DebugEditorWindow<QuestManager>
{
    private Key_Quest selectedQuestKey = Key_Quest.None;

    [MenuItem("Wasteland Debug/Quest Manager")]
    public static void ShowWindow()
    {
        GetWindow<QuestManagerDebugWindow>("Quest Manager Debug").Show();
    }

    protected override void DrawContent()
    {
        QuestManager manager = Target;

        Header("Quest Database");
        Row("Database", manager.Database != null ? manager.Database.name : "<not ready>");
        Row("Submitting", manager.IsSubmitting ? "Yes" : "No");

        Header("Debug Actions");
        selectedQuestKey = (Key_Quest)EditorGUILayout.EnumPopup("Quest Key", selectedQuestKey);

        EditorGUILayout.BeginHorizontal();
        GUI.enabled = selectedQuestKey != Key_Quest.None;
        if (GUILayout.Button("Accept", GUILayout.Height(22)))
            manager.TryAcceptQuest(selectedQuestKey);

        if (GUILayout.Button("Refresh", GUILayout.Height(22)))
            manager.RefreshQuestState(selectedQuestKey);

        if (GUILayout.Button("Submit", GUILayout.Height(22)))
            manager.TrySubmitQuest(selectedQuestKey);

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Clear All Quest Runtime State", GUILayout.Height(22)))
            manager.ClearRuntimeStateForDebug();

        Header("Accepted / Submitted Quests");
        var keys = manager.GetAllRuntimeQuestKeys();
        if (keys.Count == 0)
        {
            EditorGUILayout.HelpBox("No quests have been accepted in this play session.", MessageType.None);
            return;
        }

        for (int i = 0; i < keys.Count; i++)
        {
            Key_Quest key = keys[i];
            manager.TryGetQuestState(key, out QuestProperty.QuestState state);
            manager.TryGetQuestProperty(key, out QuestProperty property);

            ColoredRow(
                key.ToString(),
                state.ToString(),
                state == QuestProperty.QuestState.Completed ? Color.green :
                state == QuestProperty.QuestState.Submitted ? Color.cyan : Color.white);

            if (property != null)
            {
                var rows = manager.GetRequirementDisplayData(key);
                for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                {
                    QuestProperty.RequirementDisplayData row = rows[rowIndex];
                    Row($"  {row.title}", row.detail);
                }
            }
        }
    }
}
