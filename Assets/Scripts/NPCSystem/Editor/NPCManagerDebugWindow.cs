using JackyUtility;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Live debug window for <see cref="NPCManager"/>.
/// Open via  Wasteland Debug ? NPC Manager.
/// </summary>
public class NPCManagerDebugWindow : DebugEditorWindow<NPCManager>
{
    private Key_NPC _selectedEnumKey   = Key_NPC.None;
    private string  _stringKeyInput    = "";
    private Vector3 _spawnPosition     = Vector3.zero;

    [MenuItem("Wasteland Debug/NPC Manager")]
    public static void ShowWindow() =>
        GetWindow<NPCManagerDebugWindow>("NPC Manager Debug").Show();

    protected override void DrawContent()
    {
        NPCManager mgr = Target;

        // ©¤©¤ Spawn / Despawn ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        DrawSpawnSection(mgr);

        // ©¤©¤ Overview ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        Header("Overview");
        Row("Total Spawned NPCs", mgr.SpawnedNPCs.Count.ToString());

        // ©¤©¤ Spawned NPC list ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        Header("Currently Spawned");
        if (mgr.SpawnedNPCs.Count == 0)
        {
            EditorGUILayout.HelpBox("No NPCs are currently spawned.", MessageType.None);
        }
        else
        {
            foreach (var kvp in mgr.SpawnedNPCs)
            {
                string goName = kvp.Value != null ? kvp.Value.name : "<destroyed>";
                Row(kvp.Key.ToString(), goName);
            }
        }

        // ©¤©¤ Property query ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
        Header("Query NPCProperty");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Enum Key", GUILayout.Width(90));
        _selectedEnumKey = (Key_NPC)EditorGUILayout.EnumPopup(_selectedEnumKey);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("String Key", GUILayout.Width(90));
        _stringKeyInput = EditorGUILayout.TextField(_stringKeyInput);
        EditorGUILayout.EndHorizontal();

        DrawSeparator();

        // Resolve the property ¡ª string key takes precedence if filled in.
        NPCProperty prop = null;
        var dbMgr = PropertyDatabaseManager.Instance;
        if (dbMgr != null)
        {
            var db = dbMgr.GetDatabase<NPCDatabase>();
            if (db != null)
            {
                if (!string.IsNullOrWhiteSpace(_stringKeyInput))
                    prop = db.GetByString(_stringKeyInput);
                else if (_selectedEnumKey != Key_NPC.None)
                    prop = db.GetByEnum(_selectedEnumKey);
            }
        }

        if (prop == null)
        {
            EditorGUILayout.HelpBox(
                "Select an Enum Key or enter a String Key above to inspect an NPCProperty.",
                MessageType.None);
        }
        else
        {
            DrawNPCProperty(prop, mgr);
        }
    }

    // ©¤©¤ Drawing helpers ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void DrawSpawnSection(NPCManager mgr)
    {
        Header("Spawn / Despawn NPC");

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Enum Key", GUILayout.Width(90));
        _selectedEnumKey = (Key_NPC)EditorGUILayout.EnumPopup(_selectedEnumKey);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Position", GUILayout.Width(90));
        _spawnPosition = EditorGUILayout.Vector3Field("", _spawnPosition);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("Position (0, 0, 0) uses the Manager's default spawn point.", MessageType.None);

        DrawSeparator();

        bool keyValid  = _selectedEnumKey != Key_NPC.None;
        bool isSpawned = keyValid && mgr.IsSpawned(_selectedEnumKey);

        ColoredRow("Status",
            !keyValid ? "¡ª" : isSpawned ? "Spawned" : "Not Spawned",
            !keyValid ? Color.gray : isSpawned ? Color.green : Color.gray);

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();

        GUI.enabled = keyValid && !isSpawned;
        if (GUILayout.Button("Spawn", GUILayout.Height(22)))
        {
            if (_spawnPosition == Vector3.zero)
                mgr.SpawnNPC(_selectedEnumKey);
            else
                mgr.SpawnNPC(_selectedEnumKey, _spawnPosition);
        }

        GUI.enabled = keyValid && isSpawned;
        if (GUILayout.Button("Despawn", GUILayout.Height(22)))
            mgr.DespawnNPC(_selectedEnumKey);

        GUI.enabled = true;
        EditorGUILayout.EndHorizontal();
    }

    private void DrawNPCProperty(NPCProperty prop, NPCManager mgr)
    {
        Header($"NPCProperty  ¡ª  {prop.EnumKey}");

        Row("Enum Key",      prop.EnumKey.ToString());
        Row("String Key",    prop.StringKey);
        Row("Display Name",  string.IsNullOrEmpty(prop.displayName) ? "<none>" : prop.displayName);
        Row("Prefab",        prop.prefab  != null ? prop.prefab.name  : "<none>");
        Row("Portrait",      prop.portrait != null ? prop.portrait.name : "<none>");
        Row("Max Env Affinity", prop.maxEnvAffinity.ToString("F1"));

        if (prop.tagWeights != null && prop.tagWeights.Length > 0)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Tag Affinity Weights", EditorStyles.miniBoldLabel);
            for (int i = 0; i < prop.tagWeights.Length; i++)
                Row($"  {prop.tagWeights[i].tag}", prop.tagWeights[i].weight.ToString("F2"));
        }

        DrawSeparator();

        bool isSpawned = mgr.IsSpawned(prop.EnumKey);
        ColoredRow("Currently Spawned",
            isSpawned ? "Yes" : "No",
            isSpawned ? Color.green : Color.gray);

        if (isSpawned)
        {
            GameObject go = mgr.GetSpawnedNPC(prop.EnumKey);
            Row("GameObject", go != null ? go.name : "<destroyed>");
            if (go != null)
                Row("Position", go.transform.position.ToString());

            // ©¤©¤ Runtime affinity ©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤©¤
            NPCBehaviour behaviour = go != null ? go.GetComponent<NPCBehaviour>() : null;
            if (behaviour != null && behaviour.RuntimeData != null)
            {
                Header("Runtime Affinity");
                NPCRuntimeData rd = behaviour.RuntimeData;
                Row("Living Environment", rd.LivingEnvironmentAffinity.ToString("F2"));
                Row("Daily Interaction",  rd.DailyInteractionAffinity.ToString("F2"));
                Row("Familiarity",        rd.FamiliarityAffinity.ToString("F2"));
                DrawSeparator();
                ColoredRow("Total Affinity", rd.TotalAffinity.ToString("F2"), Color.cyan);

                EditorGUILayout.Space(4);
                if (GUILayout.Button("Force Recalculate Affinity", GUILayout.Height(22)))
                    behaviour.ForceRecalculateAffinity();
            }
            else
            {
                EditorGUILayout.HelpBox("NPCBehaviour not found on this GameObject.", MessageType.Warning);
            }
        }
    }
}
