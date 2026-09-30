using System;
using System.Collections.Generic;
using AbyssToolKitUnity.Editor.Utility;
using AbyssToolKitUnity.Utility;
using JackyUtility;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JackyUIEssential.Editor
{
    /// <summary>
    /// Editor-only authority for one active <see cref="UIStyleProperty"/>.
    /// It replaces only configured Image.sprite values in the current active
    /// scene and keeps Custom UI creation synchronized to the same style.
    /// </summary>
    public sealed class UITrackManagerWindow : EditorWindow
    {
        private const string _windowTitle = "UI Track Manager";

        [SerializeField] private UIStyleDatabase _uiStyleDatabase;

        [SerializeField]
        [PropertyDatabaseLink(nameof(_uiStyleDatabase))]
        private Key_UIStylePP _activeUIStyleKey = Key_UIStylePP.None;

        [SerializeField] private bool _replacementSelectionInitialized;
        [SerializeField] private bool _replaceButton = true;
        [SerializeField] private bool _replaceSlot = true;
        [SerializeField] private bool _replacePanel = true;
        [SerializeField] private bool _replaceScrollMenu = true;
        [SerializeField] private bool _replaceTab = true;
        [SerializeField] private bool _replaceToggle = true;
        [SerializeField] private bool _replaceProgressBar = true;
        [SerializeField] private bool _replaceSlider = true;

        private SerializedObject _serializedWindow;

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
            window.minSize = new Vector2(360f, 480f);
            window.Show();
        }

        private void OnEnable()
        {
            _serializedWindow = new SerializedObject(this);

            if (!_replacementSelectionInitialized)
            {
                SetAllReplacementSelections(true);
                _replacementSelectionInitialized = true;
            }

            if (TryGetActiveUIStyleProperty(out UIStyleProperty styleProperty, out _))
                SynchronizeComponentLibrary(styleProperty);
            else
                SynchronizeComponentLibrary(null);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("UI Style Authority", EditorStyles.boldLabel);

            UIStyleProperty styleProperty = DrawUIStylePropertyField();
            bool hasCompleteStyle = styleProperty != null && styleProperty.IsComplete;

            EditorGUILayout.Space(8f);
            DrawActiveSceneSummary();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Replace Active Scene Visuals", EditorStyles.boldLabel);

            DrawReplacementSelection(hasCompleteStyle);

            if (styleProperty == null)
                EditorGUILayout.HelpBox("Select an Active UI Style Property before replacing sprites.", MessageType.Info);
            else if (!styleProperty.IsComplete)
                EditorGUILayout.HelpBox("The active UI Style Property needs both a Component Library and a Visual Style Library.", MessageType.Warning);
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
            ReplaceTypes(GetAllSupportedTypes());
        }

        private UIStyleProperty DrawUIStylePropertyField()
        {
            EnsureSerializedWindow();
            _serializedWindow.Update();

            SerializedProperty styleDatabase = _serializedWindow.FindProperty(nameof(_uiStyleDatabase));
            SerializedProperty activeStyleKey = _serializedWindow.FindProperty(nameof(_activeUIStyleKey));
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(styleDatabase, new GUIContent("UI Style Database"));
            EditorGUILayout.PropertyField(activeStyleKey, new GUIContent("Active UI Style"));
            bool changed = EditorGUI.EndChangeCheck();
            _serializedWindow.ApplyModifiedProperties();

            UIStyleProperty activeProperty = TryGetActiveUIStyleProperty(out UIStyleProperty property, out string message)
                ? property
                : null;
            if (changed)
            {
                SynchronizeComponentLibrary(activeProperty);
            }

            if (activeProperty != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Component Library", activeProperty.ComponentLibrary, typeof(CustomUIComponentLibrary), false);
                    EditorGUILayout.ObjectField("Visual Style Library", activeProperty.VisualStyleLibrary, typeof(UIVisualStyleLibrary), false);
                }
            }
            else if (!string.IsNullOrEmpty(message))
            {
                EditorGUILayout.HelpBox(message, MessageType.Info);
            }

            return activeProperty;
        }

        private void DrawReplacementSelection(bool hasCompleteStyle)
        {
            using (new EditorGUI.DisabledScope(!hasCompleteStyle))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Select All"))
                    SetAllReplacementSelections(true);

                if (GUILayout.Button("Deselect All"))
                    SetAllReplacementSelections(false);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                _replaceButton = EditorGUILayout.ToggleLeft("Button", _replaceButton);
                _replaceSlot = EditorGUILayout.ToggleLeft("Slot", _replaceSlot);
                _replacePanel = EditorGUILayout.ToggleLeft("Panel", _replacePanel);
                _replaceScrollMenu = EditorGUILayout.ToggleLeft("Scroll Menu", _replaceScrollMenu);
                _replaceTab = EditorGUILayout.ToggleLeft("Tab", _replaceTab);
                _replaceToggle = EditorGUILayout.ToggleLeft("Toggle", _replaceToggle);
                _replaceProgressBar = EditorGUILayout.ToggleLeft("Progress Bar", _replaceProgressBar);
                _replaceSlider = EditorGUILayout.ToggleLeft("Slider", _replaceSlider);
                EditorGUILayout.EndVertical();

                List<CustomUIComponentType> selectedTypes = GetSelectedReplacementTypes();
                using (new EditorGUI.DisabledScope(selectedTypes.Count == 0))
                {
                    if (GUILayout.Button($"Replace Selected ({selectedTypes.Count} Types)"))
                        ReplaceTypes(selectedTypes);
                }
            }
        }

        private void SetAllReplacementSelections(bool selected)
        {
            _replaceButton = selected;
            _replaceSlot = selected;
            _replacePanel = selected;
            _replaceScrollMenu = selected;
            _replaceTab = selected;
            _replaceToggle = selected;
            _replaceProgressBar = selected;
            _replaceSlider = selected;
        }

        private List<CustomUIComponentType> GetSelectedReplacementTypes()
        {
            var selectedTypes = new List<CustomUIComponentType>();
            if (_replaceButton)
                selectedTypes.Add(CustomUIComponentType.Button);
            if (_replaceSlot)
                selectedTypes.Add(CustomUIComponentType.Slot);
            if (_replacePanel)
                selectedTypes.Add(CustomUIComponentType.Panel);
            if (_replaceScrollMenu)
                selectedTypes.Add(CustomUIComponentType.ScrollMenu);
            if (_replaceTab)
                selectedTypes.Add(CustomUIComponentType.Tab);
            if (_replaceToggle)
                selectedTypes.Add(CustomUIComponentType.Toggle);
            if (_replaceProgressBar)
                selectedTypes.Add(CustomUIComponentType.ProgressBar);
            if (_replaceSlider)
                selectedTypes.Add(CustomUIComponentType.Slider);
            return selectedTypes;
        }

        private static IReadOnlyCollection<CustomUIComponentType> GetAllSupportedTypes()
        {
            return new[]
            {
                CustomUIComponentType.Button,
                CustomUIComponentType.Slot,
                CustomUIComponentType.Panel,
                CustomUIComponentType.ScrollMenu,
                CustomUIComponentType.Tab,
                CustomUIComponentType.Toggle,
                CustomUIComponentType.ProgressBar,
                CustomUIComponentType.Slider,
            };
        }

        private static void DrawActiveSceneSummary()
        {
            List<UITracker> trackers = GetActiveSceneTrackers();
            int trackingCount = 0;
            int buttonCount = 0;
            int slotCount = 0;
            int panelCount = 0;
            int scrollMenuCount = 0;
            int tabCount = 0;
            int toggleCount = 0;
            int progressBarCount = 0;
            int sliderCount = 0;

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

                    case CustomUIComponentType.Tab:
                        tabCount++;
                        break;

                    case CustomUIComponentType.Toggle:
                        toggleCount++;
                        break;

                    case CustomUIComponentType.ProgressBar:
                        progressBarCount++;
                        break;

                    case CustomUIComponentType.Slider:
                        sliderCount++;
                        break;
                }
            }

            EditorGUILayout.LabelField(
                "Active Scene Trackers",
                $"{trackers.Count} total / {trackingCount} tracking / " +
                $"{buttonCount} Button / {slotCount} Slot / {panelCount} Panel / {scrollMenuCount} Scroll Menu / " +
                $"{tabCount} Tab / {toggleCount} Toggle / {progressBarCount} Progress Bar / {sliderCount} Slider");
        }

        private void ReplaceTypes(IReadOnlyCollection<CustomUIComponentType> types)
        {
            if (!TryGetActiveUIStyleProperty(out UIStyleProperty styleProperty, out string styleMessage))
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] {styleMessage}");
                return;
            }

            if (!styleProperty.IsComplete)
            {
                Debug.LogWarning(
                    $"[{nameof(UITrackManagerWindow)}] Active UI Style Property '{styleProperty.name}' " +
                    "must assign both a Component Library and a Visual Style Library.",
                    styleProperty);
                return;
            }

            UIVisualStyleLibrary visualStyleLibrary = styleProperty.VisualStyleLibrary;

            bool replaceButton = false;
            bool replaceSlot = false;
            bool replacePanel = false;
            bool replaceScrollMenu = false;
            bool replaceTab = false;
            bool replaceToggle = false;
            bool replaceProgressBar = false;
            bool replaceSlider = false;
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

                    case CustomUIComponentType.Tab:
                        replaceTab = true;
                        break;

                    case CustomUIComponentType.Toggle:
                        replaceToggle = true;
                        break;

                    case CustomUIComponentType.ProgressBar:
                        replaceProgressBar = true;
                        break;

                    case CustomUIComponentType.Slider:
                        replaceSlider = true;
                        break;
                }
            }

            SelectableVisualSprites buttonVisualSprites = default;
            SelectableVisualSprites slotVisualSprites = default;
            Sprite panelSprite = null;
            ScrollMenuVisualSprites scrollMenuVisualSprites = default;
            TabVisualSprites tabVisualSprites = default;
            ToggleVisualSprites toggleVisualSprites = default;
            ProgressBarVisualSprites progressBarVisualSprites = default;
            SliderVisualSprites sliderVisualSprites = default;
            bool hasButtonVisuals = !replaceButton
                                    || visualStyleLibrary.TryGetButtonVisualSprites(out buttonVisualSprites);
            bool hasSlotVisuals = !replaceSlot
                                  || visualStyleLibrary.TryGetSlotVisualSprites(out slotVisualSprites);
            bool hasPanelSprite = !replacePanel
                                  || visualStyleLibrary.TryGetPanelSprite(out panelSprite);
            bool hasScrollMenuVisuals = !replaceScrollMenu
                                        || visualStyleLibrary.TryGetScrollMenuVisualSprites(out scrollMenuVisualSprites);
            bool hasTabVisuals = !replaceTab
                                 || visualStyleLibrary.TryGetTabVisualSprites(out tabVisualSprites);
            bool hasToggleVisuals = !replaceToggle
                                    || visualStyleLibrary.TryGetToggleVisualSprites(out toggleVisualSprites);
            bool hasProgressBarVisuals = !replaceProgressBar
                                         || visualStyleLibrary.TryGetProgressBarVisualSprites(out progressBarVisualSprites);
            bool hasSliderVisuals = !replaceSlider
                                    || visualStyleLibrary.TryGetSliderVisualSprites(out sliderVisualSprites);

            if (replaceButton && !hasButtonVisuals)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Button sprites configured. No Button visuals were changed.", visualStyleLibrary);
            }

            if (replaceSlot && !hasSlotVisuals)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Slot sprites configured. No Slot visuals were changed.", visualStyleLibrary);
            }

            if (replacePanel && !hasPanelSprite)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Panel Sprite configured. No Panel visuals were changed.", visualStyleLibrary);
            }

            if (replaceScrollMenu && !hasScrollMenuVisuals)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Scroll Menu Sprites configured. No Scroll Menu visuals were changed.", visualStyleLibrary);
            }

            if (replaceTab && !hasTabVisuals)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Tab Sprites configured. No Tab visuals were changed.", visualStyleLibrary);
            }

            if (replaceToggle && !hasToggleVisuals)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Toggle Sprites configured. No Toggle visuals were changed.", visualStyleLibrary);
            }

            if (replaceProgressBar && !hasProgressBarVisuals)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Progress Bar Sprites configured. No Progress Bar visuals were changed.", visualStyleLibrary);
            }

            if (replaceSlider && !hasSliderVisuals)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] Visual Style Library '{visualStyleLibrary.name}' has no Slider Sprites configured. No Slider visuals were changed.", visualStyleLibrary);
            }

            bool hasAnyRequestedVisuals = (replaceButton && hasButtonVisuals)
                                          || (replaceSlot && hasSlotVisuals)
                                          || (replacePanel && hasPanelSprite)
                                          || (replaceScrollMenu && hasScrollMenuVisuals)
                                          || (replaceTab && hasTabVisuals)
                                          || (replaceToggle && hasToggleVisuals)
                                          || (replaceProgressBar && hasProgressBarVisuals)
                                          || (replaceSlider && hasSliderVisuals);
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

                        if (!TryAddSelectableReplacementTargets(
                                tracker,
                                buttonVisualSprites,
                                desiredTargets,
                                conflictingImages))
                        {
                            ignoredTrackers++;
                        }

                        continue;

                    case CustomUIComponentType.Slot:
                        if (!hasSlotVisuals)
                            continue;

                        if (!TryAddSelectableReplacementTargets(
                                tracker,
                                slotVisualSprites,
                                desiredTargets,
                                conflictingImages))
                        {
                            ignoredTrackers++;
                        }

                        continue;

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
                        if (!hasScrollMenuVisuals)
                            continue;

                        if (!TryAddScrollMenuReplacementTargets(
                                tracker,
                                scrollMenuVisualSprites,
                                desiredTargets,
                                conflictingImages))
                        {
                            ignoredTrackers++;
                        }

                        continue;

                    case CustomUIComponentType.Tab:
                        if (!hasTabVisuals)
                            continue;

                        if (!TryAddTabReplacementTargets(
                                tracker,
                                tabVisualSprites,
                                desiredTargets,
                                conflictingImages))
                        {
                            ignoredTrackers++;
                        }

                        continue;

                    case CustomUIComponentType.Toggle:
                        if (!hasToggleVisuals)
                            continue;

                        if (!TryAddToggleReplacementTargets(
                                tracker,
                                toggleVisualSprites,
                                desiredTargets,
                                conflictingImages))
                        {
                            ignoredTrackers++;
                        }

                        continue;

                    case CustomUIComponentType.ProgressBar:
                        if (!hasProgressBarVisuals)
                            continue;

                        if (!TryAddProgressBarReplacementTargets(
                                tracker,
                                progressBarVisualSprites,
                                desiredTargets,
                                conflictingImages))
                        {
                            ignoredTrackers++;
                        }

                        continue;

                    case CustomUIComponentType.Slider:
                        if (!hasSliderVisuals)
                            continue;

                        if (!TryAddSliderReplacementTargets(
                                tracker,
                                sliderVisualSprites,
                                desiredTargets,
                                conflictingImages))
                        {
                            ignoredTrackers++;
                        }

                        continue;

                    default:
                        continue;
                }

                AddDesiredTarget(replacementTarget, desiredTargets, conflictingImages);
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

        private static bool TryAddSelectableReplacementTargets(
            UITracker tracker,
            SelectableVisualSprites sprites,
            Dictionary<Image, VisualReplacementTarget> desiredTargets,
            HashSet<Image> conflictingImages)
        {
            if (sprites.NormalSprite == null
                || !TryCreateSelectableReplacementTarget(tracker, sprites, out VisualReplacementTarget mainTarget))
            {
                return false;
            }

            AddDesiredTarget(mainTarget, desiredTargets, conflictingImages);
            return true;
        }

        private static bool TryAddScrollMenuReplacementTargets(
            UITracker tracker,
            ScrollMenuVisualSprites sprites,
            Dictionary<Image, VisualReplacementTarget> desiredTargets,
            HashSet<Image> conflictingImages)
        {
            if (!tracker.TryGetScrollMenuImages(
                    out Image panelImage,
                    out Image slidingAreaImage,
                    out Image handleImage))
            {
                return false;
            }

            bool addedAnyTarget = false;
            addedAnyTarget |= TryAddCompositeImageTarget(
                tracker,
                "Scroll Menu",
                "Panel",
                panelImage,
                sprites.PanelSprite,
                desiredTargets,
                conflictingImages);
            addedAnyTarget |= TryAddCompositeImageTarget(
                tracker,
                "Scroll Menu",
                "Sliding Area",
                slidingAreaImage,
                sprites.SlidingAreaSprite,
                desiredTargets,
                conflictingImages);
            addedAnyTarget |= TryAddCompositeImageTarget(
                tracker,
                "Scroll Menu",
                "Handle",
                handleImage,
                sprites.HandleSprite,
                desiredTargets,
                conflictingImages);
            return addedAnyTarget;
        }

        private static bool TryAddTabReplacementTargets(
            UITracker tracker,
            TabVisualSprites sprites,
            Dictionary<Image, VisualReplacementTarget> desiredTargets,
            HashSet<Image> conflictingImages)
        {
            if (!tracker.TryGetTabImages(out Image backgroundImage, out Image selectedImage))
                return false;

            bool addedAnyTarget = false;
            addedAnyTarget |= TryAddCompositeImageTarget(tracker, "Tab", "Background", backgroundImage, sprites.BackgroundSprite, desiredTargets, conflictingImages);
            addedAnyTarget |= TryAddCompositeImageTarget(tracker, "Tab", "Selected", selectedImage, sprites.SelectedSprite, desiredTargets, conflictingImages);
            return addedAnyTarget;
        }

        private static bool TryAddToggleReplacementTargets(
            UITracker tracker,
            ToggleVisualSprites sprites,
            Dictionary<Image, VisualReplacementTarget> desiredTargets,
            HashSet<Image> conflictingImages)
        {
            if (!tracker.TryGetToggleImages(out Image backgroundImage, out Image checkmarkImage))
                return false;

            bool addedAnyTarget = false;
            addedAnyTarget |= TryAddCompositeImageTarget(tracker, "Toggle", "Background", backgroundImage, sprites.BackgroundSprite, desiredTargets, conflictingImages);
            addedAnyTarget |= TryAddCompositeImageTarget(tracker, "Toggle", "Checkmark", checkmarkImage, sprites.CheckmarkSprite, desiredTargets, conflictingImages);
            return addedAnyTarget;
        }

        private static bool TryAddProgressBarReplacementTargets(
            UITracker tracker,
            ProgressBarVisualSprites sprites,
            Dictionary<Image, VisualReplacementTarget> desiredTargets,
            HashSet<Image> conflictingImages)
        {
            if (!tracker.TryGetProgressBarImages(out Image backgroundImage, out Image fillImage))
                return false;

            bool addedAnyTarget = false;
            addedAnyTarget |= TryAddCompositeImageTarget(tracker, "Progress Bar", "Background", backgroundImage, sprites.BackgroundSprite, desiredTargets, conflictingImages);
            addedAnyTarget |= TryAddCompositeImageTarget(tracker, "Progress Bar", "Fill", fillImage, sprites.FillSprite, desiredTargets, conflictingImages);
            return addedAnyTarget;
        }

        private static bool TryAddSliderReplacementTargets(
            UITracker tracker,
            SliderVisualSprites sprites,
            Dictionary<Image, VisualReplacementTarget> desiredTargets,
            HashSet<Image> conflictingImages)
        {
            if (!tracker.TryGetSliderImages(out Image backgroundImage, out Image fillImage, out Image handleImage))
                return false;

            bool addedAnyTarget = false;
            addedAnyTarget |= TryAddCompositeImageTarget(tracker, "Slider", "Background", backgroundImage, sprites.BackgroundSprite, desiredTargets, conflictingImages);
            addedAnyTarget |= TryAddCompositeImageTarget(tracker, "Slider", "Fill", fillImage, sprites.FillSprite, desiredTargets, conflictingImages);
            addedAnyTarget |= TryAddCompositeImageTarget(tracker, "Slider", "Handle", handleImage, sprites.HandleSprite, desiredTargets, conflictingImages);
            return addedAnyTarget;
        }

        private static bool TryAddCompositeImageTarget(
            UITracker tracker,
            string componentName,
            string targetName,
            Image targetImage,
            Sprite sprite,
            Dictionary<Image, VisualReplacementTarget> desiredTargets,
            HashSet<Image> conflictingImages)
        {
            if (sprite == null)
                return false;

            if (targetImage == null)
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] {componentName} tracker on '{tracker.name}' has a {targetName} Sprite configured in the active style, but no matching {targetName} Image reference.", tracker);
                return false;
            }

            AddDesiredTarget(
                new VisualReplacementTarget(targetImage, sprite, null, default),
                desiredTargets,
                conflictingImages);
            return true;
        }

        private static void AddDesiredTarget(
            VisualReplacementTarget replacementTarget,
            Dictionary<Image, VisualReplacementTarget> desiredTargets,
            HashSet<Image> conflictingImages)
        {
            Image targetImage = replacementTarget.TargetImage;
            if (targetImage == null || conflictingImages.Contains(targetImage))
                return;

            if (desiredTargets.ContainsKey(targetImage))
            {
                desiredTargets.Remove(targetImage);
                conflictingImages.Add(targetImage);
                return;
            }

            desiredTargets[targetImage] = replacementTarget;
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

        private bool TryGetActiveUIStyleProperty(out UIStyleProperty property, out string message)
        {
            if (_activeUIStyleKey == Key_UIStylePP.None)
            {
                property = null;
                message = "No active UI Style Property is selected.";
                return false;
            }

            if (_uiStyleDatabase == null)
            {
                property = null;
                message = "Assign a UI Style Database in UI Track Manager before selecting a style.";
                return false;
            }

            property = _uiStyleDatabase.GetByEnum(_activeUIStyleKey);
            if (property == null)
            {
                message = $"UI Style Database '{_uiStyleDatabase.name}' has no UIStyleProperty for '{_activeUIStyleKey}'. " +
                          "Create the Property SO and collect the database entries.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private void EnsureSerializedWindow()
        {
            if (_serializedWindow == null)
                _serializedWindow = new SerializedObject(this);
        }

        private static void SynchronizeComponentLibrary(UIStyleProperty property)
        {
            CustomUICreationMenu.SetActiveComponentLibrary(
                property != null ? property.ComponentLibrary : null);
        }
    }
}
