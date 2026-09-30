using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AbyssToolKitUnity.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace JackyUIEssential.Editor
{
    public enum UIStyleAssemblyScanStatus
    {
        Ready = 0,
        Missing = 1,
        Duplicate = 2,
        DimensionMismatch = 3,
    }

    public sealed class UIStyleAssemblyScanItem
    {
        public UIStyleAssemblyRequirement Requirement { get; }
        public UIStyleAssemblyScanStatus Status { get; }
        public string AssetPath { get; }
        public Texture2D Texture { get; }
        public string Message { get; }

        public UIStyleAssemblyScanItem(
            UIStyleAssemblyRequirement requirement,
            UIStyleAssemblyScanStatus status,
            string assetPath,
            Texture2D texture,
            string message)
        {
            Requirement = requirement;
            Status = status;
            AssetPath = assetPath;
            Texture = texture;
            Message = message;
        }
    }

    /// <summary>
    /// Read-only result of a source-folder scan. It is valid for building only
    /// when every fixed visual slot resolves to one correctly-sized PNG.
    /// </summary>
    public sealed class UIStyleAssemblyScanResult
    {
        private readonly Dictionary<UIStyleVisualSlot, UIStyleAssemblyScanItem> _items =
            new Dictionary<UIStyleVisualSlot, UIStyleAssemblyScanItem>();

        internal UIStyleAssemblyScanResult(string sourceFolder, string styleName)
        {
            SourceFolder = sourceFolder;
            StyleName = styleName;
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public string SourceFolder { get; }
        public string StyleName { get; }
        public List<string> Errors { get; }
        public List<string> Warnings { get; }
        public IReadOnlyDictionary<UIStyleVisualSlot, UIStyleAssemblyScanItem> Items => _items;
        public bool CanBuild => Errors.Count == 0;
        public int ResolvedCount { get; internal set; }
        public int RequiredCount { get; internal set; }

        internal void SetItem(UIStyleAssemblyScanItem item)
        {
            _items[item.Requirement.Slot] = item;
        }

        internal bool TryGetAssetPath(UIStyleVisualSlot slot, out string assetPath)
        {
            assetPath = null;
            if (!_items.TryGetValue(slot, out UIStyleAssemblyScanItem item)
                || item.Status != UIStyleAssemblyScanStatus.Ready)
            {
                return false;
            }

            assetPath = item.AssetPath;
            return !string.IsNullOrWhiteSpace(assetPath);
        }
    }

    public sealed class UIStyleAssemblyBuildResult
    {
        internal UIStyleAssemblyBuildResult(UIStyleAssemblyScanResult scanResult)
        {
            ScanResult = scanResult;
        }

        public UIStyleAssemblyScanResult ScanResult { get; }
        public bool Succeeded { get; internal set; }
        public string Message { get; internal set; }
        public UIVisualStyleLibrary VisualStyleLibrary { get; internal set; }
        public CustomUIComponentLibrary ComponentLibrary { get; internal set; }
    }

    /// <summary>
    /// Deterministic editor-only assembly service for a complete UI style
    /// folder. It never overwrites an existing generated style.
    /// </summary>
    public static class UIStyleAssemblyBuilder
    {
        public const string DefaultSchemaAssetPath =
            "Assets/JackyUIEssential/Settings/UIStyleAssemblySchema.asset";

        public const string BaseComponentLibraryAssetPath =
            "Assets/JackyUIEssential/SOs/CustomUIComponentLibrary_GibiliArt.asset";

        public const string PrefabOutputRoot =
            "Assets/JackyUIEssential/Prefabs/UIPrefabs";

        public const string ScriptableObjectOutputRoot =
            "Assets/JackyUIEssential/SOs";

        private static readonly CustomUIComponentType[] _componentTypes =
        {
            CustomUIComponentType.Panel,
            CustomUIComponentType.Button,
            CustomUIComponentType.ScrollMenu,
            CustomUIComponentType.Slot,
            CustomUIComponentType.Tab,
            CustomUIComponentType.Toggle,
            CustomUIComponentType.ProgressBar,
            CustomUIComponentType.Slider,
        };

        public static UIStyleAssemblyScanResult Scan(
            UIStyleAssemblySchema schema,
            string sourceFolder)
        {
            string normalizedSourceFolder = NormalizeAssetPath(sourceFolder);
            string styleName = GetStyleName(normalizedSourceFolder);
            var result = new UIStyleAssemblyScanResult(normalizedSourceFolder, styleName);

            if (schema == null)
            {
                result.Errors.Add("Assign a UI Style Assembly Schema before scanning.");
                return result;
            }

            if (!schema.TryValidate(out List<string> schemaErrors))
            {
                result.Errors.AddRange(schemaErrors);
                return result;
            }

            result.RequiredCount = schema.Requirements.Count;

            if (!IsAssetsFolder(normalizedSourceFolder))
            {
                result.Errors.Add("Source Folder must be a folder inside Assets/.");
                return result;
            }

            if (string.IsNullOrWhiteSpace(styleName)
                || !Regex.IsMatch(styleName, "^[A-Za-z][A-Za-z0-9_]*$"))
            {
                result.Errors.Add(
                    "The source folder name is the Style Name and must start with a letter and contain only letters, numbers, or underscores.");
                return result;
            }

            List<string> sourcePngPaths = GetDirectPngPaths(normalizedSourceFolder);
            var matchedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int requirementIndex = 0; requirementIndex < schema.Requirements.Count; requirementIndex++)
            {
                UIStyleAssemblyRequirement requirement = schema.Requirements[requirementIndex];
                var candidates = new List<string>();

                for (int assetIndex = 0; assetIndex < sourcePngPaths.Count; assetIndex++)
                {
                    string assetPath = sourcePngPaths[assetIndex];
                    string fileStem = Path.GetFileNameWithoutExtension(assetPath);
                    if (MatchesAssetId(fileStem, requirement.AssetId))
                    {
                        candidates.Add(assetPath);
                    }
                }

                if (candidates.Count == 0)
                {
                    result.SetItem(new UIStyleAssemblyScanItem(
                        requirement,
                        UIStyleAssemblyScanStatus.Missing,
                        null,
                        null,
                        $"Missing required Asset ID '{requirement.AssetId}'."));
                    result.Errors.Add($"Missing '{requirement.AssetId}'.");
                    continue;
                }

                if (candidates.Count > 1)
                {
                    result.SetItem(new UIStyleAssemblyScanItem(
                        requirement,
                        UIStyleAssemblyScanStatus.Duplicate,
                        null,
                        null,
                        $"Found {candidates.Count} PNG files for '{requirement.AssetId}'. Keep exactly one finalized version in this folder."));
                    result.Errors.Add($"Duplicate finalized assets for '{requirement.AssetId}'.");
                    continue;
                }

                string candidatePath = candidates[0];
                matchedPaths.Add(candidatePath);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(candidatePath);

                if (texture == null)
                {
                    result.SetItem(new UIStyleAssemblyScanItem(
                        requirement,
                        UIStyleAssemblyScanStatus.DimensionMismatch,
                        candidatePath,
                        null,
                        $"Could not load '{candidatePath}' as a Texture2D."));
                    result.Errors.Add($"Could not load '{candidatePath}'.");
                    continue;
                }

                if (texture.width != requirement.ExpectedSize.x
                    || texture.height != requirement.ExpectedSize.y)
                {
                    string dimensionMessage =
                        $"Expected {requirement.ExpectedSize.x} x {requirement.ExpectedSize.y}, found {texture.width} x {texture.height}.";
                    result.SetItem(new UIStyleAssemblyScanItem(
                        requirement,
                        UIStyleAssemblyScanStatus.DimensionMismatch,
                        candidatePath,
                        texture,
                        dimensionMessage));
                    result.Errors.Add($"{requirement.AssetId}: {dimensionMessage}");
                    continue;
                }

                result.SetItem(new UIStyleAssemblyScanItem(
                    requirement,
                    UIStyleAssemblyScanStatus.Ready,
                    candidatePath,
                    texture,
                    "Ready."));
                result.ResolvedCount++;
            }

            for (int pathIndex = 0; pathIndex < sourcePngPaths.Count; pathIndex++)
            {
                string assetPath = sourcePngPaths[pathIndex];
                if (!matchedPaths.Contains(assetPath))
                {
                    result.Errors.Add(
                        $"'{Path.GetFileName(assetPath)}' does not map to one of the 21 required Asset IDs. " +
                        "A style source folder must contain only the 21 finalized UI PNG assets.");
                }
            }

            ValidateBaseComponentLibrary(result);
            ValidateOutputTargets(result);
            return result;
        }

        public static UIStyleAssemblyBuildResult Build(
            UIStyleAssemblySchema schema,
            string sourceFolder)
        {
            UIStyleAssemblyScanResult scanResult = Scan(schema, sourceFolder);
            var buildResult = new UIStyleAssemblyBuildResult(scanResult);

            if (!scanResult.CanBuild)
            {
                buildResult.Message = "Build was not started because preflight validation failed.";
                return buildResult;
            }

            var createdAssetPaths = new List<string>();
            string createdPrefabFolder = null;

            try
            {
                Dictionary<UIStyleVisualSlot, Sprite> sprites = ImportAndLoadSprites(scanResult, schema);

                string prefabFolder = GetPrefabOutputFolder(scanResult.StyleName);
                if (!CreateAssetFolder(prefabFolder))
                {
                    throw new InvalidOperationException($"Could not create Prefab output folder '{prefabFolder}'.");
                }

                createdPrefabFolder = prefabFolder;

                UIVisualStyleLibrary visualStyleLibrary = CreateVisualStyleLibrary(
                    scanResult.StyleName,
                    schema,
                    sprites,
                    createdAssetPaths);

                Dictionary<CustomUIComponentType, GameObject> generatedPrefabs =
                    CopyAndApplyPrefabs(
                        scanResult.StyleName,
                        prefabFolder,
                        sprites,
                        createdAssetPaths);

                CustomUIComponentLibrary componentLibrary = CreateComponentLibrary(
                    scanResult.StyleName,
                    generatedPrefabs,
                    createdAssetPaths);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                buildResult.Succeeded = true;
                buildResult.Message =
                    $"Created '{visualStyleLibrary.name}', '{componentLibrary.name}', and {generatedPrefabs.Count} styled Prefabs.";
                buildResult.VisualStyleLibrary = visualStyleLibrary;
                buildResult.ComponentLibrary = componentLibrary;
                return buildResult;
            }
            catch (Exception exception)
            {
                RollbackCreatedAssets(createdAssetPaths, createdPrefabFolder);
                Debug.LogException(exception);

                buildResult.Message =
                    $"Build failed. Newly-created output assets were rolled back. {exception.Message}";
                return buildResult;
            }
        }

        private static Dictionary<UIStyleVisualSlot, Sprite> ImportAndLoadSprites(
            UIStyleAssemblyScanResult scanResult,
            UIStyleAssemblySchema schema)
        {
            var sprites = new Dictionary<UIStyleVisualSlot, Sprite>();

            for (int index = 0; index < schema.Requirements.Count; index++)
            {
                UIStyleAssemblyRequirement requirement = schema.Requirements[index];
                if (!scanResult.TryGetAssetPath(requirement.Slot, out string assetPath))
                {
                    throw new InvalidOperationException($"No resolved asset path exists for '{requirement.AssetId}'.");
                }

                ApplyImportSettings(assetPath, requirement);

                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                {
                    throw new InvalidOperationException($"'{assetPath}' did not import as a Sprite.");
                }

                sprites.Add(requirement.Slot, sprite);
            }

            return sprites;
        }

        private static void ApplyImportSettings(
            string assetPath,
            UIStyleAssemblyRequirement requirement)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException($"'{assetPath}' has no TextureImporter.");
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = requirement.Presentation == UIStyleSpritePresentation.Sliced
                ? requirement.SliceBorder.ToUnitySpriteBorder()
                : Vector4.zero;
            importer.SaveAndReimport();
        }

        private static UIVisualStyleLibrary CreateVisualStyleLibrary(
            string styleName,
            UIStyleAssemblySchema schema,
            IReadOnlyDictionary<UIStyleVisualSlot, Sprite> sprites,
            List<string> createdAssetPaths)
        {
            string assetPath = GetVisualStyleLibraryPath(styleName);
            var library = ScriptableObject.CreateInstance<UIVisualStyleLibrary>();
            AssetDatabase.CreateAsset(library, assetPath);
            createdAssetPaths.Add(assetPath);

            var serializedLibrary = new SerializedObject(library);
            for (int index = 0; index < schema.Requirements.Count; index++)
            {
                UIStyleAssemblyRequirement requirement = schema.Requirements[index];
                string propertyName = GetVisualStyleLibraryPropertyName(requirement.Slot);
                SerializedProperty property = serializedLibrary.FindProperty(propertyName);

                if (property == null)
                {
                    throw new InvalidOperationException(
                        $"UIVisualStyleLibrary no longer contains the expected property '{propertyName}'.");
                }

                property.objectReferenceValue = sprites[requirement.Slot];
            }

            serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            return library;
        }

        private static Dictionary<CustomUIComponentType, GameObject> CopyAndApplyPrefabs(
            string styleName,
            string outputFolder,
            IReadOnlyDictionary<UIStyleVisualSlot, Sprite> sprites,
            List<string> createdAssetPaths)
        {
            CustomUIComponentLibrary baseLibrary =
                AssetDatabase.LoadAssetAtPath<CustomUIComponentLibrary>(BaseComponentLibraryAssetPath);

            if (baseLibrary == null)
            {
                throw new InvalidOperationException(
                    $"Could not load Base Component Library at '{BaseComponentLibraryAssetPath}'.");
            }

            var generatedPrefabs = new Dictionary<CustomUIComponentType, GameObject>();

            for (int index = 0; index < _componentTypes.Length; index++)
            {
                CustomUIComponentType type = _componentTypes[index];
                if (!baseLibrary.TryGetComponent(type, out CustomUIComponent baseComponent)
                    || baseComponent == null
                    || baseComponent.Prefab == null)
                {
                    throw new InvalidOperationException(
                        $"Base Component Library has no valid '{type}' Prefab assignment.");
                }

                string sourcePrefabPath = AssetDatabase.GetAssetPath(baseComponent.Prefab);
                string outputPrefabName = GetStyledPrefabName(baseComponent.Prefab.name, styleName);
                string outputPrefabPath = $"{outputFolder}/{outputPrefabName}.prefab";

                if (!AssetDatabase.CopyAsset(sourcePrefabPath, outputPrefabPath))
                {
                    throw new InvalidOperationException(
                        $"Could not copy '{sourcePrefabPath}' to '{outputPrefabPath}'.");
                }

                createdAssetPaths.Add(outputPrefabPath);
                GameObject prefabContents = PrefabUtility.LoadPrefabContents(outputPrefabPath);

                try
                {
                    prefabContents.name = outputPrefabName;
                    ApplySpritesToPrefab(prefabContents, type, sprites);
                    PrefabUtility.SaveAsPrefabAsset(prefabContents, outputPrefabPath);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(prefabContents);
                }

                GameObject generatedPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(outputPrefabPath);
                if (generatedPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"The generated '{type}' Prefab could not be loaded after saving.");
                }

                generatedPrefabs.Add(type, generatedPrefab);
            }

            return generatedPrefabs;
        }

        private static void ApplySpritesToPrefab(
            GameObject prefabRoot,
            CustomUIComponentType expectedType,
            IReadOnlyDictionary<UIStyleVisualSlot, Sprite> sprites)
        {
            UITracker[] trackers = prefabRoot.GetComponentsInChildren<UITracker>(true);
            if (!TryGetPrimaryTracker(trackers, expectedType, out _))
            {
                throw new InvalidOperationException(
                    $"'{prefabRoot.name}' must contain exactly one primary '{expectedType}' UITracker.");
            }

            for (int index = 0; index < trackers.Length; index++)
            {
                UITracker tracker = trackers[index];
                if (tracker.IsTracking)
                {
                    ApplySpritesToTracker(tracker, sprites);
                }
            }
        }

        private static void ApplySpritesToTracker(
            UITracker tracker,
            IReadOnlyDictionary<UIStyleVisualSlot, Sprite> sprites)
        {
            switch (tracker.Type)
            {
                case CustomUIComponentType.Button:
                    ApplySelectableSprites(
                        tracker,
                        sprites[UIStyleVisualSlot.ButtonNormal],
                        sprites[UIStyleVisualSlot.ButtonHighlighted],
                        sprites[UIStyleVisualSlot.ButtonPressed],
                        sprites[UIStyleVisualSlot.ButtonSelected],
                        sprites[UIStyleVisualSlot.ButtonDisabled]);
                    break;

                case CustomUIComponentType.Panel:
                    ApplySingleSprite(tracker, sprites[UIStyleVisualSlot.Panel]);
                    break;

                case CustomUIComponentType.Slot:
                    ApplySelectableSprites(
                        tracker,
                        sprites[UIStyleVisualSlot.SlotNormal],
                        sprites[UIStyleVisualSlot.SlotHighlighted],
                        sprites[UIStyleVisualSlot.SlotPressed],
                        sprites[UIStyleVisualSlot.SlotSelected],
                        sprites[UIStyleVisualSlot.SlotDisabled]);
                    break;

                case CustomUIComponentType.ScrollMenu:
                    ApplyScrollMenuSprites(
                        tracker,
                        sprites[UIStyleVisualSlot.ScrollMenuBackground],
                        sprites[UIStyleVisualSlot.ScrollMenuScrollbarTrack],
                        sprites[UIStyleVisualSlot.ScrollMenuScrollbarHandle]);
                    break;

                case CustomUIComponentType.Tab:
                    ApplyTabSprites(
                        tracker,
                        sprites[UIStyleVisualSlot.TabBackground],
                        sprites[UIStyleVisualSlot.TabSelectedOverlay]);
                    break;

                case CustomUIComponentType.Toggle:
                    ApplyToggleSprites(
                        tracker,
                        sprites[UIStyleVisualSlot.ToggleBackground],
                        sprites[UIStyleVisualSlot.ToggleSelectedIndicator]);
                    break;

                case CustomUIComponentType.ProgressBar:
                    ApplyProgressBarSprites(
                        tracker,
                        sprites[UIStyleVisualSlot.SlideBarBackground],
                        sprites[UIStyleVisualSlot.SlideBarFill]);
                    break;

                case CustomUIComponentType.Slider:
                    ApplySliderSprites(
                        tracker,
                        sprites[UIStyleVisualSlot.SlideBarBackground],
                        sprites[UIStyleVisualSlot.SlideBarFill],
                        sprites[UIStyleVisualSlot.SlideBarHandle]);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"'{tracker.Type}' is not supported by UI Style Assembly.");
            }
        }

        private static void ApplySelectableSprites(
            UITracker tracker,
            Sprite normal,
            Sprite highlighted,
            Sprite pressed,
            Sprite selected,
            Sprite disabled)
        {
            if (!tracker.TryGetTargetImage(out Image image)
                || !tracker.TryGetButton(out Button button)
                || !tracker.HasMatchingButtonTargetGraphic())
            {
                throw new InvalidOperationException(
                    $"The {tracker.Type} tracker on '{tracker.name}' is missing a matching Image/Button Sprite Swap setup.");
            }

            image.sprite = normal;
            SpriteState spriteState = button.spriteState;
            spriteState.highlightedSprite = highlighted;
            spriteState.pressedSprite = pressed;
            spriteState.selectedSprite = selected;
            spriteState.disabledSprite = disabled;
            button.spriteState = spriteState;

            EditorUtility.SetDirty(image);
            EditorUtility.SetDirty(button);
        }

        private static void ApplySingleSprite(UITracker tracker, Sprite sprite)
        {
            if (!tracker.TryGetTargetImage(out Image image))
            {
                throw new InvalidOperationException(
                    $"The {tracker.Type} tracker on '{tracker.name}' is missing its target Image.");
            }

            image.sprite = sprite;
            EditorUtility.SetDirty(image);
        }

        private static void ApplyScrollMenuSprites(
            UITracker tracker,
            Sprite panelSprite,
            Sprite trackSprite,
            Sprite handleSprite)
        {
            if (!tracker.TryGetScrollMenuImages(
                    out Image panelImage,
                    out Image trackImage,
                    out Image handleImage)
                || panelImage == null
                || trackImage == null
                || handleImage == null)
            {
                throw new InvalidOperationException(
                    $"The ScrollMenu tracker on '{tracker.name}' is missing one or more Image references.");
            }

            panelImage.sprite = panelSprite;
            trackImage.sprite = trackSprite;
            handleImage.sprite = handleSprite;
            EditorUtility.SetDirty(panelImage);
            EditorUtility.SetDirty(trackImage);
            EditorUtility.SetDirty(handleImage);
        }

        private static void ApplyTabSprites(
            UITracker tracker,
            Sprite backgroundSprite,
            Sprite selectedSprite)
        {
            if (!tracker.TryGetTabImages(out Image backgroundImage, out Image selectedImage)
                || backgroundImage == null
                || selectedImage == null)
            {
                throw new InvalidOperationException(
                    $"The Tab tracker on '{tracker.name}' is missing one or more Image references.");
            }

            backgroundImage.sprite = backgroundSprite;
            selectedImage.sprite = selectedSprite;
            EditorUtility.SetDirty(backgroundImage);
            EditorUtility.SetDirty(selectedImage);
        }

        private static void ApplyToggleSprites(
            UITracker tracker,
            Sprite backgroundSprite,
            Sprite checkmarkSprite)
        {
            if (!tracker.TryGetToggleImages(out Image backgroundImage, out Image checkmarkImage)
                || backgroundImage == null
                || checkmarkImage == null)
            {
                throw new InvalidOperationException(
                    $"The Toggle tracker on '{tracker.name}' is missing one or more Image references.");
            }

            backgroundImage.sprite = backgroundSprite;
            checkmarkImage.sprite = checkmarkSprite;
            EditorUtility.SetDirty(backgroundImage);
            EditorUtility.SetDirty(checkmarkImage);
        }

        private static void ApplyProgressBarSprites(
            UITracker tracker,
            Sprite backgroundSprite,
            Sprite fillSprite)
        {
            if (!tracker.TryGetProgressBarImages(out Image backgroundImage, out Image fillImage)
                || backgroundImage == null
                || fillImage == null)
            {
                throw new InvalidOperationException(
                    $"The ProgressBar tracker on '{tracker.name}' is missing one or more Image references.");
            }

            backgroundImage.sprite = backgroundSprite;
            fillImage.sprite = fillSprite;
            EditorUtility.SetDirty(backgroundImage);
            EditorUtility.SetDirty(fillImage);
        }

        private static void ApplySliderSprites(
            UITracker tracker,
            Sprite backgroundSprite,
            Sprite fillSprite,
            Sprite handleSprite)
        {
            if (!tracker.TryGetSliderImages(
                    out Image backgroundImage,
                    out Image fillImage,
                    out Image handleImage)
                || backgroundImage == null
                || fillImage == null
                || handleImage == null)
            {
                throw new InvalidOperationException(
                    $"The Slider tracker on '{tracker.name}' is missing one or more Image references.");
            }

            backgroundImage.sprite = backgroundSprite;
            fillImage.sprite = fillSprite;
            handleImage.sprite = handleSprite;
            EditorUtility.SetDirty(backgroundImage);
            EditorUtility.SetDirty(fillImage);
            EditorUtility.SetDirty(handleImage);
        }

        private static CustomUIComponentLibrary CreateComponentLibrary(
            string styleName,
            IReadOnlyDictionary<CustomUIComponentType, GameObject> generatedPrefabs,
            List<string> createdAssetPaths)
        {
            string assetPath = GetComponentLibraryPath(styleName);
            var library = ScriptableObject.CreateInstance<CustomUIComponentLibrary>();
            AssetDatabase.CreateAsset(library, assetPath);
            createdAssetPaths.Add(assetPath);

            var serializedLibrary = new SerializedObject(library);
            SerializedProperty componentsProperty = serializedLibrary.FindProperty("_components");

            if (componentsProperty == null)
            {
                throw new InvalidOperationException(
                    "CustomUIComponentLibrary no longer contains the expected '_components' property.");
            }

            componentsProperty.arraySize = _componentTypes.Length;
            for (int index = 0; index < _componentTypes.Length; index++)
            {
                CustomUIComponentType type = _componentTypes[index];
                SerializedProperty componentProperty =
                    componentsProperty.GetArrayElementAtIndex(index);

                componentProperty.FindPropertyRelative("_type").enumValueIndex = (int)type;
                componentProperty.FindPropertyRelative("_prefab").objectReferenceValue =
                    generatedPrefabs[type];
                componentProperty.FindPropertyRelative("_name").stringValue = string.Empty;
            }

            serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(library);
            return library;
        }

        private static void ValidateBaseComponentLibrary(UIStyleAssemblyScanResult result)
        {
            CustomUIComponentLibrary baseLibrary =
                AssetDatabase.LoadAssetAtPath<CustomUIComponentLibrary>(BaseComponentLibraryAssetPath);

            if (baseLibrary == null)
            {
                result.Errors.Add(
                    $"Base Component Library is missing at '{BaseComponentLibraryAssetPath}'.");
                return;
            }

            for (int index = 0; index < _componentTypes.Length; index++)
            {
                CustomUIComponentType type = _componentTypes[index];
                if (!baseLibrary.TryGetComponent(type, out CustomUIComponent component)
                    || component == null
                    || component.Prefab == null)
                {
                    result.Errors.Add(
                        $"Base Component Library must contain exactly one valid '{type}' Prefab.");
                    continue;
                }

                string prefabPath = AssetDatabase.GetAssetPath(component.Prefab);
                if (string.IsNullOrWhiteSpace(prefabPath))
                {
                    result.Errors.Add($"Base '{type}' Prefab is not a persistent asset.");
                    continue;
                }

                ValidateBasePrefabTracker(result, prefabPath, type);
            }
        }

        private static void ValidateBasePrefabTracker(
            UIStyleAssemblyScanResult result,
            string prefabPath,
            CustomUIComponentType expectedType)
        {
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                UITracker[] trackers = prefabContents.GetComponentsInChildren<UITracker>(true);
                if (!TryGetPrimaryTracker(trackers, expectedType, out _))
                {
                    result.Errors.Add(
                        $"Base Prefab '{Path.GetFileNameWithoutExtension(prefabPath)}' must contain exactly one primary '{expectedType}' UITracker.");
                    return;
                }

                for (int index = 0; index < trackers.Length; index++)
                {
                    UITracker tracker = trackers[index];
                    if (!tracker.IsTracking || IsTrackerFullyConfigured(tracker))
                    {
                        continue;
                    }

                    result.Errors.Add(
                        $"Base Prefab '{Path.GetFileNameWithoutExtension(prefabPath)}' has incomplete '{tracker.Type}' UITracker Image references.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private static bool TryGetPrimaryTracker(
            IReadOnlyList<UITracker> trackers,
            CustomUIComponentType expectedType,
            out UITracker primaryTracker)
        {
            primaryTracker = null;

            for (int index = 0; index < trackers.Count; index++)
            {
                UITracker tracker = trackers[index];
                if (tracker == null || tracker.Type != expectedType)
                {
                    continue;
                }

                if (primaryTracker != null)
                {
                    primaryTracker = null;
                    return false;
                }

                primaryTracker = tracker;
            }

            return primaryTracker != null;
        }

        private static bool IsTrackerFullyConfigured(UITracker tracker)
        {
            switch (tracker.Type)
            {
                case CustomUIComponentType.Button:
                case CustomUIComponentType.Slot:
                    return tracker.TryGetTargetImage(out Image selectableImage)
                           && selectableImage != null
                           && tracker.TryGetButton(out Button selectableButton)
                           && selectableButton != null
                           && tracker.HasMatchingButtonTargetGraphic();

                case CustomUIComponentType.Panel:
                    return tracker.TryGetTargetImage(out Image panelImage) && panelImage != null;

                case CustomUIComponentType.ScrollMenu:
                    return tracker.TryGetScrollMenuImages(
                               out Image scrollMenuPanel,
                               out Image scrollMenuTrack,
                               out Image scrollMenuHandle)
                           && scrollMenuPanel != null
                           && scrollMenuTrack != null
                           && scrollMenuHandle != null;

                case CustomUIComponentType.Tab:
                    return tracker.TryGetTabImages(out Image tabBackground, out Image tabSelected)
                           && tabBackground != null
                           && tabSelected != null;

                case CustomUIComponentType.Toggle:
                    return tracker.TryGetToggleImages(out Image toggleBackground, out Image toggleCheckmark)
                           && toggleBackground != null
                           && toggleCheckmark != null;

                case CustomUIComponentType.ProgressBar:
                    return tracker.TryGetProgressBarImages(out Image progressBackground, out Image progressFill)
                           && progressBackground != null
                           && progressFill != null;

                case CustomUIComponentType.Slider:
                    return tracker.TryGetSliderImages(
                               out Image sliderBackground,
                               out Image sliderFill,
                               out Image sliderHandle)
                           && sliderBackground != null
                           && sliderFill != null
                           && sliderHandle != null;

                default:
                    return false;
            }
        }

        private static void ValidateOutputTargets(UIStyleAssemblyScanResult result)
        {
            if (string.IsNullOrWhiteSpace(result.StyleName))
            {
                return;
            }

            string prefabFolder = GetPrefabOutputFolder(result.StyleName);
            if (AssetDatabase.IsValidFolder(prefabFolder))
            {
                result.Errors.Add(
                    $"Prefab output folder '{prefabFolder}' already exists. Existing styles are never overwritten.");
            }

            string visualStylePath = GetVisualStyleLibraryPath(result.StyleName);
            if (AssetDatabase.LoadMainAssetAtPath(visualStylePath) != null)
            {
                result.Errors.Add(
                    $"Visual Style Library '{visualStylePath}' already exists. Existing styles are never overwritten.");
            }

            string componentLibraryPath = GetComponentLibraryPath(result.StyleName);
            if (AssetDatabase.LoadMainAssetAtPath(componentLibraryPath) != null)
            {
                result.Errors.Add(
                    $"Component Library '{componentLibraryPath}' already exists. Existing styles are never overwritten.");
            }
        }

        private static List<string> GetDirectPngPaths(string sourceFolder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { sourceFolder });
            var paths = new List<string>();

            for (int index = 0; index < guids.Length; index++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                string directory = NormalizeAssetPath(Path.GetDirectoryName(assetPath));

                if (!string.Equals(directory, sourceFolder, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(Path.GetExtension(assetPath), ".png", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                paths.Add(assetPath);
            }

            return paths;
        }

        private static bool MatchesAssetId(string fileStem, string assetId)
        {
            if (string.IsNullOrWhiteSpace(fileStem) || string.IsNullOrWhiteSpace(assetId))
            {
                return false;
            }

            string pattern = $"(?:^|_){Regex.Escape(assetId)}(?:_|$)";
            return Regex.IsMatch(fileStem, pattern, RegexOptions.IgnoreCase);
        }

        private static bool IsAssetsFolder(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath)
                   && assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                   && AssetDatabase.IsValidFolder(assetPath);
        }

        private static string GetStyleName(string sourceFolder)
        {
            return string.IsNullOrWhiteSpace(sourceFolder)
                ? string.Empty
                : Path.GetFileName(sourceFolder.TrimEnd('/'));
        }

        private static string GetPrefabOutputFolder(string styleName)
        {
            return $"{PrefabOutputRoot}/{styleName}";
        }

        private static string GetVisualStyleLibraryPath(string styleName)
        {
            return $"{ScriptableObjectOutputRoot}/UIVisualStyleLibrary_{styleName}.asset";
        }

        private static string GetComponentLibraryPath(string styleName)
        {
            return $"{ScriptableObjectOutputRoot}/CustomUIComponentLibrary_{styleName}.asset";
        }

        private static string GetStyledPrefabName(string basePrefabName, string styleName)
        {
            const string baseStyleSuffix = "_GibiliArt";
            string withoutBaseStyle = basePrefabName.EndsWith(
                baseStyleSuffix,
                StringComparison.Ordinal)
                ? basePrefabName.Substring(0, basePrefabName.Length - baseStyleSuffix.Length)
                : basePrefabName;

            return $"{withoutBaseStyle}_{styleName}";
        }

        private static bool CreateAssetFolder(string assetFolderPath)
        {
            if (AssetDatabase.IsValidFolder(assetFolderPath))
            {
                return false;
            }

            string parentFolder = NormalizeAssetPath(Path.GetDirectoryName(assetFolderPath));
            string folderName = Path.GetFileName(assetFolderPath);

            return !string.IsNullOrWhiteSpace(parentFolder)
                   && !string.IsNullOrWhiteSpace(folderName)
                   && AssetDatabase.IsValidFolder(parentFolder)
                   && !string.IsNullOrWhiteSpace(AssetDatabase.CreateFolder(parentFolder, folderName));
        }

        private static string GetVisualStyleLibraryPropertyName(UIStyleVisualSlot slot)
        {
            switch (slot)
            {
                case UIStyleVisualSlot.ButtonNormal:
                    return "_buttonSprite";
                case UIStyleVisualSlot.ButtonHighlighted:
                    return "_buttonHighlightedSprite";
                case UIStyleVisualSlot.ButtonPressed:
                    return "_buttonPressedSprite";
                case UIStyleVisualSlot.ButtonSelected:
                    return "_buttonSelectedSprite";
                case UIStyleVisualSlot.ButtonDisabled:
                    return "_buttonDisabledSprite";
                case UIStyleVisualSlot.Panel:
                    return "_panelSprite";
                case UIStyleVisualSlot.SlotNormal:
                    return "_slotSprite";
                case UIStyleVisualSlot.SlotHighlighted:
                    return "_slotHighlightedSprite";
                case UIStyleVisualSlot.SlotPressed:
                    return "_slotPressedSprite";
                case UIStyleVisualSlot.SlotSelected:
                    return "_slotSelectedSprite";
                case UIStyleVisualSlot.SlotDisabled:
                    return "_slotDisabledSprite";
                case UIStyleVisualSlot.TabBackground:
                    return "_tabBackgroundSprite";
                case UIStyleVisualSlot.TabSelectedOverlay:
                    return "_tabSelectedSprite";
                case UIStyleVisualSlot.ToggleBackground:
                    return "_toggleBackgroundSprite";
                case UIStyleVisualSlot.ToggleSelectedIndicator:
                    return "_toggleCheckmarkSprite";
                case UIStyleVisualSlot.ScrollMenuBackground:
                    return "_scrollMenuSprite";
                case UIStyleVisualSlot.ScrollMenuScrollbarTrack:
                    return "_scrollMenuSlidingAreaSprite";
                case UIStyleVisualSlot.ScrollMenuScrollbarHandle:
                    return "_scrollMenuHandleSprite";
                case UIStyleVisualSlot.SlideBarBackground:
                    return "_slideBarBackgroundSprite";
                case UIStyleVisualSlot.SlideBarFill:
                    return "_slideBarFillSprite";
                case UIStyleVisualSlot.SlideBarHandle:
                    return "_slideBarHandleSprite";
                default:
                    throw new ArgumentOutOfRangeException(nameof(slot), slot, null);
            }
        }

        private static void RollbackCreatedAssets(
            List<string> createdAssetPaths,
            string createdPrefabFolder)
        {
            for (int index = createdAssetPaths.Count - 1; index >= 0; index--)
            {
                string assetPath = createdAssetPaths[index];
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }

            if (!string.IsNullOrWhiteSpace(createdPrefabFolder)
                && AssetDatabase.IsValidFolder(createdPrefabFolder))
            {
                AssetDatabase.DeleteAsset(createdPrefabFolder);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path)
                ? string.Empty
                : path.Replace("\\", "/").TrimEnd('/');
        }
    }
}
