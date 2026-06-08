using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace JackyUtility
{
    /// <summary>
    /// Editor window for managing Property ScriptableObjects in the EnumStringKeyed system.
    /// Open via  Wasteland Tools > Property SO Manager.
    ///
    /// Features:
    ///   Open   – select an existing SO by enum key and open it in the Inspector / Project view
    ///   Create – add a new enum member to the .cs source, then create a matching SO asset
    ///   Validate – scan a folder and report duplicate enum keys
    ///   Manage – add / remove SO entries from a Database asset
    /// </summary>
    public class PropertySOManagerWindow : EditorWindow
    {
        // ── Shared ───────────────────────────────────────────────────────────
        private ScriptableObject _databaseSO;
        private Type             _entryType;   // TEntry  e.g. NPCProperty
        private Type             _enumType;    // TEnum   e.g. Key_NPC

        // ── Foldout states ───────────────────────────────────────────────────
        private bool _foldOpen     = true;
        private bool _foldCreate   = false;
        private bool _foldValidate = false;
        private bool _foldManage   = false;

        // ── Open tab ─────────────────────────────────────────────────────────
        private string   _openScanFolder   = "Assets";
        private int      _openEnumIndex    = 0;
        private string[] _openEnumNames;       // enum member names
        private int[]    _openEnumInts;        // enum member underlying int values
        private bool[]   _openHasSO;           // whether each enum member has an asset
        private string[] _openDisplayLabels;   // labels shown in popup

        // ── Create tab ───────────────────────────────────────────────────────
        private string _createEnumName   = "";
        private int    _createEnumInt    = 0;
        private string _createEnumFile   = "";   // path to the .cs containing the enum
        private string _createSOName     = "";
        private string _createSOFolder   = "Assets";
        private string _createStringKey  = "";

        // ── Validate tab ─────────────────────────────────────────────────────
        private string       _validateFolder    = "Assets";
        private List<string> _validateResults   = new List<string>();
        private bool         _validateDone      = false;
        private Vector2      _validateScroll;

        // ── Manage tab ───────────────────────────────────────────────────────
        private string          _manageScanFolder   = "Assets";
        private int             _manageEnumIndex    = 0;
        private string[]        _manageEnumNames;
        private int[]           _manageEnumInts;
        private bool[]          _manageInDatabase;
        private string[]        _manageDisplayLabels;
        private ScriptableObject _manageDragSO;
        private Vector2          _manageScroll;

        // ── Scroll ───────────────────────────────────────────────────────────
        private Vector2 _scroll;

        // ────────────────────────────────────────────────────────────────────
        [MenuItem("Wasteland Tools/Property SO Manager")]
        public static void ShowWindow() =>
            GetWindow<PropertySOManagerWindow>("Property SO Manager").Show();

        // ── OnGUI ────────────────────────────────────────────────────────────
        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Property SO Manager", EditorStyles.boldLabel);
            DrawSeparator();

            // ── Database SO field (always visible) ──────────────────────────
            EditorGUI.BeginChangeCheck();
            _databaseSO = (ScriptableObject)EditorGUILayout.ObjectField(
                "Database SO", _databaseSO, typeof(ScriptableObject), false);
            if (EditorGUI.EndChangeCheck())
                OnDatabaseChanged();

            if (_databaseSO == null)
            {
                EditorGUILayout.HelpBox(
                    "Drag a Database ScriptableObject here to begin.\n" +
                    "e.g. BuildableDatabase, NPCDatabase …",
                    MessageType.Info);
                EditorGUILayout.EndScrollView();
                return;
            }

            if (_entryType == null || _enumType == null)
            {
                EditorGUILayout.HelpBox(
                    $"{_databaseSO.GetType().Name} does not appear to be a valid " +
                    "EnumStringKeyedDatabase. Check that it inherits the base class correctly.",
                    MessageType.Error);
                EditorGUILayout.EndScrollView();
                return;
            }

            EditorGUILayout.LabelField(
                $"Entry: {_entryType.Name}   Enum: {_enumType.Name}",
                EditorStyles.miniLabel);

            DrawSeparator();

            // ── Foldout tabs ─────────────────────────────────────────────────
            DrawOpenSection();
            DrawCreateSection();
            DrawValidateSection();
            DrawManageSection();

            EditorGUILayout.Space(6);
            EditorGUILayout.EndScrollView();
        }

        // ════════════════════════════════════════════════════════════════════
        // Open
        // ════════════════════════════════════════════════════════════════════
        private void DrawOpenSection()
        {
            _foldOpen = EditorGUILayout.BeginFoldoutHeaderGroup(_foldOpen, "Open");
            if (_foldOpen)
            {
                EditorGUI.indentLevel++;

                // Scan folder
                EditorGUILayout.BeginHorizontal();
                _openScanFolder = EditorGUILayout.TextField("Scan Folder", _openScanFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(55)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Select Scan Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(picked))
                        _openScanFolder = AbsoluteToAssetPath(picked);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Scan"))
                    RefreshOpenEnumList(_openScanFolder);

                if (_openEnumNames != null && _openEnumNames.Length > 0)
                {
                    _openEnumIndex = Mathf.Clamp(_openEnumIndex, 0, _openDisplayLabels.Length - 1);
                    _openEnumIndex = EditorGUILayout.Popup("Enum Key", _openEnumIndex, _openDisplayLabels);

                    bool hasSO = _openHasSO != null && _openEnumIndex < _openHasSO.Length
                                 && _openHasSO[_openEnumIndex];

                    if (!hasSO)
                    {
                        EditorGUILayout.HelpBox(
                            "No SO asset found for this enum key in the scanned folder.",
                            MessageType.Warning);
                    }

                    using (new EditorGUI.DisabledScope(!hasSO))
                    {
                        if (GUILayout.Button("Open in Inspector"))
                            OpenSO(_openScanFolder, _openEnumInts[_openEnumIndex]);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Press Scan to load enum keys.", MessageType.None);
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════
        // Create
        // ════════════════════════════════════════════════════════════════════
        private void DrawCreateSection()
        {
            _foldCreate = EditorGUILayout.BeginFoldoutHeaderGroup(_foldCreate, "Create");
            if (_foldCreate)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.LabelField("Step 1: Add Enum Member to .cs File",
                    EditorStyles.boldLabel);

                _createEnumName = EditorGUILayout.TextField("Enum Member Name", _createEnumName);
                _createEnumInt  = EditorGUILayout.IntField("Enum Int Value",    _createEnumInt);

                EditorGUILayout.BeginHorizontal();
                _createEnumFile = EditorGUILayout.TextField("Enum .cs File", _createEnumFile);
                if (GUILayout.Button("Browse", GUILayout.Width(55)))
                {
                    string picked = EditorUtility.OpenFilePanel(
                        "Select .cs file containing the enum", "Assets", "cs");
                    if (!string.IsNullOrEmpty(picked))
                        _createEnumFile = AbsoluteToAssetPath(picked);
                }
                EditorGUILayout.EndHorizontal();

                bool step1Valid = IsValidIdentifier(_createEnumName)
                               && !string.IsNullOrWhiteSpace(_createEnumFile)
                               && File.Exists(_createEnumFile);

                if (!step1Valid)
                    EditorGUILayout.HelpBox(
                        "Fill in a valid enum member name, int value, and .cs file path.",
                        MessageType.Warning);

                using (new EditorGUI.DisabledScope(!step1Valid))
                {
                    if (GUILayout.Button("Step 1: Add Enum Key  (triggers recompile)"))
                        AddEnumMemberToFile();
                }

                DrawSeparator();
                EditorGUILayout.LabelField("Step 2: Create SO Asset", EditorStyles.boldLabel);

                _createSOName   = EditorGUILayout.TextField("Asset File Name", _createSOName);
                _createStringKey = EditorGUILayout.TextField("String Key",     _createStringKey);

                EditorGUILayout.BeginHorizontal();
                _createSOFolder = EditorGUILayout.TextField("Output Folder", _createSOFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(55)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(picked))
                        _createSOFolder = AbsoluteToAssetPath(picked);
                }
                EditorGUILayout.EndHorizontal();

                bool step2Valid = IsValidIdentifier(_createEnumName)
                               && !string.IsNullOrWhiteSpace(_createSOName)
                               && !string.IsNullOrWhiteSpace(_createSOFolder);

                if (!step2Valid)
                    EditorGUILayout.HelpBox(
                        "Fill in all fields. Enum member name must match what was added in Step 1.",
                        MessageType.Warning);

                using (new EditorGUI.DisabledScope(!step2Valid))
                {
                    if (GUILayout.Button("Step 2: Create SO Asset"))
                        CreateSOAsset();
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════
        // Validate
        // ════════════════════════════════════════════════════════════════════
        private void DrawValidateSection()
        {
            _foldValidate = EditorGUILayout.BeginFoldoutHeaderGroup(_foldValidate, "Validate");
            if (_foldValidate)
            {
                EditorGUI.indentLevel++;

                EditorGUILayout.BeginHorizontal();
                _validateFolder = EditorGUILayout.TextField("Scan Folder", _validateFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(55)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Select Folder to Validate", "Assets", "");
                    if (!string.IsNullOrEmpty(picked))
                        _validateFolder = AbsoluteToAssetPath(picked);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Run Validate"))
                    RunValidate();

                if (_validateDone)
                {
                    if (_validateResults.Count == 0)
                    {
                        EditorGUILayout.HelpBox(
                            "No duplicate enum keys found. All clear!", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            $"{_validateResults.Count} duplicate group(s) found:",
                            MessageType.Error);

                        _validateScroll = EditorGUILayout.BeginScrollView(
                            _validateScroll, GUILayout.MaxHeight(200));

                        foreach (string line in _validateResults)
                            EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);

                        EditorGUILayout.EndScrollView();
                    }
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════
        // Manage
        // ════════════════════════════════════════════════════════════════════
        private void DrawManageSection()
        {
            _foldManage = EditorGUILayout.BeginFoldoutHeaderGroup(_foldManage, "Manage Database Entries");
            if (_foldManage)
            {
                EditorGUI.indentLevel++;

                // Scan folder (shared with Database scanFolder default)
                EditorGUILayout.BeginHorizontal();
                _manageScanFolder = EditorGUILayout.TextField("Scan Folder", _manageScanFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(55)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Select Scan Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(picked))
                        _manageScanFolder = AbsoluteToAssetPath(picked);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Scan"))
                    RefreshManageList(_manageScanFolder);

                DrawSeparator();

                // ── Select by enum key drop-down ─────────────────────────────
                EditorGUILayout.LabelField("Select by Enum Key", EditorStyles.boldLabel);

                if (_manageEnumNames != null && _manageEnumNames.Length > 0)
                {
                    _manageEnumIndex = Mathf.Clamp(_manageEnumIndex, 0,
                        _manageDisplayLabels.Length - 1);
                    _manageEnumIndex = EditorGUILayout.Popup(
                        "Enum Key", _manageEnumIndex, _manageDisplayLabels);

                    bool inDB    = _manageInDatabase != null
                                && _manageEnumIndex < _manageInDatabase.Length
                                && _manageInDatabase[_manageEnumIndex];

                    bool hasSO   = IsManagedSOPresent(_manageEnumIndex);

                    EditorGUILayout.BeginHorizontal();

                    using (new EditorGUI.DisabledScope(inDB || !hasSO))
                    {
                        if (GUILayout.Button("Add to Database"))
                            ManageAddByEnum(_manageEnumInts[_manageEnumIndex]);
                    }

                    using (new EditorGUI.DisabledScope(!inDB))
                    {
                        if (GUILayout.Button("Remove from Database"))
                            ManageRemoveByEnum(_manageEnumInts[_manageEnumIndex]);
                    }

                    EditorGUILayout.EndHorizontal();

                    if (!hasSO && !inDB)
                        EditorGUILayout.HelpBox(
                            "No SO found for this key in the scanned folder.",
                            MessageType.Warning);
                    if (inDB)
                        EditorGUILayout.HelpBox(
                            "This key is already in the Database entries.",
                            MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Press Scan to load enum keys.", MessageType.None);
                }

                DrawSeparator();

                // ── Drag-in SO directly ──────────────────────────────────────
                EditorGUILayout.LabelField("Or Drag In SO Directly", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                _manageDragSO = (ScriptableObject)EditorGUILayout.ObjectField(
                    "Property SO", _manageDragSO, typeof(ScriptableObject), false);
                EditorGUI.EndChangeCheck();

                if (_manageDragSO != null)
                {
                    bool dragInDB  = IsSoInDatabase(_manageDragSO);
                    bool validType = _entryType != null
                                  && _entryType.IsInstanceOfType(_manageDragSO);

                    if (!validType)
                    {
                        EditorGUILayout.HelpBox(
                            $"This SO is not of type {_entryType?.Name ?? "unknown"}.",
                            MessageType.Error);
                    }
                    else
                    {
                        EditorGUILayout.BeginHorizontal();

                        using (new EditorGUI.DisabledScope(dragInDB))
                        {
                            if (GUILayout.Button("Add to Database"))
                                ManageAddSO(_manageDragSO);
                        }

                        using (new EditorGUI.DisabledScope(!dragInDB))
                        {
                            if (GUILayout.Button("Remove from Database"))
                                ManageRemoveSO(_manageDragSO);
                        }

                        EditorGUILayout.EndHorizontal();

                        EditorGUILayout.LabelField(
                            dragInDB ? "Status: In Database" : "Status: Not in Database",
                            EditorStyles.miniLabel);
                    }
                }

                // ── Current entries list ─────────────────────────────────────
                DrawSeparator();
                EditorGUILayout.LabelField("Current Database Entries", EditorStyles.boldLabel);

                var so = new SerializedObject(_databaseSO);
                var entriesProp = so.FindProperty("entries");
                if (entriesProp != null)
                {
                    _manageScroll = EditorGUILayout.BeginScrollView(
                        _manageScroll, GUILayout.MaxHeight(160));

                    for (int i = 0; i < entriesProp.arraySize; i++)
                    {
                        var elem = entriesProp.GetArrayElementAtIndex(i);
                        var asset = elem.objectReferenceValue;
                        if (asset == null) continue;

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField(asset.name, GUILayout.ExpandWidth(true));
                        if (GUILayout.Button("Ping", GUILayout.Width(40)))
                            EditorGUIUtility.PingObject(asset);
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.EndScrollView();
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════
        // Logic — Database reflection
        // ════════════════════════════════════════════════════════════════════
        private void OnDatabaseChanged()
        {
            _entryType  = null;
            _enumType   = null;
            _openEnumNames  = null;
            _manageEnumNames = null;
            _validateDone   = false;

            if (_databaseSO == null) return;

            // Walk up the inheritance chain looking for EnumStringKeyedDatabase<,>
            Type dbType = _databaseSO.GetType();
            while (dbType != null && dbType != typeof(object))
            {
                if (dbType.IsGenericType)
                {
                    Type def = dbType.GetGenericTypeDefinition();
                    // Match both JackyUtility.EnumStringKeyedDatabase<,> and unqualified
                    if (def.Name.StartsWith("EnumStringKeyedDatabase"))
                    {
                        Type[] args = dbType.GetGenericArguments();
                        if (args.Length == 2)
                        {
                            _entryType = args[0];
                            _enumType  = args[1];
                            break;
                        }
                    }
                }
                dbType = dbType.BaseType;
            }

            // Seed scan folders from Database's scanFolder if available
            if (_databaseSO != null)
            {
                var soSerialized = new SerializedObject(_databaseSO);
                var scanProp = soSerialized.FindProperty("scanFolder");
                if (scanProp != null && !string.IsNullOrWhiteSpace(scanProp.stringValue))
                {
                    string folder = "Assets/" + scanProp.stringValue.TrimStart('/');
                    _openScanFolder    = folder;
                    _validateFolder    = folder;
                    _manageScanFolder  = folder;
                    _createSOFolder    = folder;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Logic — Open
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Scans the given folder for all TEntry assets and builds the popup lists.
        /// </summary>
        private void RefreshOpenEnumList(string folder)
        {
            if (_enumType == null) return;

            Array enumValues = Enum.GetValues(_enumType);
            var   enumNames  = Enum.GetNames(_enumType);
            var   assets     = LoadAllEntryAssets(folder);

            // Build a set of int values that have an asset
            var coveredInts = new HashSet<int>();
            foreach (var asset in assets)
            {
                int iv = GetEnumIntFromSO(asset);
                coveredInts.Add(iv);
            }

            _openEnumNames    = enumNames;
            _openEnumInts     = new int[enumValues.Length];
            _openHasSO        = new bool[enumValues.Length];
            _openDisplayLabels = new string[enumValues.Length];

            for (int i = 0; i < enumValues.Length; i++)
            {
                int iv = Convert.ToInt32(enumValues.GetValue(i));
                _openEnumInts[i]      = iv;
                _openHasSO[i]         = coveredInts.Contains(iv);
                _openDisplayLabels[i] = _openHasSO[i]
                    ? $"● {enumNames[i]}  ({iv})"
                    : $"○ {enumNames[i]}  ({iv})";
            }

            _openEnumIndex = 0;
        }

        private void OpenSO(string folder, int enumIntValue)
        {
            var assets = LoadAllEntryAssets(folder);
            foreach (var asset in assets)
            {
                if (GetEnumIntFromSO(asset) == enumIntValue)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                    return;
                }
            }
            Debug.LogWarning($"[PropertySOManager] No asset found for int key {enumIntValue} in {folder}");
        }

        // ════════════════════════════════════════════════════════════════════
        // Logic — Create
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Step 1: Insert the new enum member into the .cs source file, then refresh.
        /// This causes a domain reload — the window will reset after this.
        /// </summary>
        private void AddEnumMemberToFile()
        {
            if (!File.Exists(_createEnumFile))
            {
                EditorUtility.DisplayDialog("Error", $"File not found:\n{_createEnumFile}", "OK");
                return;
            }

            string source = File.ReadAllText(_createEnumFile);
            string enumDecl = $"enum {_enumType.Name}";

            int enumStart = source.IndexOf(enumDecl, StringComparison.Ordinal);
            if (enumStart < 0)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Could not find 'enum {_enumType.Name}' inside:\n{_createEnumFile}", "OK");
                return;
            }

            int braceOpen = source.IndexOf('{', enumStart);
            if (braceOpen < 0)
            {
                EditorUtility.DisplayDialog("Error", "Malformed enum block (no opening brace).", "OK");
                return;
            }

            // Check for duplicate int value or name
            string newLine = $"    {_createEnumName} = {_createEnumInt},";

            if (source.Contains($"{_createEnumName} =") || source.Contains($"= {_createEnumInt},"))
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Possible Duplicate",
                    $"The name '{_createEnumName}' or value {_createEnumInt} may already exist in the enum.\nProceed anyway?",
                    "Yes", "Cancel");
                if (!proceed) return;
            }

            // Insert before the closing brace of the enum block
            int braceClose = FindMatchingBrace(source, braceOpen);
            if (braceClose < 0)
            {
                EditorUtility.DisplayDialog("Error", "Could not locate the closing brace of the enum.", "OK");
                return;
            }

            // Insert the new line just before the closing brace
            string updated = source.Insert(braceClose, newLine + "\n");
            File.WriteAllText(_createEnumFile, updated);

            AssetDatabase.Refresh();
            // Domain reload will happen here — window state is lost intentionally.
        }

        /// <summary>
        /// Step 2: Create the SO asset. Run this after Step 1's recompile has finished.
        /// </summary>
        private void CreateSOAsset()
        {
            if (_entryType == null)
            {
                EditorUtility.DisplayDialog("Error", "Entry type is not resolved. Re-drag the Database SO.", "OK");
                return;
            }

            // Verify enum member exists after recompile
            if (!Enum.IsDefined(_enumType, _createEnumName))
            {
                EditorUtility.DisplayDialog("Error",
                    $"Enum member '{_createEnumName}' is not defined in {_enumType.Name}.\n" +
                    "Run Step 1 first and wait for Unity to finish recompiling.", "OK");
                return;
            }

            string folder = _createSOFolder.TrimEnd('/');
            if (!AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            string assetPath = $"{folder}/{_createSOName}.asset";
            if (File.Exists(assetPath))
            {
                bool overwrite = EditorUtility.DisplayDialog("File Exists",
                    $"{assetPath} already exists. Overwrite?", "Overwrite", "Cancel");
                if (!overwrite) return;
            }

            var instance = ScriptableObject.CreateInstance(_entryType);
            AssetDatabase.CreateAsset(instance, assetPath);

            // Write enumKey and stringKey via SerializedObject
            var serialized   = new SerializedObject(instance);
            var enumKeyProp  = serialized.FindProperty("enumKey");
            var stringKeyProp = serialized.FindProperty("stringKey");

            if (enumKeyProp != null)
            {
                enumKeyProp.intValue = (int)Enum.Parse(_enumType, _createEnumName);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            if (stringKeyProp != null && !string.IsNullOrEmpty(_createStringKey))
            {
                stringKeyProp.stringValue = _createStringKey;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorGUIUtility.PingObject(instance);
            Selection.activeObject = instance;

            Debug.Log($"[PropertySOManager] Created {_entryType.Name} at {assetPath}");
            EditorUtility.DisplayDialog("Done",
                $"Created {_createSOName}.asset\nat {folder}", "OK");
        }

        // ════════════════════════════════════════════════════════════════════
        // Logic — Validate
        // ════════════════════════════════════════════════════════════════════
        private void RunValidate()
        {
            _validateResults.Clear();
            _validateDone = false;

            if (_entryType == null) return;

            var assets = LoadAllEntryAssets(_validateFolder);

            // Group by enum int value
            var groups = new Dictionary<int, List<string>>();
            foreach (var asset in assets)
            {
                int iv = GetEnumIntFromSO(asset);
                if (!groups.ContainsKey(iv))
                    groups[iv] = new List<string>();
                groups[iv].Add(AssetDatabase.GetAssetPath(asset));
            }

            foreach (var kvp in groups)
            {
                if (kvp.Value.Count <= 1) continue;

                string enumName = Enum.IsDefined(_enumType, kvp.Key)
                    ? Enum.GetName(_enumType, kvp.Key)
                    : $"(int){kvp.Key}";

                _validateResults.Add($"── Duplicate key: {enumName} ({kvp.Key}) ──");
                foreach (string path in kvp.Value)
                    _validateResults.Add($"    {path}");
            }

            _validateDone = true;
        }

        // ════════════════════════════════════════════════════════════════════
        // Logic — Manage
        // ════════════════════════════════════════════════════════════════════
        private void RefreshManageList(string folder)
        {
            if (_enumType == null) return;

            Array enumValues = Enum.GetValues(_enumType);
            string[] enumNames = Enum.GetNames(_enumType);

            var assets = LoadAllEntryAssets(folder);
            var coveredInts = new HashSet<int>();
            foreach (var a in assets) coveredInts.Add(GetEnumIntFromSO(a));

            var dbEntryInts = GetDatabaseEntryInts();

            _manageEnumNames     = enumNames;
            _manageEnumInts      = new int[enumValues.Length];
            _manageInDatabase    = new bool[enumValues.Length];
            _manageDisplayLabels = new string[enumValues.Length];

            for (int i = 0; i < enumValues.Length; i++)
            {
                int iv = Convert.ToInt32(enumValues.GetValue(i));
                _manageEnumInts[i]   = iv;
                _manageInDatabase[i] = dbEntryInts.Contains(iv);

                bool hasSO = coveredInts.Contains(iv);
                string indbTag = _manageInDatabase[i] ? " [DB]" : "";
                string soTag   = hasSO ? "●" : "○";
                _manageDisplayLabels[i] = $"{soTag} {enumNames[i]}  ({iv}){indbTag}";
            }

            _manageEnumIndex = 0;
        }

        private bool IsManagedSOPresent(int enumIdx)
        {
            if (_manageEnumInts == null || enumIdx >= _manageEnumInts.Length) return false;
            var assets = LoadAllEntryAssets(_manageScanFolder);
            int target = _manageEnumInts[enumIdx];
            foreach (var a in assets)
                if (GetEnumIntFromSO(a) == target) return true;
            return false;
        }

        private void ManageAddByEnum(int enumIntValue)
        {
            var assets = LoadAllEntryAssets(_manageScanFolder);
            foreach (var asset in assets)
            {
                if (GetEnumIntFromSO(asset) == enumIntValue)
                {
                    ManageAddSO(asset);
                    return;
                }
            }
            Debug.LogWarning($"[PropertySOManager] No asset found for int key {enumIntValue}");
        }

        private void ManageRemoveByEnum(int enumIntValue)
        {
            var assets = LoadAllEntryAssets(_manageScanFolder);
            foreach (var asset in assets)
            {
                if (GetEnumIntFromSO(asset) == enumIntValue)
                {
                    ManageRemoveSO(asset);
                    return;
                }
            }
            Debug.LogWarning($"[PropertySOManager] No asset found for int key {enumIntValue}");
        }

        private void ManageAddSO(ScriptableObject so)
        {
            if (_databaseSO == null || so == null) return;

            var serialized  = new SerializedObject(_databaseSO);
            var entriesProp = serialized.FindProperty("entries");
            if (entriesProp == null) return;

            // Check not already present
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                if (entriesProp.GetArrayElementAtIndex(i).objectReferenceValue == so)
                {
                    Debug.Log($"[PropertySOManager] {so.name} is already in the database.");
                    return;
                }
            }

            entriesProp.arraySize++;
            entriesProp.GetArrayElementAtIndex(entriesProp.arraySize - 1).objectReferenceValue = so;

            // Sort entries by enumKey int value
            SortEntriesProperty(entriesProp);

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_databaseSO);
            AssetDatabase.SaveAssets();

            // Refresh manage list
            RefreshManageList(_manageScanFolder);
            Debug.Log($"[PropertySOManager] Added {so.name} to {_databaseSO.name}");
        }

        private void ManageRemoveSO(ScriptableObject so)
        {
            if (_databaseSO == null || so == null) return;

            var serialized  = new SerializedObject(_databaseSO);
            var entriesProp = serialized.FindProperty("entries");
            if (entriesProp == null) return;

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                if (entriesProp.GetArrayElementAtIndex(i).objectReferenceValue == so)
                {
                    entriesProp.DeleteArrayElementAtIndex(i);
                    // Unity may null the slot first; delete twice if needed
                    if (i < entriesProp.arraySize &&
                        entriesProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        entriesProp.DeleteArrayElementAtIndex(i);

                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_databaseSO);
                    AssetDatabase.SaveAssets();

                    RefreshManageList(_manageScanFolder);
                    Debug.Log($"[PropertySOManager] Removed {so.name} from {_databaseSO.name}");
                    return;
                }
            }
        }

        private bool IsSoInDatabase(ScriptableObject so)
        {
            if (_databaseSO == null || so == null) return false;
            var serialized  = new SerializedObject(_databaseSO);
            var entriesProp = serialized.FindProperty("entries");
            if (entriesProp == null) return false;
            for (int i = 0; i < entriesProp.arraySize; i++)
                if (entriesProp.GetArrayElementAtIndex(i).objectReferenceValue == so) return true;
            return false;
        }

        private HashSet<int> GetDatabaseEntryInts()
        {
            var result = new HashSet<int>();
            if (_databaseSO == null) return result;

            var serialized  = new SerializedObject(_databaseSO);
            var entriesProp = serialized.FindProperty("entries");
            if (entriesProp == null) return result;

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var obj = entriesProp.GetArrayElementAtIndex(i).objectReferenceValue;
                if (obj is ScriptableObject so)
                    result.Add(GetEnumIntFromSO(so));
            }
            return result;
        }

        /// <summary>
        /// Sort the entries SerializedProperty array by the enumKey int value of each element.
        /// </summary>
        private void SortEntriesProperty(SerializedProperty entriesProp)
        {
            int count = entriesProp.arraySize;
            if (count <= 1) return;

            // Extract (index, intValue) pairs
            var pairs = new List<(int idx, int val)>(count);
            for (int i = 0; i < count; i++)
            {
                var obj = entriesProp.GetArrayElementAtIndex(i).objectReferenceValue;
                int val = obj is ScriptableObject so ? GetEnumIntFromSO(so) : 0;
                pairs.Add((i, val));
            }
            pairs.Sort((a, b) => a.val.CompareTo(b.val));

            // If already sorted, skip
            bool sorted = true;
            for (int i = 0; i < count; i++)
                if (pairs[i].idx != i) { sorted = false; break; }
            if (sorted) return;

            // Bubble-sort the SerializedProperty array to match desired order
            // We do a simple selection-sort via MoveArrayElement
            for (int i = 0; i < count - 1; i++)
            {
                if (pairs[i].idx == i) continue;
                // Find where element i is now
                int cur = -1;
                for (int j = i + 1; j < count; j++)
                    if (pairs[j].idx == i) { cur = j; break; }
                if (cur < 0) continue;

                entriesProp.MoveArrayElement(cur, i);
                // Update pairs to reflect the move
                var tmp = pairs[cur];
                pairs.RemoveAt(cur);
                pairs.Insert(i, tmp);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Shared helpers
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Load all assets of type TEntry from the given folder path.
        /// </summary>
        private List<ScriptableObject> LoadAllEntryAssets(string folder)
        {
            var result = new List<ScriptableObject>();
            if (_entryType == null) return result;

            string searchPath = string.IsNullOrWhiteSpace(folder) ? "Assets" : folder;
            if (!AssetDatabase.IsValidFolder(searchPath))
            {
                Debug.LogWarning($"[PropertySOManager] Folder not found: {searchPath}");
                return result;
            }

            string[] guids = AssetDatabase.FindAssets($"t:{_entryType.Name}", new[] { searchPath });
            foreach (string guid in guids)
            {
                string path  = AssetDatabase.GUIDToAssetPath(guid);
                var    asset = AssetDatabase.LoadAssetAtPath(path, _entryType) as ScriptableObject;
                if (asset != null) result.Add(asset);
            }
            return result;
        }

        /// <summary>
        /// Read the enumKey int value from a Property SO via SerializedObject.
        /// </summary>
        private static int GetEnumIntFromSO(ScriptableObject so)
        {
            if (so == null) return 0;
            var sp = new SerializedObject(so).FindProperty("enumKey");
            return sp != null ? sp.intValue : 0;
        }

        private static bool IsValidIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            if (!char.IsLetter(s[0]) && s[0] != '_') return false;
            for (int i = 1; i < s.Length; i++)
                if (!char.IsLetterOrDigit(s[i]) && s[i] != '_') return false;
            return true;
        }

        /// <summary>
        /// Find the closing brace index for an opening brace in source text.
        /// </summary>
        private static int FindMatchingBrace(string source, int openIdx)
        {
            int depth = 0;
            for (int i = openIdx; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Convert an absolute filesystem path to a project-relative Assets/... path.
        /// </summary>
        private static string AbsoluteToAssetPath(string absolute)
        {
            string full    = Path.GetFullPath(absolute).Replace('\\', '/');
            string dataPath = Application.dataPath.Replace('\\', '/');
            // dataPath ends with /Assets — strip that and replace with relative
            string projectRoot = dataPath.Substring(0, dataPath.Length - "Assets".Length);
            if (full.StartsWith(projectRoot))
                return full.Substring(projectRoot.Length);
            return absolute; // fallback
        }

        private static void DrawSeparator()
        {
            EditorGUILayout.Space(4);
            Rect r = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 1f));
            EditorGUILayout.Space(4);
        }
    }
}
