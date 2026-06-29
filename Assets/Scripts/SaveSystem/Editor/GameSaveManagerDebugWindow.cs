using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor debug window for GameSaveManager.
/// Open via  Wasteland Debug → Build Save Manager  in the Unity menu bar.
/// Requires Play Mode — shows Save / Load / Delete buttons and current save status.
/// </summary>
public class GameSaveManagerDebugWindow : EditorWindow
{
    [MenuItem("Wasteland Debug/Game Save Manager")]
    public static void ShowWindow() =>
        GetWindow<GameSaveManagerDebugWindow>("Game Save Manager").Show();

    private Vector2 _scrollPos;

    private void Update()
    {
        if (Application.isPlaying)
            Repaint();
    }

    private void OnGUI()
    {
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use the save system.", MessageType.Info);
            return;
        }

        var sm = GameSaveManager.Instance;
        if (sm == null)
        {
            EditorGUILayout.HelpBox("No GameSaveManager found in the active scene.", MessageType.Warning);
            return;
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // ── Save File Info ──────────────────────────────────────────────────
        SectionHeader("Save File");

        string path = sm.GetSavePath();
        EditorGUILayout.LabelField("Path", path);

        bool exists = sm.SaveExists();
        Color prevContent = GUI.contentColor;
        GUI.contentColor = exists ? Color.green : Color.red;
        EditorGUILayout.LabelField("File Exists", exists ? "Yes" : "No", EditorStyles.boldLabel);
        GUI.contentColor = prevContent;

        EditorGUILayout.Space(6);

        // ── Actions ─────────────────────────────────────────────────────────
        SectionHeader("Actions");

        Color prevBg = GUI.backgroundColor;

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Save", GUILayout.Height(28)))
            sm.Save();

        GUI.backgroundColor = new Color(0.4f, 0.6f, 1.0f);
        if (GUILayout.Button("Load", GUILayout.Height(28)))
            sm.Load();

        GUI.backgroundColor = new Color(1.0f, 0.4f, 0.4f);
        if (GUILayout.Button("Delete Save", GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog(
                    "Delete Save",
                    "Delete the save file? This cannot be undone.",
                    "Delete", "Cancel"))
                sm.DeleteSave();
        }

        GUI.backgroundColor = prevBg;

        EditorGUILayout.Space(6);

        // ── Grid Stats ──────────────────────────────────────────────────────
        SectionHeader("Registered Savables");

        EditorGUILayout.LabelField("Count", sm.RegisteredSavableCount.ToString());
        foreach (var key in sm.GetRegisteredSavableKeys())
            EditorGUILayout.LabelField("Key", key);

        EditorGUILayout.EndScrollView();
    }

    private static void SectionHeader(string title)
    {
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        Rect r = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 1f));
        EditorGUILayout.Space(2);
    }
}
