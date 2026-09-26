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

        private readonly struct VisualReplacementTarget
        {
            public VisualReplacementTarget(
                Image targetImage,
                Sprite normalSprite,
                Button spriteSwapButton,
                SelectableVisualSprites selectableVisualSprites)
            {
                TargetImage = targetImage;
                NormalSprite = normalSprite;
                SpriteSwapButton = spriteSwapButton;
                SelectableVisualSprites = selectableVisualSprites;
            }

            public Image TargetImage { get; }
            public Sprite NormalSprite { get; }
            public Button SpriteSwapButton { get; }
            public SelectableVisualSprites SelectableVisualSprites { get; }
            public bool ReplacesSpriteSwapState => SpriteSwapButton != null;
        }

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

                if (GUILayout.Button("Replace Slot"))
                    ReplaceUI(CustomUIComponentType.Slot);

                if (GUILayout.Button("Replace Panel"))
                    ReplaceUI(CustomUIComponentType.Panel);

                if (GUILayout.Button("Replace Scroll Menu"))
                    ReplaceUI(CustomUIComponentType.ScrollMenu);

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
        /// Replaces all supported visual sprites in a single undo operation.
        /// </summary>
        public void ReplaceAllUI()
        {
            ReplaceTypes(new[]
            {
                CustomUIComponentType.Button,
                CustomUIComponentType.Slot,
                CustomUIComponentType.Panel,
                CustomUIComponentType.ScrollMenu,
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
            int slotCount = 0;
            int panelCount = 0;
            int scrollMenuCount = 0;

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

                    case CustomUIComponentType.Slot:
                        slotCount++;
                        break;

                    case CustomUIComponentType.Panel:
                        panelCount++;
                        break;

                    case CustomUIComponentType.ScrollMenu:
                        scrollMenuCount++;
                        break;
                }
            }

            EditorGUILayout.LabelField(
                "Active Scene Trackers",
                $"{trackers.Count} total / {trackingCount} tracking / {buttonCount} Button / {slotCount} Slot / {panelCount} Panel / {scrollMenuCount} Scroll Menu");
        }

        private static void ReplaceTypes(IReadOnlyCollection<CustomUIComponentType> types)
        {
            if (!TryGetActiveVisualStyleLibrary(out UIVisualStyleLibrary visualStyleLibrary, out string visualStyleMessage))
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] {visualStyleMessage}");
                return;
            }

            bool replaceButton = false;
            bool replaceSlot = false;
            bool replacePanel = false;
            bool replaceScrollMenu = false;
            foreach (CustomUIComponentType type in types)
            {
                switch (type)
                {
                    case CustomUIComponentType.Button:
                        replaceButton = true;
                        break;

                    case CustomUIComponentType.Slot:
                        replaceSlot = true;
                        break;

                    case CustomUIComponentType.Panel:
                        replacePanel = true;
                        break;

                    case CustomUIComponentType.ScrollMenu:
                        replaceScrollMenu = true;
                        break;
                }
            }

            SelectableVisualSprites buttonVisualSprites = default;
            SelectableVisualSprites slotVisualSprites = default;
            Sprite panelSprite = null;
            Sprite scrollMenuSprite = null;
            bool hasButtonVisuals = !replaceButton
                                    || visualStyleLibrary.TryGetButtonVisualSprites(out buttonVisualSprites);
            bool hasSlotVisuals = !replaceSlot
                                  || visualStyleLibrary.TryGetSlotVisualSprites(out slotVisualSprites);
            bool hasPanelSprite = !replacePanel
                                  || visualStyleLibrary.TryGetPanelSprite(out panelSprite);
            bool hasScrollMenuSprite = !replaceScrollMenu
                                        || visualStyleLibrary.TryGetScrollMenuSprite(out scrollMenuSprite);

            if (replaceButton && !hasButtonVisuals)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Button Normal Sprite configured. No Button visuals were changed.", visualStyleLibrary);
            }

            if (replaceSlot && !hasSlotVisuals)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Slot Normal Sprite configured. No Slot visuals were changed.", visualStyleLibrary);
            }

            if (replacePanel && !hasPanelSprite)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Panel Sprite configured. No Panel visuals were changed.", visualStyleLibrary);
            }

            if (replaceScrollMenu && !hasScrollMenuSprite)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Scroll Menu Sprite configured. No Scroll Menu visuals were changed.", visualStyleLibrary);
            }

            bool hasAnyRequestedVisuals = (replaceButton && hasButtonVisuals)
                                          || (replaceSlot && hasSlotVisuals)
                                          || (replacePanel && hasPanelSprite)
                                          || (replaceScrollMenu && hasScrollMenuSprite);
            if (!hasAnyRequestedVisuals)
                return;

            var desiredTargets = new Dictionary<Image, VisualReplacementTarget>();
            var conflictingImages = new HashSet<Image>();
            int ignoredTrackers = 0;

            List<UITracker> trackers = GetActiveSceneTrackers();
            for (int i = 0; i < trackers.Count; i++)
            {
                UITracker tracker = trackers[i];
                if (!tracker.IsTracking)
                    continue;

                VisualReplacementTarget replacementTarget;
                switch (tracker.Type)
                {
                    case CustomUIComponentType.Button:
                        if (!hasButtonVisuals)
                            continue;

                        if (!TryCreateSelectableReplacementTarget(tracker, buttonVisualSprites, out replacementTarget))
                        {
                            ignoredTrackers++;
                            continue;
                        }

                        break;

                    case CustomUIComponentType.Slot:
                        if (!hasSlotVisuals)
                            continue;

                        if (!TryCreateSelectableReplacementTarget(tracker, slotVisualSprites, out replacementTarget))
                        {
                            ignoredTrackers++;
                            continue;
                        }

                        break;

                    case CustomUIComponentType.Panel:
                        if (!hasPanelSprite)
                            continue;

                        if (!tracker.TryGetTargetImage(out Image panelImage))
                        {
                            ignoredTrackers++;
                            continue;
                        }

                        replacementTarget = new VisualReplacementTarget(panelImage, panelSprite, null, default);
                        break;

                    case CustomUIComponentType.ScrollMenu:
                        if (!hasScrollMenuSprite)
                            continue;

                        if (!tracker.TryGetTargetImage(out Image scrollMenuImage))
                        {
                            ignoredTrackers++;
                            continue;
                        }

                        replacementTarget = new VisualReplacementTarget(scrollMenuImage, scrollMenuSprite, null, default);
                        break;

                    default:
                        continue;
                }

                Image targetImage = replacementTarget.TargetImage;
                if (conflictingImages.Contains(targetImage))
                    continue;

                if (desiredTargets.ContainsKey(targetImage))
                {
                    desiredTargets.Remove(targetImage);
                    conflictingImages.Add(targetImage);
                    continue;
                }

                desiredTargets[targetImage] = replacementTarget;
            }

            var changedTargets = new List<VisualReplacementTarget>();
            foreach (VisualReplacementTarget target in desiredTargets.Values)
            {
                if (NeedsReplacement(target))
                    changedTargets.Add(target);
            }

            if (changedTargets.Count == 0)
            {
                Debug.Log($"[{nameof(UITrackManagerWindow)}] No UI visual sprites needed replacement in active scene '{SceneManager.GetActiveScene().name}'.");
                return;
            }

            var undoObjects = new List<UnityEngine.Object>();
            var seenUndoObjects = new HashSet<UnityEngine.Object>();
            for (int i = 0; i < changedTargets.Count; i++)
            {
                VisualReplacementTarget target = changedTargets[i];
                AddUndoObject(target.TargetImage, undoObjects, seenUndoObjects);
                if (target.ReplacesSpriteSwapState)
                    AddUndoObject(target.SpriteSwapButton, undoObjects, seenUndoObjects);
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace UI Visual Sprites");
            Undo.RecordObjects(undoObjects.ToArray(), "Replace UI Visual Sprites");

            int spriteSwapButtonCount = 0;
            for (int i = 0; i < changedTargets.Count; i++)
            {
                VisualReplacementTarget target = changedTargets[i];
                target.TargetImage.sprite = target.NormalSprite;
                EditorUtility.SetDirty(target.TargetImage);

                if (!target.ReplacesSpriteSwapState)
                    continue;

                SpriteState spriteState = target.SpriteSwapButton.spriteState;
                spriteState.highlightedSprite = target.SelectableVisualSprites.HighlightedSprite;
                spriteState.pressedSprite = target.SelectableVisualSprites.PressedSprite;
                spriteState.selectedSprite = target.SelectableVisualSprites.SelectedSprite;
                spriteState.disabledSprite = target.SelectableVisualSprites.DisabledSprite;
                target.SpriteSwapButton.spriteState = spriteState;
                EditorUtility.SetDirty(target.SpriteSwapButton);
                spriteSwapButtonCount++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Undo.CollapseUndoOperations(undoGroup);

            string message = $"Replaced {changedTargets.Count} UI visual target(s) in active scene '{SceneManager.GetActiveScene().name}'.";
            if (spriteSwapButtonCount > 0)
                message += $" Updated Sprite Swap states on {spriteSwapButtonCount} Button(s).";

            if (ignoredTrackers > 0 || conflictingImages.Count > 0)
            {
                message += $" Skipped {ignoredTrackers} tracker(s) without a target Image and {conflictingImages.Count} conflicting Image target(s).";
            }

            Debug.Log($"[{nameof(UITrackManagerWindow)}] {message}");
        }

        private static bool TryCreateSelectableReplacementTarget(
            UITracker tracker,
            SelectableVisualSprites selectableVisualSprites,
            out VisualReplacementTarget target)
        {
            target = default;
            if (!tracker.TryGetTargetImage(out Image buttonImage) || !tracker.TryGetButton(out Button button))
                return false;

            if (button.transition == Selectable.Transition.SpriteSwap && !tracker.HasMatchingButtonTargetGraphic())
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] UITracker on '{tracker.name}' was skipped because its tracked Image does not match the Sprite Swap Button Target Graphic.", tracker);
                return false;
            }

            Button spriteSwapButton = button.transition == Selectable.Transition.SpriteSwap ? button : null;
            target = new VisualReplacementTarget(buttonImage, selectableVisualSprites.NormalSprite, spriteSwapButton, selectableVisualSprites);
            return true;
        }

        private static bool NeedsReplacement(VisualReplacementTarget target)
        {
            if (target.TargetImage.sprite != target.NormalSprite)
                return true;

            if (!target.ReplacesSpriteSwapState)
                return false;

            SpriteState spriteState = target.SpriteSwapButton.spriteState;
            return spriteState.highlightedSprite != target.SelectableVisualSprites.HighlightedSprite
                   || spriteState.pressedSprite != target.SelectableVisualSprites.PressedSprite
                   || spriteState.selectedSprite != target.SelectableVisualSprites.SelectedSprite
                   || spriteState.disabledSprite != target.SelectableVisualSprites.DisabledSprite;
        }

        private static void AddUndoObject(
            UnityEngine.Object target,
            List<UnityEngine.Object> undoObjects,
            HashSet<UnityEngine.Object> seenUndoObjects)
        {
            if (target != null && seenUndoObjects.Add(target))
                undoObjects.Add(target);
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
