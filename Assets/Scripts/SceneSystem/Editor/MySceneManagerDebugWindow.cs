using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor debug window for MySceneManager.
/// Open via  Wasteland Debug → Scene Manager  in the Unity menu bar.
/// Requires Play Mode — shows current scene, configured entries, and quick-load buttons.
/// </summary>
public class MySceneManagerDebugWindow : EditorWindow
{
    [MenuItem("Wasteland Debug/Scene Manager")]
    public static void ShowWindow() =>
        GetWindow<MySceneManagerDebugWindow>("Scene Manager").Show();

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
            EditorGUILayout.HelpBox("Enter Play Mode to use the scene manager.", MessageType.Info);
            return;
        }

        var sm = MySceneManager.Instance;
        if (sm == null)
        {
            EditorGUILayout.HelpBox("No MySceneManager found. Make sure its Prefab is in MyGameSystem.allSystemPrefabSingletons.", MessageType.Warning);
            return;
        }

        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        // ── Current Scene ───────────────────────────────────────────────────
        SectionHeader("Current Scene");
        EditorGUILayout.LabelField("Name", sm.GetCurrentSceneName());

        EditorGUILayout.Space(6);

        // ── Quick Load ──────────────────────────────────────────────────────
        SectionHeader("Quick Load");

        Color prevBg = GUI.backgroundColor;

        GUI.backgroundColor = new Color(0.4f, 0.6f, 1.0f);
        if (GUILayout.Button("Go To Main Menu", GUILayout.Height(28)))
            sm.GoToMainMenu();

        GUI.backgroundColor = new Color(0.9f, 0.7f, 0.2f);
        if (GUILayout.Button("Reload Current Scene", GUILayout.Height(28)))
        {
            if (EditorUtility.DisplayDialog(
                    "Reload Scene",
                    $"Reload \"{sm.GetCurrentSceneName()}\"? Unsaved changes will be lost.",
                    "Reload", "Cancel"))
                sm.ReloadCurrentScene();
        }

        GUI.backgroundColor = prevBg;

        EditorGUILayout.Space(6);

        // ── Scene Entries ───────────────────────────────────────────────────
        SectionHeader("Scene Entries");

        var so = new SerializedObject(sm);
        var entriesProp = so.FindProperty("sceneEntries");

        if (entriesProp == null || entriesProp.arraySize == 0)
        {
            EditorGUILayout.HelpBox("No scene entries configured. Add entries in the MySceneManager Inspector.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var entry = entriesProp.GetArrayElementAtIndex(i);
                string displayName = entry.FindPropertyRelative("displayName").stringValue;
                string sceneName   = entry.FindPropertyRelative("sceneName").stringValue;

                bool isCurrent = sceneName == sm.GetCurrentSceneName();

                EditorGUILayout.BeginHorizontal();

                Color prevContent = GUI.contentColor;
                GUI.contentColor = isCurrent ? Color.green : Color.white;
                EditorGUILayout.LabelField(
                    $"{i}: {(string.IsNullOrEmpty(displayName) ? sceneName : displayName)}",
                    isCurrent ? EditorStyles.boldLabel : EditorStyles.label,
                    GUILayout.Width(180));
                GUI.contentColor = prevContent;

                EditorGUILayout.LabelField(sceneName, EditorStyles.miniLabel);

                GUI.backgroundColor = isCurrent ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.4f, 0.8f, 0.4f);
                GUI.enabled = !isCurrent;
                if (GUILayout.Button("Load", GUILayout.Width(50), GUILayout.Height(18)))
                    sm.LoadScene(i);
                GUI.enabled = true;
                GUI.backgroundColor = prevBg;

                EditorGUILayout.EndHorizontal();
            }
        }

        EditorGUILayout.Space(6);

        // ── Manual Load ─────────────────────────────────────────────────────
        SectionHeader("Manual Load by Name");
        EditorGUILayout.HelpBox("Use the Debug Console command:  scene <sceneName>  or  scene ls", MessageType.None);

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
