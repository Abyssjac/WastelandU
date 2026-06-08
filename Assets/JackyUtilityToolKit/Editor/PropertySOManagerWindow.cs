using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace JackyUtility
{
    /// <summary>
    /// Editor window for managing Property ScriptableObjects in the EnumStringKeyed system.
    /// Open via  Wasteland Tools > Property SO Manager.
    ///
    /// Features:
    ///   Current Entries – always-visible foldout showing Database entries
    ///   Open            – select an existing SO by enum key and open it in the Inspector
    ///   Create          – pick an enum key, fill details, create SO and auto-add to Database
    ///   Manage          – add / remove SO entries from a Database by enum key or drag-in
    ///   Validate        – scan a folder and report duplicate enum keys
    /// </summary>
    public class PropertySOManagerWindow : EditorWindow
    {
        // ── Shared ───────────────────────────────────────────────────────────
        private ScriptableObject _databaseSO;
        private Type             _entryType;   // TEntry  e.g. NPCProperty
        private Type             _enumType;    // TEnum   e.g. Key_NPC

        // ── Foldout states ───────────────────────────────────────────────────
        private bool _foldEntries  = true;
        private bool _foldOpen     = true;
        private bool _foldCreate   = false;
        private bool _foldManage   = false;
        private bool _foldValidate = false;

        // ── Entries foldout (top) ─────────────────────────────────────────────
        private Vector2 _entriesScroll;
        private const float EntryRowHeight  = 18f;
        private const int   EntryVisibleRows = 5;

        // ── Open tab ─────────────────────────────────────────────────────────
        private string   _openScanFolder   = "Assets";
        private int      _openEnumIndex    = 0;
        private string[] _openEnumNames;
        private int[]    _openEnumInts;
        private bool[]   _openHasSO;
        private string[] _openDisplayLabels;

        // ── Create tab ───────────────────────────────────────────────────────
        private string   _createScanFolder    = "Assets";
        private int      _createEnumIndex     = 0;
        private string[] _createEnumNames;
        private int[]    _createEnumInts;
        private bool[]   _createHasSO;
        private string[] _createDisplayLabels;
        private string   _createSOName        = "";
        private string   _createSOFolder      = "Assets";
        private string   _createStringKey     = "";

        // ── Validate tab ─────────────────────────────────────────────────────
        private string       _validateFolder  = "Assets";
        private List<string> _validateResults = new List<string>();
        private bool         _validateDone    = false;
        private Vector2      _validateScroll;

        // ── Manage tab ───────────────────────────────────────────────────────
        private string           _manageScanFolder    = "Assets";
        private int              _manageEnumIndex     = 0;
        private string[]         _manageEnumNames;
        private int[]            _manageEnumInts;
        private bool[]           _manageInDatabase;
        private string[]         _manageDisplayLabels;
        private ScriptableObject _manageDragSO;

        // ── Window scroll ────────────────────────────────────────────────────
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

            // ── Foldout sections ─────────────────────────────────────────────
            DrawEntriesSection();
            DrawSeparator();
            DrawOpenSection();
            DrawCreateSection();
            DrawManageSection();
            DrawValidateSection();

            EditorGUILayout.Space(6);
            EditorGUILayout.EndScrollView();
        }

        // ════════════════════════════════════════════════════════════════════
        // Current Database Entries (top foldout)
        // ════════════════════════════════════════════════════════════════════
        private void DrawEntriesSection()
        {
            _foldEntries = EditorGUILayout.BeginFoldoutHeaderGroup(_foldEntries, "Current Database Entries");
            if (_foldEntries)
            {
                EditorGUI.indentLevel++;

                var serialized  = new SerializedObject(_databaseSO);
                var entriesProp = serialized.FindProperty("entries");

                if (entriesProp == null || entriesProp.arraySize == 0)
                {
                    EditorGUILayout.LabelField("(empty)", EditorStyles.miniLabel);
                }
                else
                {
                    float scrollHeight = EntryRowHeight * EntryVisibleRows + 4f;
                    _entriesScroll = EditorGUILayout.BeginScrollView(
                        _entriesScroll, GUILayout.Height(scrollHeight));

                    for (int i = 0; i < entriesProp.arraySize; i++)
                    {
                        var asset = entriesProp.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (asset == null) continue;
                        EditorGUILayout.LabelField(asset.name, EditorStyles.miniLabel,
                            GUILayout.Height(EntryRowHeight));
                    }

                    EditorGUILayout.EndScrollView();

                    EditorGUILayout.LabelField(
                        $"Total: {entriesProp.arraySize} entries",
                        EditorStyles.miniLabel);
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
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

                    bool hasSO = _openHasSO != null
                              && _openEnumIndex < _openHasSO.Length
                              && _openHasSO[_openEnumIndex];

                    if (!hasSO)
                        EditorGUILayout.HelpBox(
                            "No SO asset found for this enum key in the scanned folder.",
                            MessageType.Warning);

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

                // Scan folder for enum key list
                EditorGUILayout.BeginHorizontal();
                _createScanFolder = EditorGUILayout.TextField("Scan Folder", _createScanFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(55)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Select Scan Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(picked))
                        _createScanFolder = AbsoluteToAssetPath(picked);
                }
                EditorGUILayout.EndHorizontal();

                if (GUILayout.Button("Scan"))
                    RefreshCreateEnumList(_createScanFolder);

                // Enum key dropdown
                if (_createEnumNames != null && _createEnumNames.Length > 0)
                {
                    _createEnumIndex = Mathf.Clamp(_createEnumIndex, 0, _createDisplayLabels.Length - 1);
                    _createEnumIndex = EditorGUILayout.Popup("Enum Key", _createEnumIndex, _createDisplayLabels);

                    bool hasSO = _createHasSO != null
                              && _createEnumIndex < _createHasSO.Length
                              && _createHasSO[_createEnumIndex];

                    if (hasSO)
                        EditorGUILayout.HelpBox(
                            "An SO with this enum key already exists in the scan folder.",
                            MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.HelpBox("Press Scan to load enum keys.", MessageType.None);
                }

                DrawSeparator();

                // Asset details
                _createSOName    = EditorGUILayout.TextField("Asset File Name", _createSOName);
                _createStringKey = EditorGUILayout.TextField("String Key",      _createStringKey);

                EditorGUILayout.BeginHorizontal();
                _createSOFolder = EditorGUILayout.TextField("Output Folder", _createSOFolder);
                if (GUILayout.Button("Browse", GUILayout.Width(55)))
                {
                    string picked = EditorUtility.OpenFolderPanel("Select Output Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(picked))
                        _createSOFolder = AbsoluteToAssetPath(picked);
                }
                EditorGUILayout.EndHorizontal();

                bool createValid = _createEnumNames != null
                                && _createEnumNames.Length > 0
                                && !string.IsNullOrWhiteSpace(_createSOName)
                                && !string.IsNullOrWhiteSpace(_createSOFolder);

                if (!createValid)
                    EditorGUILayout.HelpBox(
                        "Scan for enum keys, then fill Asset File Name and Output Folder.",
                        MessageType.Warning);

                using (new EditorGUI.DisabledScope(!createValid))
                {
                    if (GUILayout.Button("Create SO Asset", GUILayout.Height(26)))
                        CreateSOAsset();
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

                // ── Enum key drop-down ───────────────────────────────────────
                EditorGUILayout.LabelField("Select by Enum Key", EditorStyles.boldLabel);

                if (_manageEnumNames != null && _manageEnumNames.Length > 0)
                {
                    _manageEnumIndex = Mathf.Clamp(_manageEnumIndex, 0, _manageDisplayLabels.Length - 1);
                    _manageEnumIndex = EditorGUILayout.Popup("Enum Key", _manageEnumIndex, _manageDisplayLabels);

                    bool inDB  = _manageInDatabase != null
                              && _manageEnumIndex < _manageInDatabase.Length
                              && _manageInDatabase[_manageEnumIndex];
                    bool hasSO = IsManagedSOPresent(_manageEnumIndex);

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
                            "No SO found for this key in the scanned folder.", MessageType.Warning);
                    if (inDB)
                        EditorGUILayout.HelpBox(
                            "This key is already in the Database entries.", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("Press Scan to load enum keys.", MessageType.None);
                }

                DrawSeparator();

                // ── Drag-in directly ─────────────────────────────────────────
                EditorGUILayout.LabelField("Or Drag In SO Directly", EditorStyles.boldLabel);

                _manageDragSO = (ScriptableObject)EditorGUILayout.ObjectField(
                    "Property SO", _manageDragSO, typeof(ScriptableObject), false);

                if (_manageDragSO != null)
                {
                    bool dragInDB  = IsSoInDatabase(_manageDragSO);
                    bool validType = _entryType != null && _entryType.IsInstanceOfType(_manageDragSO);

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

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        // ════════════════════════════════════════════════════════════════════
        // Validate  (last section)
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
                        EditorGUILayout.HelpBox("No duplicate enum keys found. All clear!", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            $"{_validateResults.Count} duplicate group(s) found:", MessageType.Error);

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
        // Logic — Database reflection
        // ════════════════════════════════════════════════════════════════════
        private void OnDatabaseChanged()
        {
            _entryType        = null;
            _enumType         = null;
            _openEnumNames    = null;
            _createEnumNames  = null;
            _manageEnumNames  = null;
            _validateDone     = false;

            if (_databaseSO == null) return;

            Type dbType = _databaseSO.GetType();
            while (dbType != null && dbType != typeof(object))
            {
                if (dbType.IsGenericType)
                {
                    Type def = dbType.GetGenericTypeDefinition();
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

            // Seed scan folders from Database's scanFolder field
            var soSer    = new SerializedObject(_databaseSO);
            var scanProp = soSer.FindProperty("scanFolder");
            if (scanProp != null && !string.IsNullOrWhiteSpace(scanProp.stringValue))
            {
                string folder      = "Assets/" + scanProp.stringValue.TrimStart('/');
                _openScanFolder    = folder;
                _createScanFolder  = folder;
                _createSOFolder    = folder;
                _validateFolder    = folder;
                _manageScanFolder  = folder;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Logic — Open
        // ════════════════════════════════════════════════════════════════════
        private void RefreshOpenEnumList(string folder)
        {
            if (_enumType == null) return;
            BuildEnumLists(folder,
                out _openEnumNames, out _openEnumInts,
                out _openHasSO, out _openDisplayLabels);
            _openEnumIndex = 0;
        }

        private void OpenSO(string folder, int enumIntValue)
        {
            foreach (var asset in LoadAllEntryAssets(folder))
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
        private void RefreshCreateEnumList(string folder)
        {
            if (_enumType == null) return;
            BuildEnumLists(folder,
                out _createEnumNames, out _createEnumInts,
                out _createHasSO, out _createDisplayLabels);
            _createEnumIndex = 0;
        }

        private void CreateSOAsset()
        {
            if (_entryType == null)
            {
                EditorUtility.DisplayDialog("Error",
                    "Entry type is not resolved. Re-drag the Database SO.", "OK");
                return;
            }

            if (_createEnumNames == null || _createEnumIndex >= _createEnumNames.Length)
            {
                EditorUtility.DisplayDialog("Error", "No enum key selected. Press Scan first.", "OK");
                return;
            }

            string enumMemberName = _createEnumNames[_createEnumIndex];
            int    enumIntValue   = _createEnumInts[_createEnumIndex];

            // Duplicate SO warning
            if (_createHasSO != null && _createHasSO[_createEnumIndex])
            {
                bool proceed = EditorUtility.DisplayDialog(
                    "Duplicate Warning",
                    $"An SO with key '{enumMemberName}' already exists in '{_createScanFolder}'.\n" +
                    "Create another one anyway?",
                    "Create", "Cancel");
                if (!proceed) return;
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

            var serialized    = new SerializedObject(instance);
            var enumKeyProp   = serialized.FindProperty("enumKey");
            var stringKeyProp = serialized.FindProperty("stringKey");

            if (enumKeyProp != null)
            {
                enumKeyProp.intValue = enumIntValue;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            if (stringKeyProp != null && !string.IsNullOrEmpty(_createStringKey))
            {
                stringKeyProp.stringValue = _createStringKey;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(instance);
            AssetDatabase.SaveAssets();

            // Auto-add to Database
            ManageAddSO(instance);

            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(instance);
            Selection.activeObject = instance;

            Debug.Log($"[PropertySOManager] Created {_entryType.Name} at {assetPath}");
            EditorUtility.DisplayDialog("Done",
                $"Created and added to Database:\n  {assetPath}", "OK");
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
            BuildEnumLists(folder,
                out _manageEnumNames, out _manageEnumInts,
                out _, out _);

            var dbEntryInts = GetDatabaseEntryInts();
            _manageInDatabase    = new bool[_manageEnumInts.Length];
            _manageDisplayLabels = new string[_manageEnumInts.Length];

            var assets = LoadAllEntryAssets(folder);
            var coveredInts = new HashSet<int>();
            foreach (var a in assets) coveredInts.Add(GetEnumIntFromSO(a));

            for (int i = 0; i < _manageEnumInts.Length; i++)
            {
                int iv = _manageEnumInts[i];
                _manageInDatabase[i] = dbEntryInts.Contains(iv);
                bool hasSO   = coveredInts.Contains(iv);
                string dbTag = _manageInDatabase[i] ? " [DB]" : "";
                string soTag = hasSO ? "●" : "○";
                _manageDisplayLabels[i] = $"{soTag} {_manageEnumNames[i]}  ({iv}){dbTag}";
            }

            _manageEnumIndex = 0;
        }

        private bool IsManagedSOPresent(int enumIdx)
        {
            if (_manageEnumInts == null || enumIdx >= _manageEnumInts.Length) return false;
            int target = _manageEnumInts[enumIdx];
            foreach (var a in LoadAllEntryAssets(_manageScanFolder))
                if (GetEnumIntFromSO(a) == target) return true;
            return false;
        }

        private void ManageAddByEnum(int enumIntValue)
        {
            foreach (var asset in LoadAllEntryAssets(_manageScanFolder))
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
            foreach (var asset in LoadAllEntryAssets(_manageScanFolder))
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
            SortEntriesProperty(entriesProp);
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(_databaseSO);
            AssetDatabase.SaveAssets();

            if (_manageEnumNames != null) RefreshManageList(_manageScanFolder);
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
                    if (i < entriesProp.arraySize &&
                        entriesProp.GetArrayElementAtIndex(i).objectReferenceValue == null)
                        entriesProp.DeleteArrayElementAtIndex(i);

                    serialized.ApplyModifiedProperties();
                    EditorUtility.SetDirty(_databaseSO);
                    AssetDatabase.SaveAssets();

                    if (_manageEnumNames != null) RefreshManageList(_manageScanFolder);
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
                if (entriesProp.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableObject so)
                    result.Add(GetEnumIntFromSO(so));
            }
            return result;
        }

        private void SortEntriesProperty(SerializedProperty entriesProp)
        {
            int count = entriesProp.arraySize;
            if (count <= 1) return;

            var pairs = new List<(int idx, int val)>(count);
            for (int i = 0; i < count; i++)
            {
                var obj = entriesProp.GetArrayElementAtIndex(i).objectReferenceValue;
                int val = obj is ScriptableObject so ? GetEnumIntFromSO(so) : 0;
                pairs.Add((i, val));
            }
            pairs.Sort((a, b) => a.val.CompareTo(b.val));

            bool sorted = true;
            for (int i = 0; i < count; i++)
                if (pairs[i].idx != i) { sorted = false; break; }
            if (sorted) return;

            for (int i = 0; i < count - 1; i++)
            {
                if (pairs[i].idx == i) continue;
                int cur = -1;
                for (int j = i + 1; j < count; j++)
                    if (pairs[j].idx == i) { cur = j; break; }
                if (cur < 0) continue;
                entriesProp.MoveArrayElement(cur, i);
                var tmp = pairs[cur];
                pairs.RemoveAt(cur);
                pairs.Insert(i, tmp);
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // Shared helpers
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Build enum name/int/hasSO/label arrays for a given scan folder.
        /// Used by Open, Create, and Manage scan buttons.
        /// </summary>
        private void BuildEnumLists(string folder,
            out string[] outNames, out int[] outInts,
            out bool[] outHasSO, out string[] outLabels)
        {
            Array    enumValues = Enum.GetValues(_enumType);
            string[] enumNames  = Enum.GetNames(_enumType);
            var      assets     = LoadAllEntryAssets(folder);

            var coveredInts = new HashSet<int>();
            foreach (var a in assets) coveredInts.Add(GetEnumIntFromSO(a));

            outNames  = enumNames;
            outInts   = new int[enumValues.Length];
            outHasSO  = new bool[enumValues.Length];
            outLabels = new string[enumValues.Length];

            for (int i = 0; i < enumValues.Length; i++)
            {
                int iv         = Convert.ToInt32(enumValues.GetValue(i));
                outInts[i]   = iv;
                outHasSO[i]  = coveredInts.Contains(iv);
                outLabels[i] = outHasSO[i]
                    ? $"● {enumNames[i]}  ({iv})"
                    : $"○ {enumNames[i]}  ({iv})";
            }
        }

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

        private static int GetEnumIntFromSO(ScriptableObject so)
        {
            if (so == null) return 0;
            var sp = new SerializedObject(so).FindProperty("enumKey");
            return sp != null ? sp.intValue : 0;
        }

        private static string AbsoluteToAssetPath(string absolute)
        {
            string full        = Path.GetFullPath(absolute).Replace('\\', '/');
            string dataPath    = Application.dataPath.Replace('\\', '/');
            string projectRoot = dataPath.Substring(0, dataPath.Length - "Assets".Length);
            return full.StartsWith(projectRoot)
                ? full.Substring(projectRoot.Length)
                : absolute;
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
