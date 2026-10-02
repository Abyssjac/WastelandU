using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace JackyUIEssential.Editor
{
    /// <summary>
    /// Builds the two static visual-replacement test panels used by S_UITest.
    /// The panels intentionally contain no Store, NPC, save, or gameplay scripts.
    /// </summary>
    internal static class UIVisualTestPanelGenerator
    {
        private const string MenuPath = "AbyssTools/UIEssential/Build Visual Test Panels";
        private const string TestScenePath = "Assets/JackyUIEssential/S_UITest.unity";
        private const string OutputFolder = "Assets/JackyUIEssential/Prefabs/UITest";
        private const string GibiliArtFolder = "Assets/JackyUIEssential/Prefabs/UIPrefabs/GibiliArt";

        private const string PanelPrefabPath = GibiliArtFolder + "/Panel_Root_GibiliArt.prefab";
        private const string ButtonPrefabPath = GibiliArtFolder + "/Button_Root_GibiliArt.prefab";
        private const string SlotPrefabPath = GibiliArtFolder + "/Slot_Root_GibiliArt.prefab";
        private const string ScrollMenuPrefabPath = GibiliArtFolder + "/ScrollMenuGrid_Root_GibiliArt.prefab";
        private const string TabPrefabPath = GibiliArtFolder + "/Tab_Root_GibiliArt.prefab";
        private const string ProgressBarPrefabPath = GibiliArtFolder + "/ProgressBar_Root_GibiliArt.prefab";
        private const string SliderPrefabPath = GibiliArtFolder + "/Slider_Root_GibiliArt.prefab";

        private static readonly Vector2 PanelSize = new Vector2(1180f, 680f);

        [MenuItem(MenuPath)]
        private static void BuildFromMenu()
        {
            Build();
        }

        /// <summary>
        /// Also usable from Unity batch mode through -executeMethod.
        /// </summary>
        public static void Build()
        {
            ValidateSourcePrefabs();
            EnsureFolder(OutputFolder);

            Scene previousScene = SceneManager.GetActiveScene();
            Scene buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            try
            {
                SceneManager.SetActiveScene(buildScene);
                CreateStorePanel(OutputFolder + "/StoreVisualTestPanel.prefab");
                CreateNpcProfilePanel(OutputFolder + "/NPCProfileVisualTestPanel.prefab");
            }
            finally
            {
                EditorSceneManager.CloseScene(buildScene, true);
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
            }

            PlacePanelsInTestScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("UI visual test panels were rebuilt and placed in S_UITest.");
        }

        private static void CreateStorePanel(string outputPath)
        {
            GameObject root = InstantiateComponent(PanelPrefabPath, null, "StoreVisualTestPanel", PanelSize, Vector2.zero);

            AddText(root.transform, "Title", "Mira's Island Store", new Vector2(0f, 280f), new Vector2(580f, 56f), 35f, TextAlignmentOptions.Center);
            CreateButton(root.transform, "CloseButton", "Close", new Vector2(500f, 280f), new Vector2(120f, 46f));

            CreateTab(root.transform, "BuyTab", "Buy", new Vector2(-205f, 210f), true);
            CreateTab(root.transform, "SellTab", "Sell", new Vector2(145f, 210f), false);

            GameObject scrollMenu = InstantiateComponent(ScrollMenuPrefabPath, root.transform, "StoreOfferScrollMenu", new Vector2(440f, 440f), new Vector2(-305f, -35f));
            AddText(scrollMenu.transform, "HeaderLabel", "Available Goods", new Vector2(0f, 188f), new Vector2(320f, 35f), 22f, TextAlignmentOptions.Center);
            Transform content = FindDeepChild(scrollMenu.transform, "Content");
            if (content != null)
            {
                ClearChildren(content);
                string[] items = { "Cloudwood", "Old Rope", "Glowstone", "Sea Salt", "Iron Shard", "Map Fragment" };
                for (int index = 0; index < items.Length; index++)
                {
                    GameObject slot = InstantiateComponent(SlotPrefabPath, content, $"OfferSlot_{index + 1}", new Vector2(142f, 142f), Vector2.zero);
                    AddText(slot.transform, "ItemLabel", items[index], new Vector2(0f, 24f), new Vector2(125f, 46f), 16f, TextAlignmentOptions.Center);
                    AddText(slot.transform, "PriceLabel", $"{12 + (index * 7)} G", new Vector2(0f, -40f), new Vector2(115f, 28f), 15f, TextAlignmentOptions.Center);
                }
            }

            GameObject detailPanel = InstantiateComponent(PanelPrefabPath, root.transform, "ItemDetailPanel", new Vector2(510f, 440f), new Vector2(285f, -35f));
            AddText(detailPanel.transform, "ItemName", "Cloudwood", new Vector2(0f, 150f), new Vector2(420f, 44f), 29f, TextAlignmentOptions.Center);
            AddText(detailPanel.transform, "Description", "A warm, lightweight timber collected from the island's wind-swept forests.", new Vector2(0f, 65f), new Vector2(390f, 96f), 20f, TextAlignmentOptions.Center);
            AddText(detailPanel.transform, "Price", "Price   12 G", new Vector2(0f, -45f), new Vector2(340f, 36f), 22f, TextAlignmentOptions.Center);
            CreateButton(detailPanel.transform, "PurchaseButton", "Purchase", new Vector2(0f, -145f), new Vector2(230f, 55f));

            SavePrefab(root, outputPath);
        }

        private static void CreateNpcProfilePanel(string outputPath)
        {
            GameObject root = InstantiateComponent(PanelPrefabPath, null, "NPCProfileVisualTestPanel", PanelSize, Vector2.zero);

            AddText(root.transform, "Title", "Mira", new Vector2(0f, 280f), new Vector2(480f, 56f), 38f, TextAlignmentOptions.Center);
            CreateButton(root.transform, "CloseButton", "Close", new Vector2(500f, 280f), new Vector2(120f, 46f));

            GameObject portraitPanel = InstantiateComponent(PanelPrefabPath, root.transform, "PortraitPanel", new Vector2(285f, 410f), new Vector2(-380f, -30f));
            AddText(portraitPanel.transform, "PortraitPlaceholder", "MIRA\n\nMerchant of\nFairwind Dock", Vector2.zero, new Vector2(230f, 260f), 27f, TextAlignmentOptions.Center);

            CreateTab(root.transform, "ProfileTab", "Profile", new Vector2(-135f, 210f), true);
            CreateTab(root.transform, "AffinityTab", "Affinity", new Vector2(145f, 210f), false);
            CreateTab(root.transform, "RoomTab", "Room", new Vector2(425f, 210f), false);

            GameObject profilePanel = InstantiateComponent(PanelPrefabPath, root.transform, "ProfileContent", new Vector2(665f, 410f), new Vector2(220f, -30f));
            AddText(profilePanel.transform, "Role", "Island Merchant", new Vector2(0f, 155f), new Vector2(500f, 42f), 26f, TextAlignmentOptions.Center);
            AddText(profilePanel.transform, "AffinityLabel", "Affinity", new Vector2(-235f, 80f), new Vector2(145f, 34f), 20f, TextAlignmentOptions.Left);
            CreateProgressBar(profilePanel.transform, "AffinityProgress", new Vector2(60f, 80f), new Vector2(425f, 38f), 0.68f);

            CreateStatSlider(profilePanel.transform, "Trust", new Vector2(0f, 15f), 0.76f);
            CreateStatSlider(profilePanel.transform, "Trade", new Vector2(0f, -65f), 0.52f);
            CreateStatSlider(profilePanel.transform, "Discovery", new Vector2(0f, -145f), 0.34f);
            CreateButton(profilePanel.transform, "OpenProfileButton", "Open NPC Panel", new Vector2(0f, -230f), new Vector2(280f, 52f));

            SavePrefab(root, outputPath);
        }

        private static void PlacePanelsInTestScene()
        {
            Scene scene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            Canvas canvas = FindCanvas(scene);
            if (canvas == null)
                throw new InvalidOperationException("S_UITest does not contain a Canvas.");

            RemoveExistingInstance(canvas.transform, "StoreVisualTestPanel_TestInstance");
            RemoveExistingInstance(canvas.transform, "NPCProfileVisualTestPanel_TestInstance");

            CreateTestSceneInstance(canvas.transform, OutputFolder + "/StoreVisualTestPanel.prefab", "StoreVisualTestPanel_TestInstance");
            CreateTestSceneInstance(canvas.transform, OutputFolder + "/NPCProfileVisualTestPanel.prefab", "NPCProfileVisualTestPanel_TestInstance");
            EditorSceneManager.SaveScene(scene);
        }

        private static void CreateTestSceneInstance(Transform parent, string prefabPath, string instanceName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = instanceName;

            RectTransform rectTransform = instance.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = PanelSize;
            instance.SetActive(false);
        }

        private static void CreateTab(Transform parent, string name, string label, Vector2 position, bool selected)
        {
            GameObject tab = InstantiateComponent(TabPrefabPath, parent, name, new Vector2(250f, 56f), position);
            AddText(tab.transform, "LabelOverlay", label, Vector2.zero, new Vector2(205f, 40f), 21f, TextAlignmentOptions.Center);

            Transform selectedOverlay = FindDeepChild(tab.transform, "Selected");
            if (selectedOverlay != null)
                selectedOverlay.gameObject.SetActive(selected);
        }

        private static void CreateButton(Transform parent, string name, string label, Vector2 position, Vector2 size)
        {
            GameObject button = InstantiateComponent(ButtonPrefabPath, parent, name, size, position);
            AddText(button.transform, "Label", label, Vector2.zero, new Vector2(size.x - 18f, size.y - 8f), 20f, TextAlignmentOptions.Center);
        }

        private static void CreateProgressBar(Transform parent, string name, Vector2 position, Vector2 size, float value)
        {
            GameObject progressBar = InstantiateComponent(ProgressBarPrefabPath, parent, name, size, position);
            Slider slider = progressBar.GetComponentInChildren<Slider>(true);
            if (slider != null)
                slider.value = value;

            AddText(progressBar.transform, "ValueOverlay", $"{Mathf.RoundToInt(value * 100f)}%", Vector2.zero, new Vector2(100f, size.y), 15f, TextAlignmentOptions.Center);
        }

        private static void CreateStatSlider(Transform parent, string label, Vector2 position, float value)
        {
            AddText(parent, label + "Label", label, new Vector2(-220f, position.y), new Vector2(140f, 32f), 18f, TextAlignmentOptions.Left);
            GameObject slider = InstantiateComponent(SliderPrefabPath, parent, label + "Slider", new Vector2(385f, 30f), new Vector2(45f, position.y));
            Slider sliderComponent = slider.GetComponentInChildren<Slider>(true);
            if (sliderComponent != null)
                sliderComponent.value = value;
        }

        private static GameObject InstantiateComponent(string prefabPath, Transform parent, string name, Vector2 size, Vector2 position)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (source == null)
                throw new InvalidOperationException($"Missing required UI prefab: {prefabPath}");

            GameObject instance = parent == null
                ? (GameObject)PrefabUtility.InstantiatePrefab(source)
                : (GameObject)PrefabUtility.InstantiatePrefab(source, parent);

            instance.name = name;
            RectTransform rectTransform = instance.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;
            return instance;
        }

        private static void AddText(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.layer = parent.gameObject.layer;
            textObject.transform.SetParent(parent, false);

            RectTransform rectTransform = textObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = position;
            rectTransform.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
        }

        private static void SavePrefab(GameObject root, string outputPath)
        {
            PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void ValidateSourcePrefabs()
        {
            string[] sourcePaths =
            {
                PanelPrefabPath,
                ButtonPrefabPath,
                SlotPrefabPath,
                ScrollMenuPrefabPath,
                TabPrefabPath,
                ProgressBarPrefabPath,
                SliderPrefabPath,
            };

            foreach (string path in sourcePaths)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    throw new InvalidOperationException($"Required GibiliArt source prefab is missing: {path}");
            }
        }

        private static Canvas FindCanvas(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Canvas canvas = root.GetComponentInChildren<Canvas>(true);
                if (canvas != null)
                    return canvas;
            }

            return null;
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                    return child;
            }

            return null;
        }

        private static void RemoveExistingInstance(Transform parent, string instanceName)
        {
            Transform existing = parent.Find(instanceName);
            if (existing != null)
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
                UnityEngine.Object.DestroyImmediate(parent.GetChild(index).gameObject);
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
                return;

            string[] parts = folderPath.Split('/');
            string currentPath = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string nextPath = currentPath + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(nextPath))
                    AssetDatabase.CreateFolder(currentPath, parts[index]);

                currentPath = nextPath;
            }
        }
    }
}
