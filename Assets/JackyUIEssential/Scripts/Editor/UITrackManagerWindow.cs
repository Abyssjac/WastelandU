using System;
using System.Collections.Generic;
using AbyssToolKitUnity.Editor.Utility;
using AbyssToolKitUnity.Utility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JackyUIEssential.Editor
{
    /// <summary>
    /// Editor-only authority for the active Custom UI Component Library and
    /// visual style. It replaces only configured Image.sprite values in the
    /// current active scene.
    /// </summary>
    public sealed class UITrackManagerWindow : EditorWindow
    {
        private const string _activeVisualStyleEditorPrefsKey = "JackyUIEssential.UITrackManager.ActiveVisualStyleLibraryGuid";
        private const string _windowTitle = "UI Track Manager";

        [MenuItem("Tools/Jacky UI Essential/UI Track Manager")]
        private static void ShowWindow()
        {
            UITrackManagerWindow window = GetWindow<UITrackManagerWindow>();
            window.titleContent = new GUIContent(_windowTitle);
            window.minSize = new Vector2(360f, 280f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Library Authority", EditorStyles.boldLabel);

            DrawComponentLibraryField();
            UIVisualStyleLibrary visualStyleLibrary = DrawVisualStyleLibraryField();

            EditorGUILayout.Space(8f);
            DrawActiveSceneSummary();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Replace Active Scene Visuals", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(visualStyleLibrary == null))
            {
                if (GUILayout.Button("Replace Button"))
                    ReplaceUI(CustomUIComponentType.Button);

                if (GUILayout.Button("Replace Panel"))
                    ReplaceUI(CustomUIComponentType.Panel);

                if (GUILayout.Button("Replace All"))
                    ReplaceAllUI();
            }

            if (visualStyleLibrary == null)
            {
                EditorGUILayout.HelpBox("Select an Active Visual Style Library before replacing sprites.", MessageType.Info);
            }
        }

        /// <summary>
        /// Replaces the configured target Image sprites for one supported type
        /// in the current active scene.
        /// </summary>
        public void ReplaceUI(CustomUIComponentType type)
        {
            ReplaceTypes(new[] { type });
        }

        /// <summary>
        /// Replaces Button and Panel visual sprites in a single undo operation.
        /// </summary>
        public void ReplaceAllUI()
        {
            ReplaceTypes(new[]
            {
                CustomUIComponentType.Button,
                CustomUIComponentType.Panel,
            });
        }

        private static void DrawComponentLibraryField()
        {
            CustomUIComponentLibrary activeLibrary = null;
            bool hasActiveLibrary = CustomUICreationMenu.TryGetActiveComponentLibrary(out activeLibrary, out string message);

            EditorGUI.BeginChangeCheck();
            CustomUIComponentLibrary selectedLibrary = (CustomUIComponentLibrary)EditorGUILayout.ObjectField(
                new GUIContent("Active Component Library"),
                hasActiveLibrary ? activeLibrary : null,
                typeof(CustomUIComponentLibrary),
                false);

            if (EditorGUI.EndChangeCheck())
            {
                CustomUICreationMenu.SetActiveComponentLibrary(selectedLibrary);
            }

            if (!hasActiveLibrary)
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }
        }

        private static UIVisualStyleLibrary DrawVisualStyleLibraryField()
        {
            UIVisualStyleLibrary activeLibrary = TryGetActiveVisualStyleLibrary(out UIVisualStyleLibrary library, out string message)
                ? library
                : null;

            EditorGUI.BeginChangeCheck();
            UIVisualStyleLibrary selectedLibrary = (UIVisualStyleLibrary)EditorGUILayout.ObjectField(
                new GUIContent("Active Visual Style Library"),
                activeLibrary,
                typeof(UIVisualStyleLibrary),
                false);

            if (EditorGUI.EndChangeCheck())
            {
                SetActiveVisualStyleLibrary(selectedLibrary);
                activeLibrary = selectedLibrary;
                message = string.Empty;
            }

            if (activeLibrary == null && !string.IsNullOrEmpty(message))
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }

            return activeLibrary;
        }

        private static void DrawActiveSceneSummary()
        {
            List<UITracker> trackers = GetActiveSceneTrackers();
            int trackingCount = 0;
            int buttonCount = 0;
            int panelCount = 0;

            for (int i = 0; i < trackers.Count; i++)
            {
                UITracker tracker = trackers[i];
                if (!tracker.IsTracking)
                    continue;

                trackingCount++;
                switch (tracker.Type)
                {
                    case CustomUIComponentType.Button:
                        buttonCount++;
                        break;

                    case CustomUIComponentType.Panel:
                        panelCount++;
                        break;
                }
            }

            EditorGUILayout.LabelField(
                "Active Scene Trackers",
                $"{trackers.Count} total / {trackingCount} tracking / {buttonCount} Button / {panelCount} Panel");
        }

        private static void ReplaceTypes(IReadOnlyCollection<CustomUIComponentType> types)
        {
            if (!TryGetActiveVisualStyleLibrary(out UIVisualStyleLibrary visualStyleLibrary, out string visualStyleMessage))
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] {visualStyleMessage}");
                return;
            }

            var replacementSprites = new Dictionary<CustomUIComponentType, Sprite>();
            foreach (CustomUIComponentType type in types)
            {
                if (!visualStyleLibrary.TryGetSprite(type, out Sprite sprite))
                {
                    Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Sprite configured for '{type}'. No '{type}' images were changed.", visualStyleLibrary);
                    continue;
                }

                replacementSprites[type] = sprite;
            }

            if (replacementSprites.Count == 0)
                return;

            var desiredSprites = new Dictionary<Image, Sprite>();
            var conflictingImages = new HashSet<Image>();
            int ignoredTrackers = 0;

            List<UITracker> trackers = GetActiveSceneTrackers();
            for (int i = 0; i < trackers.Count; i++)
            {
                UITracker tracker = trackers[i];
                if (!tracker.IsTracking || !replacementSprites.TryGetValue(tracker.Type, out Sprite replacementSprite))
                    continue;

                if (!tracker.TryGetTargetImage(out Image targetImage))
                {
                    ignoredTrackers++;
                    continue;
                }

                if (conflictingImages.Contains(targetImage))
                    continue;

                if (desiredSprites.TryGetValue(targetImage, out Sprite existingSprite) && existingSprite != replacementSprite)
                {
                    desiredSprites.Remove(targetImage);
                    conflictingImages.Add(targetImage);
                    continue;
                }

                desiredSprites[targetImage] = replacementSprite;
            }

            var changedImages = new List<Image>();
            foreach (KeyValuePair<Image, Sprite> entry in desiredSprites)
            {
                if (entry.Key.sprite != entry.Value)
                    changedImages.Add(entry.Key);
            }

            if (changedImages.Count == 0)
            {
                Debug.Log($"[{nameof(UITrackManagerWindow)}] No Image sprites needed replacement in active scene '{SceneManager.GetActiveScene().name}'.");
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace UI Visual Sprites");
            Undo.RecordObjects(changedImages.ToArray(), "Replace UI Visual Sprites");

            foreach (KeyValuePair<Image, Sprite> entry in desiredSprites)
            {
                if (entry.Key.sprite == entry.Value)
                    continue;

                entry.Key.sprite = entry.Value;
                EditorUtility.SetDirty(entry.Key);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);

            string message = $"Replaced {changedImages.Count} UI Image sprite(s) in active scene '{SceneManager.GetActiveScene().name}'.";
            if (ignoredTrackers > 0 || conflictingImages.Count > 0)
            {
                message += $" Skipped {ignoredTrackers} tracker(s) without a target Image and {conflictingImages.Count} conflicting Image target(s).";
            }

            Debug.Log($"[{nameof(UITrackManagerWindow)}] {message}");
        }

        private static List<UITracker> GetActiveSceneTrackers()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            UITracker[] allTrackers = Resources.FindObjectsOfTypeAll<UITracker>();
            var activeSceneTrackers = new List<UITracker>();

            for (int i = 0; i < allTrackers.Length; i++)
            {
                UITracker tracker = allTrackers[i];
                if (tracker == null
                    || EditorUtility.IsPersistent(tracker)
                    || tracker.gameObject.scene != activeScene)
                {
                    continue;
                }

                activeSceneTrackers.Add(tracker);
            }

            return activeSceneTrackers;
        }

        private static bool TryGetActiveVisualStyleLibrary(out UIVisualStyleLibrary library, out string message)
        {
            string guid = EditorPrefs.GetString(_activeVisualStyleEditorPrefsKey, string.Empty);
            if (string.IsNullOrWhiteSpace(guid))
            {
                library = null;
                message = "No active UI Visual Style Library is selected.";
                return false;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                library = null;
                message = "The active UI Visual Style Library could not be found. Select another library.";
                return false;
            }

            library = AssetDatabase.LoadAssetAtPath<UIVisualStyleLibrary>(assetPath);
            if (library == null)
            {
                message = "The active UI Visual Style Library could not be loaded. Select another library.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static void SetActiveVisualStyleLibrary(UIVisualStyleLibrary library)
        {
            if (library == null)
            {
                EditorPrefs.DeleteKey(_activeVisualStyleEditorPrefsKey);
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(library);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] '{library.name}' is not a persistent asset and cannot be selected as the active Visual Style Library.", library);
                return;
            }

            EditorPrefs.SetString(_activeVisualStyleEditorPrefsKey, guid);
        }
    }
}
