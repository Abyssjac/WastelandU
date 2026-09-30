using UnityEditor;
using UnityEngine;

namespace JackyUIEssential.Editor
{
    /// <summary>
    /// Presents the source-folder preflight report and runs the deterministic
    /// UI style assembly only after all 21 required PNG assets are valid.
    /// </summary>
    public sealed class UIStyleBuilderWindow : EditorWindow
    {
        private const string _windowTitle = "UI Style Builder";

        [SerializeField] private DefaultAsset _sourceFolder;
        [SerializeField] private UIStyleAssemblySchema _schema;

        private UIStyleAssemblyScanResult _scanResult;
        private Vector2 _scrollPosition;

        [MenuItem("Tools/Jacky UI Essential/UI Style Builder")]
        private static void ShowWindow()
        {
            UIStyleBuilderWindow window = GetWindow<UIStyleBuilderWindow>();
            window.titleContent = new GUIContent(_windowTitle);
            window.minSize = new Vector2(660f, 620f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_schema == null)
            {
                _schema = AssetDatabase.LoadAssetAtPath<UIStyleAssemblySchema>(
                    UIStyleAssemblyBuilder.DefaultSchemaAssetPath);
            }
        }

        private void OnGUI()
        {
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

            EditorGUI.BeginChangeCheck();
            _schema = (UIStyleAssemblySchema)EditorGUILayout.ObjectField(
                new GUIContent("Assembly Schema"),
                _schema,
                typeof(UIStyleAssemblySchema),
                false);

            if (EditorGUI.EndChangeCheck())
            {
                _scanResult = null;
            }

            DrawSchemaCreation();
            DrawSourceSummary();

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(_sourceFolder == null || _schema == null))
            {
                if (GUILayout.Button("Scan And Validate"))
                {
                    _scanResult = UIStyleAssemblyBuilder.Scan(_schema, GetSourceFolderPath());
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
                    "Build is disabled until every required Asset ID resolves to exactly one correctly-sized PNG and the output Style Name is unused.",
                    MessageType.Warning);
            }
        }

        private void DrawSchemaCreation()
        {
            if (_schema != null)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "The shared UI Style Assembly Schema is missing. Create it once to define the fixed 21 UI Sprite slots.",
                MessageType.Warning);

            if (GUILayout.Button("Create Default Assembly Schema"))
            {
                UIStyleAssemblySchema existing =
                    AssetDatabase.LoadAssetAtPath<UIStyleAssemblySchema>(
                        UIStyleAssemblyBuilder.DefaultSchemaAssetPath);

                if (existing != null)
                {
                    _schema = existing;
                    return;
                }

                UIStyleAssemblySchema schema =
                    UIStyleAssemblySchema.CreateDefaultInstance();
                AssetDatabase.CreateAsset(
                    schema,
                    UIStyleAssemblyBuilder.DefaultSchemaAssetPath);
                AssetDatabase.SaveAssets();

                _schema = schema;
                Selection.activeObject = schema;
                EditorGUIUtility.PingObject(schema);
            }
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
            EditorGUILayout.LabelField(
                "Prefab Output",
                $"{UIStyleAssemblyBuilder.PrefabOutputRoot}/{styleName}");
            EditorGUILayout.LabelField(
                "Visual Library",
                $"{UIStyleAssemblyBuilder.ScriptableObjectOutputRoot}/UIVisualStyleLibrary_{styleName}.asset");
            EditorGUILayout.LabelField(
                "Component Library",
                $"{UIStyleAssemblyBuilder.ScriptableObjectOutputRoot}/CustomUIComponentLibrary_{styleName}.asset");
        }

        private void DrawScanReport(UIStyleAssemblyScanResult result)
        {
            EditorGUILayout.LabelField(
                $"Preflight: {result.ResolvedCount} / {result.RequiredCount} required assets resolved",
                EditorStyles.boldLabel);

            float footerHeight = result.CanBuild ? 48f : 108f;
            float reportHeight = Mathf.Max(215f, position.height - 265f - footerHeight);
            _scrollPosition = EditorGUILayout.BeginScrollView(
                _scrollPosition,
                GUILayout.Height(reportHeight));

            for (int index = 0; index < _schema.Requirements.Count; index++)
            {
                UIStyleAssemblyRequirement requirement = _schema.Requirements[index];
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

            DrawMessages("Errors", result.Errors, MessageType.Error);
            DrawMessages("Warnings", result.Warnings, MessageType.Warning);

            EditorGUILayout.EndScrollView();
        }

        private static void DrawMessages(
            string label,
            System.Collections.Generic.IReadOnlyList<string> messages,
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
                UIStyleAssemblyBuilder.Build(_schema, GetSourceFolderPath());
            _scanResult = buildResult.ScanResult;

            if (!buildResult.Succeeded)
            {
                EditorUtility.DisplayDialog(
                    _windowTitle,
                    buildResult.Message,
                    "Close");
                return;
            }

            Selection.activeObject = buildResult.ComponentLibrary;
            EditorGUIUtility.PingObject(buildResult.ComponentLibrary);

            EditorUtility.DisplayDialog(
                _windowTitle,
                buildResult.Message,
                "Close");
        }

        private string GetSourceFolderPath()
        {
            return _sourceFolder == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(_sourceFolder);
        }
    }
}
