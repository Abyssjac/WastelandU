#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Places the authored NPC interaction menu below the existing dialogue Canvas.
/// The automatic attempt only touches S_BaseBuilding when that scene is already
/// loaded, so it never replaces a user's currently open scene.
/// </summary>
[InitializeOnLoad]
public static class NPCInteractionMenuSceneInstaller
{
    private const string ScenePath = "Assets/Scenes/S_BaseBuilding.unity";
    private const string PrefabPath = "Assets/A_Systems/NPCSystem/AllUIs/NPCInteractionPanel/Root_NPCInteractionMenu.prefab";

    static NPCInteractionMenuSceneInstaller()
    {
        EditorApplication.delayCall += TryCreateInLoadedBaseBuildingScene;
    }

    [MenuItem("Wasteland/NPC/Create Interaction Menu In Loaded Base Building Scene")]
    private static void CreateFromMenu()
    {
        TryCreateInLoadedBaseBuildingScene();
    }

    private static void TryCreateInLoadedBaseBuildingScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (HasMenuInScene(scene))
            return;

        GameObject dialogueSystem = GameObject.Find("MyDialogueSystem");
        if (dialogueSystem == null || dialogueSystem.scene != scene)
        {
            Debug.LogWarning($"[{nameof(NPCInteractionMenuSceneInstaller)}] Could not find MyDialogueSystem in {scene.name}.");
            return;
        }

        Canvas canvas = dialogueSystem.GetComponentInChildren<Canvas>(true);
        if (canvas == null)
        {
            Debug.LogWarning($"[{nameof(NPCInteractionMenuSceneInstaller)}] Could not find a Canvas below MyDialogueSystem.");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[{nameof(NPCInteractionMenuSceneInstaller)}] Missing prefab at '{PrefabPath}'.");
            return;
        }

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, canvas.transform) as GameObject;
        if (instance == null)
            return;

        instance.name = "Root_NPCInteractionMenu";
        Undo.RegisterCreatedObjectUndo(instance, "Create NPC Interaction Menu");
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[{nameof(NPCInteractionMenuSceneInstaller)}] Created NPC Interaction Menu below MyDialogueSystem/Canvas. Save the scene to persist it.", instance);
    }

    private static bool HasMenuInScene(Scene scene)
    {
        NPCInteractionMenuUI[] menus = Resources.FindObjectsOfTypeAll<NPCInteractionMenuUI>();

        for (int i = 0; i < menus.Length; i++)
        {
            NPCInteractionMenuUI menu = menus[i];
            if (menu != null && menu.gameObject.scene == scene)
                return true;
        }

        return false;
    }
}
#endif
