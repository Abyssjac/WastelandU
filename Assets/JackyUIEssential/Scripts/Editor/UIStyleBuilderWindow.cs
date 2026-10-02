using System.Collections.Generic;
using AbyssToolKitUnity.Utility;
using JackyUtility;
using UnityEditor;
using UnityEngine;

namespace JackyUIEssential.Editor
{
    /// <summary>
    /// Presents the source-folder preflight report and runs the deterministic
    /// UI style assembly only after all 21 required PNG assets are valid.
    /// Each project may create and select any number of tool Settings assets.
    /// </summary>
    public sealed class UIStyleBuilderWindow : EditorWindow
    {
        private const string _windowTitle = "UI Style Builder";

        [SerializeField] private UIStyleToolSettings _settings;
        [SerializeField] private DefaultAsset _sourceFolder;

        private UIStyleAssemblyScanResult _scanResult;
        private Vector2 _scrollPosition;

        [MenuItem("AbyssTools/UIEssential/UI Style Builder")]
        private static void ShowWindow()
        {
            UIStyleBuilderWindow window = GetWindow<UIStyleBuilderWindow>();
            window.titleContent = new GUIContent(_windowTitle);
            window.minSize = new Vector2(660f, 620f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("UI Style Tool Settings", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _settings = (UIStyleToolSettings)EditorGUILayout.ObjectField(
                new GUIContent(
                    "Settings",
                    "Project-owned builder paths, base Component Library, Schema, and tracked Prefab folders."),
                _settings,
                typeof(UIStyleToolSettings),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                _scanResult = null;
            }

            if (_settings == null)
            {
                EditorGUILayout.HelpBox(
                    "Create one or more UI Style Tool Settings assets, configure their folders in the Inspector, then assign one here.",
                    MessageType.Info);
                return;
            }

            DrawSettingsSummary();

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("UI Style Source", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _sourceFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                new GUIContent("Source Folder", "A direct folder inside Assets containing the 21 finalized PNG files."),
                _sourceFolder,
                typeof(DefaultAsset),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                _scanResult = null;
            }

            DrawSourceSummary();

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(_sourceFolder == null))
            {
                if (GUILayout.Button("Scan And Validate"))
                {
                    _scanResult = UIStyleAssemblyBuilder.Scan(_settings, GetSourceFolderPath());
                }
            }

            if (_scanResult == null)
            {
                EditorGUILayout.HelpBox(
                    "Select an Assets folder, then scan it before creating any output assets.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(8f);
            DrawScanReport(_scanResult);

            EditorGUILayout.Space(8f);
            using (new EditorGUI.DisabledScope(!_scanResult.CanBuild))
            {
                if (GUILayout.Button("Build New UI Style"))
                {
                    BuildStyle();
                }
            }

            if (!_scanResult.CanBuild)
            {
                EditorGUILayout.HelpBox(
                    "Build is disabled until every required Asset ID resolves to exactly one correctly-sized PNG, every Settings reference is valid, and all output names are unused.",
                    MessageType.Warning);
            }
        }

        private void DrawSettingsSummary()
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    "Assembly Schema",
                    _settings.AssemblySchema,
                    typeof(UIStyleAssemblySchema),
                    false);
                EditorGUILayout.ObjectField(
                    "Base Component Library",
                    _settings.BaseComponentLibrary,
                    typeof(CustomUIComponentLibrary),
                    false);
            }

            if (!_settings.TryGetBuildPaths(
                    out string prefabOutputRoot,
                    out string visualLibraryOutputFolder,
                    out string componentLibraryOutputFolder,
                    out string message))
            {
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("Prefab Output Root", prefabOutputRoot);
            EditorGUILayout.LabelField("Visual Library Output", visualLibraryOutputFolder);
            EditorGUILayout.LabelField("Component Library Output", componentLibraryOutputFolder);
        }

        private void DrawSourceSummary()
        {
            string sourcePath = GetSourceFolderPath();
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                EditorGUILayout.HelpBox(
                    "Only a direct folder inside Assets/ is accepted. Its final directory name becomes the new Style Name.",
                    MessageType.Info);
                return;
            }

            string normalizedPath = sourcePath.Replace("\\", "/").TrimEnd('/');
            string styleName = System.IO.Path.GetFileName(normalizedPath);
            EditorGUILayout.LabelField("Derived Style Name", styleName);

            if (!_settings.TryGetBuildPaths(
                    out string prefabOutputRoot,
                    out string visualLibraryOutputFolder,
                    out string componentLibraryOutputFolder,
                    out _))
            {
                return;
            }

            EditorGUILayout.LabelField("Prefab Output", $"{prefabOutputRoot}/{styleName}");
            EditorGUILayout.LabelField(
                "Visual Library",
                $"{visualLibraryOutputFolder}/UIVisualStyleLibrary_{styleName}.asset");
            EditorGUILayout.LabelField(
                "Component Library",
                $"{componentLibraryOutputFolder}/CustomUIComponentLibrary_{styleName}.asset");
        }

        private void DrawScanReport(UIStyleAssemblyScanResult result)
        {
            EditorGUILayout.LabelField(
                $"Preflight: {result.ResolvedCount} / {result.RequiredCount} required assets resolved",
                EditorStyles.boldLabel);

            float footerHeight = result.CanBuild ? 48f : 108f;
            float reportHeight = Mathf.Max(215f, position.height - 300f - footerHeight);
            _scrollPosition = EditorGUILayout.BeginScrollView(
                _scrollPosition,
                GUILayout.Height(reportHeight));

            UIStyleAssemblySchema schema = _settings != null ? _settings.AssemblySchema : null;
            if (schema != null)
            {
                for (int index = 0; index < schema.Requirements.Count; index++)
                {
                    UIStyleAssemblyRequirement requirement = schema.Requirements[index];
                    result.Items.TryGetValue(requirement.Slot, out UIStyleAssemblyScanItem item);

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(requirement.Slot.ToString(), GUILayout.Width(170f));
                    EditorGUILayout.LabelField(requirement.AssetId, GUILayout.Width(250f));
                    EditorGUILayout.LabelField(
                        item != null ? item.Status.ToString() : "Not Scanned",
                        GUILayout.Width(120f));
                    EditorGUILayout.EndHorizontal();

                    if (item != null)
                    {
                        EditorGUILayout.LabelField(
                            string.IsNullOrWhiteSpace(item.AssetPath) ? item.Message : item.AssetPath,
                            EditorStyles.miniLabel);
                    }

                    EditorGUILayout.EndVertical();
                }
            }

            DrawMessages("Errors", result.Errors, MessageType.Error);
            DrawMessages("Warnings", result.Warnings, MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawMessages(
            string label,
            IReadOnlyList<string> messages,
            MessageType messageType)
        {
            for (int index = 0; index < messages.Count; index++)
            {
                EditorGUILayout.HelpBox($"{label}: {messages[index]}", messageType);
            }
        }

        private void BuildStyle()
        {
            UIStyleAssemblyBuildResult buildResult =
                UIStyleAssemblyBuilder.Build(_settings, GetSourceFolderPath());
            _scanResult = buildResult.ScanResult;

            if (!buildResult.Succeeded)
            {
                EditorUtility.DisplayDialog(_windowTitle, buildResult.Message, "Close");
                return;
            }

            Selection.activeObject = buildResult.ComponentLibrary;
            EditorGUIUtility.PingObject(buildResult.ComponentLibrary);
            EditorUtility.DisplayDialog(_windowTitle, buildResult.Message, "Close");
        }

        private string GetSourceFolderPath()
        {
            return _sourceFolder == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(_sourceFolder);
        }
    }
}
