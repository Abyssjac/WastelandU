using System.IO;
using UnityEditor;
using UnityEngine;

namespace JackyUtility
{
    /// <summary>
    /// Editor window that generates a matched Property + Database script pair
    /// for the EnumStringKeyed database system.
    /// Open via  Wasteland Tools > Database Generator.
    /// </summary>
    public class DatabaseGeneratorWindow : EditorWindow
    {
        // ── Inputs ──────────────────────────────────────────────────────────
        private string _enumName = "Key_NewPP";
        private string _propertyClassName = "NewProperty";
        private string _databaseClassName = "NewDatabase";
        private string _outputFolder = "Assets/NewToolKit";

        // ── Scroll ──────────────────────────────────────────────────────────
        private Vector2 _scroll;

        // ── Styles (lazy-init) ───────────────────────────────────────────────
        private GUIStyle _previewStyle;

        [MenuItem("Wasteland Tools/Database Generator")]
        public static void ShowWindow() =>
            GetWindow<DatabaseGeneratorWindow>("Database Generator").Show();

        // ── GUI ─────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            _previewStyle = _previewStyle ?? new GUIStyle(EditorStyles.helpBox)
            {
                fontSize = 11,
                wordWrap = false,
                richText = false,
                fontStyle = FontStyle.Normal,
            };

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // ── Inputs ──────────────────────────────────────────────────────
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Database Generator", EditorStyles.boldLabel);
            DrawSeparator();

            _enumName = EditorGUILayout.TextField("Enum Name", _enumName);
            _propertyClassName = EditorGUILayout.TextField("Property Class", _propertyClassName);
            _databaseClassName = EditorGUILayout.TextField("Database Class", _databaseClassName);
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);

            EditorGUILayout.Space(4);

            // ── File name preview ───────────────────────────────────────────
            EditorGUILayout.LabelField("Files that will be created:", EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"  {_outputFolder}/{_propertyClassName}.cs",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"  {_outputFolder}/{_databaseClassName}.cs",
                EditorStyles.miniLabel);

            DrawSeparator();

            // ── Code preview ────────────────────────────────────────────────
            EditorGUILayout.LabelField("Preview  ──  " + _propertyClassName + ".cs",
                EditorStyles.boldLabel);
            EditorGUILayout.TextArea(BuildPropertySource(), _previewStyle,
                GUILayout.ExpandHeight(false));

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Preview  ──  " + _databaseClassName + ".cs",
                EditorStyles.boldLabel);
            EditorGUILayout.TextArea(BuildDatabaseSource(), _previewStyle,
                GUILayout.ExpandHeight(false));

            DrawSeparator();

            // ── Validation warning ──────────────────────────────────────────
            bool valid = IsInputValid(out string warning);
            if (!valid)
                EditorGUILayout.HelpBox(warning, MessageType.Warning);

            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("Generate Files", GUILayout.Height(30)))
                    GenerateFiles();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.EndScrollView();
        }

        // ── Validation ───────────────────────────────────────────────────────
        private bool IsInputValid(out string warning)
        {
            if (string.IsNullOrWhiteSpace(_enumName))
            { warning = "Enum Name cannot be empty."; return false; }

            if (string.IsNullOrWhiteSpace(_propertyClassName))
            { warning = "Property Class cannot be empty."; return false; }

            if (string.IsNullOrWhiteSpace(_databaseClassName))
            { warning = "Database Class cannot be empty."; return false; }

            if (string.IsNullOrWhiteSpace(_outputFolder))
            { warning = "Output Folder cannot be empty."; return false; }

            if (!IsValidIdentifier(_enumName))
            { warning = $"'{_enumName}' is not a valid C# identifier."; return false; }

            if (!IsValidIdentifier(_propertyClassName))
            { warning = $"'{_propertyClassName}' is not a valid C# identifier."; return false; }

            if (!IsValidIdentifier(_databaseClassName))
            { warning = $"'{_databaseClassName}' is not a valid C# identifier."; return false; }

            warning = null;
            return true;
        }

        private static bool IsValidIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (!char.IsLetter(s[0]) && s[0] != '_') return false;
            for (int i = 1; i < s.Length; i++)
                if (!char.IsLetterOrDigit(s[i]) && s[i] != '_') return false;
            return true;
        }

        // ── Code builders ────────────────────────────────────────────────────
        private string BuildPropertySource()
        {
            string propertyFileName = _propertyClassName + "PP_";
            string lines = string.Join("\n", new[]
            {
                "using JackyUtility;",
                "using UnityEngine;",
                "",
                $"public enum {_enumName}",
                "{",
                "    None = 0,",
                "}",
                "",
                $"[CreateAssetMenu(fileName = \"{propertyFileName}\", menuName = \"AllProperties/{_propertyClassName}\")]",
                $"public class {_propertyClassName} : EnumStringKeyedProperty<{_enumName}>",
                "{",
                "}",
            });
            return lines;
        }

        private string BuildDatabaseSource()
        {
            string databaseFileName = _databaseClassName + "DB_";
            string lines = string.Join("\n", new[]
            {
                "using JackyUtility;",
                "using UnityEngine;",
                "",
                $"[CreateAssetMenu(fileName = \"{databaseFileName}\", menuName = \"AllPropertyDatabases/{_databaseClassName}\")]",
                $"public class {_databaseClassName} : EnumStringKeyedDatabase<{_propertyClassName}, {_enumName}>",
                "{",
                "    [ContextMenu(\"Collect Entries From Folder\")]",
                "    private void CollectEntriesFromFolder()",
                "    {",
                "        base.EditorCollectFromFolder();",
                "    }",
                "}",
            });
            return lines;
        }

        // ── File generation ──────────────────────────────────────────────────
        private void GenerateFiles()
        {
            string folder = _outputFolder.TrimEnd('/');
            string propPath = folder + "/" + _propertyClassName + ".cs";
            string dbPath = folder + "/" + _databaseClassName + ".cs";

            // Overwrite guard
            bool propExists = File.Exists(propPath);
            bool dbExists = File.Exists(dbPath);

            if (propExists || dbExists)
            {
                string existing = (propExists ? propPath + "\n" : "") +
                                  (dbExists ? dbPath : "");
                bool overwrite = EditorUtility.DisplayDialog(
                    "File Already Exists",
                    $"The following file(s) already exist:\n\n{existing}\n\nOverwrite?",
                    "Overwrite", "Cancel");

                if (!overwrite) return;
            }

            // Ensure directory
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            // Write files
            File.WriteAllText(propPath, BuildPropertySource());
            File.WriteAllText(dbPath, BuildDatabaseSource());

            AssetDatabase.Refresh();

            Debug.Log($"[DatabaseGenerator] Created:\n  {propPath}\n  {dbPath}");

            EditorUtility.DisplayDialog(
                "Done",
                $"Generated:\n  {propPath}\n  {dbPath}\n\nWaiting for Unity to compile...",
                "OK");
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static void DrawSeparator()
        {
            EditorGUILayout.Space(4);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 1f));
            EditorGUILayout.Space(4);
        }
    }
}
