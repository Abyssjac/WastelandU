using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ItemDefinitionBuildableBatchGenerator
{
    private const string OutputFolder = "Assets/A_Systems/InventorySystem/AllItemDefinitions/Buildables";
    private const string CanonicalBuildableFolder = "Assets/A_Systems/BuildingSystem/AllBuildSOs";
    private const int DefaultStackCount = 99;

    [MenuItem("Tools/Item System/Generate Buildable Item Definitions")]
    public static void GenerateBuildableItemDefinitions()
    {
        EnsureFolder(OutputFolder);

        List<BuildableProperty> buildables = AssetDatabase
            .FindAssets("t:BuildableProperty", new[] { CanonicalBuildableFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildableProperty>)
            .Where(buildable => buildable != null && buildable.EnumKey != Key_BuildablePP.None)
            .OrderBy(buildable => (int)buildable.EnumKey)
            .ToList();

        Dictionary<Key_BuildablePP, List<BuildableProperty>> duplicateGroups = buildables
            .GroupBy(buildable => buildable.EnumKey)
            .Where(group => group.Count() > 1)
            .ToDictionary(group => group.Key, group => group.ToList());

        if (duplicateGroups.Count > 0)
        {
            foreach (KeyValuePair<Key_BuildablePP, List<BuildableProperty>> duplicate in duplicateGroups)
            {
                string paths = string.Join(", ", duplicate.Value.Select(AssetDatabase.GetAssetPath));
                Debug.LogError($"[ItemDefinition] Duplicate BuildableKey '{duplicate.Key}': {paths}");
            }

            return;
        }

        int createdCount = 0;
        int updatedCount = 0;

        foreach (BuildableProperty buildable in buildables)
        {
            string itemKeyName = $"Item_{buildable.EnumKey}";
            if (!Enum.TryParse(itemKeyName, out Key_ItemDefinitionPP itemKey))
            {
                Debug.LogError($"[ItemDefinition] Missing ItemDefinition key '{itemKeyName}' for '{AssetDatabase.GetAssetPath(buildable)}'.");
                continue;
            }

            string assetPath = $"{OutputFolder}/ItemDefinition_{itemKey}.asset";
            ItemDefinitionSO itemDefinition = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(assetPath);
            bool isNew = itemDefinition == null;

            if (isNew)
            {
                itemDefinition = ScriptableObject.CreateInstance<ItemDefinitionSO>();
                AssetDatabase.CreateAsset(itemDefinition, assetPath);
                createdCount++;
            }
            else
            {
                updatedCount++;
            }

            Configure(itemDefinition, itemKey, buildable, isNew);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ItemDefinition] Buildable item generation complete. Created: {createdCount}, updated: {updatedCount}, total: {buildables.Count}. Output: {OutputFolder}");
    }

    private static void Configure(
        ItemDefinitionSO itemDefinition,
        Key_ItemDefinitionPP itemKey,
        BuildableProperty buildable,
        bool isNew)
    {
        SerializedObject serializedItem = new SerializedObject(itemDefinition);
        serializedItem.FindProperty("enumKey").intValue = (int)itemKey;
        serializedItem.FindProperty("buildableKey").intValue = (int)buildable.EnumKey;
        serializedItem.FindProperty("icon").objectReferenceValue = buildable.iconSprite;

        if (isNew)
        {
            serializedItem.FindProperty("stringKey").stringValue = string.Empty;
            serializedItem.FindProperty("stackCount").intValue = DefaultStackCount;
        }

        serializedItem.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(itemDefinition);
    }

    private static void EnsureFolder(string folderPath)
    {
        string[] pathParts = folderPath.Split('/');
        string currentPath = pathParts[0];

        for (int index = 1; index < pathParts.Length; index++)
        {
            string nextPath = $"{currentPath}/{pathParts[index]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, pathParts[index]);
            }

            currentPath = nextPath;
        }
    }
}
