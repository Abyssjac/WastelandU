using System;
using System.Collections.Generic;
using System.Linq;
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

        [SerializeField] private UIStyleToolSettings _toolSettings;
        [SerializeField] private UIStyleProperty _directUIStyleProperty;
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
        [SerializeField] private List<TrackedPrefabEntry> _trackedPrefabEntries =
            new List<TrackedPrefabEntry>();
        [SerializeField] private Vector2 _prefabEntryScrollPosition;

        [Serializable]
        private sealed class TrackedPrefabEntry
        {
            [SerializeField] private GameObject _prefab;
            [SerializeField] private bool _isSelected = true;
            [SerializeField] private int _trackerCount;

            public TrackedPrefabEntry(GameObject prefab, bool isSelected, int trackerCount)
            {
                _prefab = prefab;
                _isSelected = isSelected;
                _trackerCount = trackerCount;
            }

            public GameObject Prefab => _prefab;
            public bool IsSelected
            {
                get => _isSelected;
                set => _isSelected = value;
            }

            public int TrackerCount => _trackerCount;
        }

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

        [MenuItem("AbyssTools/UIEssential/UI Track Manager")]
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

            EditorGUILayout.Space(12f);
            DrawTrackedPrefabReplacement(hasCompleteStyle);

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

            SerializedProperty toolSettings = _serializedWindow.FindProperty(nameof(_toolSettings));
            SerializedProperty directStyleProperty = _serializedWindow.FindProperty(nameof(_directUIStyleProperty));
            SerializedProperty styleDatabase = _serializedWindow.FindProperty(nameof(_uiStyleDatabase));
            SerializedProperty activeStyleKey = _serializedWindow.FindProperty(nameof(_activeUIStyleKey));
            UIStyleToolSettings previousToolSettings = _toolSettings;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(toolSettings, new GUIContent("UI Style Tool Settings"));
            EditorGUILayout.PropertyField(
                directStyleProperty,
                new GUIContent("Direct UI Style Property", "Optional. When assigned, this Property is used before the Database and enum fallback."));
            EditorGUILayout.Space(3f);
            EditorGUILayout.PropertyField(styleDatabase, new GUIContent("UI Style Database"));
            EditorGUILayout.PropertyField(activeStyleKey, new GUIContent("Active UI Style", "Used only when Direct UI Style Property is empty."));
            bool changed = EditorGUI.EndChangeCheck();
            _serializedWindow.ApplyModifiedProperties();

            UIStyleProperty activeProperty = TryGetActiveUIStyleProperty(out UIStyleProperty property, out string message)
                ? property
                : null;
            if (changed)
            {
                if (previousToolSettings != _toolSettings)
                {
                    _trackedPrefabEntries.Clear();
                }

                SynchronizeComponentLibrary(activeProperty);
            }

            if (activeProperty != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField("Resolved UI Style Property", activeProperty, typeof(UIStyleProperty), false);
                }

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

        private void DrawTrackedPrefabReplacement(bool hasCompleteStyle)
        {
            EditorGUILayout.LabelField("Replace Tracked Prefab Visuals", EditorStyles.boldLabel);

            if (_toolSettings == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign UI Style Tool Settings to scan its Tracked Prefab Folders recursively.",
                    MessageType.Info);
                return;
            }

            List<string> folderPaths = _toolSettings.GetTrackedPrefabFolderPaths();
            if (folderPaths.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Add one or more Tracked Prefab Folders to the selected UI Style Tool Settings asset.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Configured Folders", $"{folderPaths.Count} recursive folder(s)");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scan Prefab Folders"))
            {
                RefreshTrackedPrefabEntries();
            }

            using (new EditorGUI.DisabledScope(_trackedPrefabEntries.Count == 0))
            {
                if (GUILayout.Button("Select All Prefabs"))
                    SetAllTrackedPrefabSelections(true);

                if (GUILayout.Button("Deselect All Prefabs"))
                    SetAllTrackedPrefabSelections(false);
            }
            EditorGUILayout.EndHorizontal();

            if (_trackedPrefabEntries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No scanned Prefabs are currently listed. Scan the configured folders to find Prefabs that contain UITracker components.",
                    MessageType.Info);
                return;
            }

            int selectedPrefabCount = 0;
            _prefabEntryScrollPosition = EditorGUILayout.BeginScrollView(
                _prefabEntryScrollPosition,
                GUILayout.MaxHeight(190f));
            for (int index = 0; index < _trackedPrefabEntries.Count; index++)
            {
                TrackedPrefabEntry entry = _trackedPrefabEntries[index];
                if (entry == null || entry.Prefab == null)
                {
                    continue;
                }

                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                entry.IsSelected = EditorGUILayout.ToggleLeft(
                    $"{entry.Prefab.name} ({entry.TrackerCount} tracker(s))",
                    entry.IsSelected);
                EditorGUILayout.LabelField(
                    AssetDatabase.GetAssetPath(entry.Prefab),
                    EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                if (entry.IsSelected)
                    selectedPrefabCount++;
            }
            EditorGUILayout.EndScrollView();

            List<CustomUIComponentType> selectedTypes = GetSelectedReplacementTypes();
            using (new EditorGUI.DisabledScope(
                       !hasCompleteStyle
                       || selectedPrefabCount == 0
                       || selectedTypes.Count == 0))
            {
                if (GUILayout.Button($"Replace Selected Prefabs ({selectedPrefabCount})"))
                {
                    ReplaceSelectedPrefabAssets(selectedTypes);
                }
            }
        }

        private void RefreshTrackedPrefabEntries()
        {
            if (_toolSettings == null)
            {
                return;
            }

            var previousSelections = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < _trackedPrefabEntries.Count; index++)
            {
                TrackedPrefabEntry entry = _trackedPrefabEntries[index];
                if (entry?.Prefab == null)
                {
                    continue;
                }

                previousSelections[AssetDatabase.GetAssetPath(entry.Prefab)] = entry.IsSelected;
            }

            _trackedPrefabEntries.Clear();
            List<string> folderPaths = _toolSettings.GetTrackedPrefabFolderPaths();
            if (folderPaths.Count == 0)
            {
                return;
            }

            string prefabOutputRoot = GetPrefabOutputRootPath(_toolSettings);
            string[] guids = AssetDatabase.FindAssets("t:Prefab", folderPaths.ToArray());
            Array.Sort(guids, (left, right) => string.CompareOrdinal(
                AssetDatabase.GUIDToAssetPath(left),
                AssetDatabase.GUIDToAssetPath(right)));

            int skippedGeneratedStylePrefabCount = 0;
            for (int index = 0; index < guids.Length; index++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                if (IsPathInsideFolder(assetPath, prefabOutputRoot))
                {
                    skippedGeneratedStylePrefabCount++;
                    continue;
                }

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                {
                    continue;
                }

                UITracker[] trackers = prefab.GetComponentsInChildren<UITracker>(true);
                if (trackers == null || trackers.Length == 0)
                {
                    continue;
                }

                bool isSelected = !previousSelections.TryGetValue(assetPath, out bool previousSelection)
                                  || previousSelection;
                _trackedPrefabEntries.Add(new TrackedPrefabEntry(prefab, isSelected, trackers.Length));
            }

            if (skippedGeneratedStylePrefabCount > 0)
            {
                Debug.Log(
                    $"[{nameof(UITrackManagerWindow)}] Skipped {skippedGeneratedStylePrefabCount} Prefab(s) inside configured Prefab Output Root '{prefabOutputRoot}' to avoid overwriting generated style templates.");
            }
        }

        private void SetAllTrackedPrefabSelections(bool selected)
        {
            for (int index = 0; index < _trackedPrefabEntries.Count; index++)
            {
                TrackedPrefabEntry entry = _trackedPrefabEntries[index];
                if (entry != null)
                {
                    entry.IsSelected = selected;
                }
            }
        }

        private void ReplaceSelectedPrefabAssets(IReadOnlyCollection<CustomUIComponentType> types)
        {
            int changedPrefabCount = 0;
            int changedTargetCount = 0;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Replace Tracked Prefab UI Visual Sprites");

            try
            {
                for (int index = 0; index < _trackedPrefabEntries.Count; index++)
                {
                    TrackedPrefabEntry entry = _trackedPrefabEntries[index];
                    if (entry?.Prefab == null || !entry.IsSelected)
                    {
                        continue;
                    }

                    string prefabPath = AssetDatabase.GetAssetPath(entry.Prefab);
                    if (string.IsNullOrWhiteSpace(prefabPath))
                    {
                        continue;
                    }

                    GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        UITracker[] trackers = prefabContents.GetComponentsInChildren<UITracker>(true);
                        int changedTargets = ReplaceTypes(
                            types,
                            new List<UITracker>(trackers),
                            $"Prefab asset '{prefabPath}'",
                            false,
                            false);
                        if (changedTargets <= 0)
                        {
                            continue;
                        }

                        PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                        changedPrefabCount++;
                        changedTargetCount += changedTargets;
                    }
                    finally
                    {
                        PrefabUtility.UnloadPrefabContents(prefabContents);
                    }
                }
            }
            finally
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            if (changedPrefabCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                $"[{nameof(UITrackManagerWindow)}] Replaced {changedTargetCount} UI visual target(s) across {changedPrefabCount} selected Prefab asset(s).");
        }

        private static string GetPrefabOutputRootPath(UIStyleToolSettings settings)
        {
            if (settings == null
                || !settings.TryGetBuildPaths(
                    out string prefabOutputRoot,
                    out _,
                    out _,
                    out _))
            {
                return string.Empty;
            }

            return prefabOutputRoot;
        }

        private static bool IsPathInsideFolder(string assetPath, string folderPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || string.IsNullOrWhiteSpace(folderPath))
            {
                return false;
            }

            string normalizedAssetPath = assetPath.Replace("\\", "/");
            string normalizedFolderPath = folderPath.Replace("\\", "/").TrimEnd('/');
            return normalizedAssetPath.StartsWith(
                normalizedFolderPath + "/",
                StringComparison.OrdinalIgnoreCase);
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

        private int ReplaceTypes(
            IReadOnlyCollection<CustomUIComponentType> types,
            List<UITracker> targetTrackers = null,
            string targetDescription = null,
            bool markActiveSceneDirty = true,
            bool manageUndoGroup = true)
        {
            if (!TryGetActiveUIStyleProperty(out UIStyleProperty styleProperty, out string styleMessage))
            {
                Debug.LogWarning($"[{nameof(UITrackManagerWindow)}] {styleMessage}");
                return 0;
            }

            if (!styleProperty.IsComplete)
            {
                Debug.LogWarning(
                    $"[{nameof(UITrackManagerWindow)}] Active UI Style Property '{styleProperty.name}' " +
                    "must assign both a Component Library and a Visual Style Library.",
                    styleProperty);
                return 0;
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
            bool hasButtonVisuals = replaceButton
                                    && visualStyleLibrary.TryGetButtonVisualSprites(out buttonVisualSprites);
            bool hasSlotVisuals = replaceSlot
                                  && visualStyleLibrary.TryGetSlotVisualSprites(out slotVisualSprites);
            bool hasPanelSprite = replacePanel
                                  && visualStyleLibrary.TryGetPanelSprite(out panelSprite);
            bool hasScrollMenuVisuals = replaceScrollMenu
                                        && visualStyleLibrary.TryGetScrollMenuVisualSprites(out scrollMenuVisualSprites);
            bool hasTabVisuals = replaceTab
                                 && visualStyleLibrary.TryGetTabVisualSprites(out tabVisualSprites);
            bool hasToggleVisuals = replaceToggle
                                    && visualStyleLibrary.TryGetToggleVisualSprites(out toggleVisualSprites);
            bool hasProgressBarVisuals = replaceProgressBar
                                         && visualStyleLibrary.TryGetProgressBarVisualSprites(out progressBarVisualSprites);
            bool hasSliderVisuals = replaceSlider
                                    && visualStyleLibrary.TryGetSliderVisualSprites(out sliderVisualSprites);

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
                return 0;

            var desiredTargets = new Dictionary<Image, VisualReplacementTarget>();
            var conflictingImages = new HashSet<Image>();
            int ignoredTrackers = 0;

            List<UITracker> trackers = targetTrackers ?? GetActiveSceneTrackers();
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
                string unchangedTargetDescription = string.IsNullOrWhiteSpace(targetDescription)
                    ? $"active scene '{SceneManager.GetActiveScene().name}'"
                    : targetDescription;
                Debug.Log($"[{nameof(UITrackManagerWindow)}] No UI visual sprites needed replacement in {unchangedTargetDescription}.");
                return 0;
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

            int undoGroup = -1;
            if (manageUndoGroup)
            {
                Undo.IncrementCurrentGroup();
                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Replace UI Visual Sprites");
            }
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

            if (markActiveSceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            if (manageUndoGroup)
            {
                Undo.CollapseUndoOperations(undoGroup);
            }

            string changedTargetDescription = string.IsNullOrWhiteSpace(targetDescription)
                ? $"active scene '{SceneManager.GetActiveScene().name}'"
                : targetDescription;
            string message = $"Replaced {changedTargets.Count} UI visual target(s) in {changedTargetDescription}.";
            if (spriteSwapButtonCount > 0)
                message += $" Updated Sprite Swap states on {spriteSwapButtonCount} Button(s).";

            if (ignoredTrackers > 0 || conflictingImages.Count > 0)
            {
                message += $" Skipped {ignoredTrackers} tracker(s) without a target Image and {conflictingImages.Count} conflicting Image target(s).";
            }

            Debug.Log($"[{nameof(UITrackManagerWindow)}] {message}");
            return changedTargets.Count;
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
            if (_directUIStyleProperty != null)
            {
                property = _directUIStyleProperty;
                message = string.Empty;
                return true;
            }

            if (_activeUIStyleKey == Key_UIStylePP.None)
            {
                property = null;
                message = "Assign a Direct UI Style Property, or assign a UI Style Database and select an Active UI Style.";
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
