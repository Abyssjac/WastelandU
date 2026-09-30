using System;
using System.Collections.Generic;
using UnityEngine;

namespace JackyUIEssential.Editor
{
    /// <summary>
    /// The fixed visual destinations supported by <see cref="UIVisualStyleLibrary"/>.
    /// Every generated UI style must provide exactly one Sprite for each slot.
    /// </summary>
    public enum UIStyleVisualSlot
    {
        ButtonNormal = 0,
        ButtonHighlighted = 1,
        ButtonPressed = 2,
        ButtonSelected = 3,
        ButtonDisabled = 4,
        Panel = 5,
        SlotNormal = 6,
        SlotHighlighted = 7,
        SlotPressed = 8,
        SlotSelected = 9,
        SlotDisabled = 10,
        TabBackground = 11,
        TabSelectedOverlay = 12,
        ToggleBackground = 13,
        ToggleSelectedIndicator = 14,
        ScrollMenuBackground = 15,
        ScrollMenuScrollbarTrack = 16,
        ScrollMenuScrollbarHandle = 17,
        SlideBarBackground = 18,
        SlideBarFill = 19,
        SlideBarHandle = 20,
    }

    public enum UIStyleSpritePresentation
    {
        Simple = 0,
        Sliced = 1,
    }

    /// <summary>
    /// Human-readable nine-slice values. Unity's Sprite border vector uses
    /// left, bottom, right, top order, so conversion is kept in one place.
    /// </summary>
    [Serializable]
    public struct UIStyleSliceBorder
    {
        [SerializeField, Min(0)] private int _left;
        [SerializeField, Min(0)] private int _right;
        [SerializeField, Min(0)] private int _top;
        [SerializeField, Min(0)] private int _bottom;

        public UIStyleSliceBorder(int left, int right, int top, int bottom)
        {
            _left = left;
            _right = right;
            _top = top;
            _bottom = bottom;
        }

        public Vector4 ToUnitySpriteBorder()
        {
            return new Vector4(_left, _bottom, _right, _top);
        }
    }

    /// <summary>
    /// One required finalized Sprite in a UI style source folder.
    /// </summary>
    [Serializable]
    public sealed class UIStyleAssemblyRequirement
    {
        [SerializeField] private UIStyleVisualSlot _slot;
        [SerializeField] private string _assetId;
        [SerializeField] private Vector2Int _expectedSize;
        [SerializeField] private UIStyleSpritePresentation _presentation;
        [SerializeField] private UIStyleSliceBorder _sliceBorder;

        public UIStyleVisualSlot Slot => _slot;
        public string AssetId => _assetId;
        public Vector2Int ExpectedSize => _expectedSize;
        public UIStyleSpritePresentation Presentation => _presentation;
        public UIStyleSliceBorder SliceBorder => _sliceBorder;

        public UIStyleAssemblyRequirement(
            UIStyleVisualSlot slot,
            string assetId,
            int width,
            int height,
            UIStyleSpritePresentation presentation,
            UIStyleSliceBorder sliceBorder)
        {
            _slot = slot;
            _assetId = assetId;
            _expectedSize = new Vector2Int(width, height);
            _presentation = presentation;
            _sliceBorder = sliceBorder;
        }
    }

    /// <summary>
    /// Editor configuration shared by every generated Jacky UI style. It is
    /// deliberately separate from per-style output libraries.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UIStyleAssemblySchema",
        menuName = "Jacky UI Essential/UI Style Assembly Schema")]
    public sealed class UIStyleAssemblySchema : ScriptableObject
    {
        [SerializeField] private List<UIStyleAssemblyRequirement> _requirements =
            new List<UIStyleAssemblyRequirement>();

        public IReadOnlyList<UIStyleAssemblyRequirement> Requirements => _requirements;

        [ContextMenu("Reset To Current UI Specification")]
        public void ResetToCurrentUISpecification()
        {
            _requirements = CreateDefaultRequirements();
        }

        public bool TryValidate(out List<string> errors)
        {
            errors = new List<string>();

            if (_requirements == null)
            {
                errors.Add("The requirements list is missing.");
                return false;
            }

            Array values = Enum.GetValues(typeof(UIStyleVisualSlot));
            if (_requirements.Count != values.Length)
            {
                errors.Add($"The schema requires exactly {values.Length} entries, but contains {_requirements.Count}.");
            }

            var seenSlots = new HashSet<UIStyleVisualSlot>();
            var seenAssetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < _requirements.Count; index++)
            {
                UIStyleAssemblyRequirement requirement = _requirements[index];
                if (requirement == null)
                {
                    errors.Add($"Requirement {index} is null.");
                    continue;
                }

                if (!seenSlots.Add(requirement.Slot))
                {
                    errors.Add($"The slot '{requirement.Slot}' appears more than once.");
                }

                if (string.IsNullOrWhiteSpace(requirement.AssetId))
                {
                    errors.Add($"The slot '{requirement.Slot}' has no Asset ID.");
                }
                else if (!seenAssetIds.Add(requirement.AssetId))
                {
                    errors.Add($"The Asset ID '{requirement.AssetId}' appears more than once.");
                }

                if (requirement.ExpectedSize.x <= 0 || requirement.ExpectedSize.y <= 0)
                {
                    errors.Add($"The slot '{requirement.Slot}' has an invalid expected size.");
                }
            }

            foreach (UIStyleVisualSlot slot in values)
            {
                if (!seenSlots.Contains(slot))
                {
                    errors.Add($"The required slot '{slot}' is missing from the schema.");
                }
            }

            return errors.Count == 0;
        }

        public static UIStyleAssemblySchema CreateDefaultInstance()
        {
            UIStyleAssemblySchema schema = CreateInstance<UIStyleAssemblySchema>();
            schema.ResetToCurrentUISpecification();
            return schema;
        }

        private static List<UIStyleAssemblyRequirement> CreateDefaultRequirements()
        {
            var requirements = new List<UIStyleAssemblyRequirement>(21);

            UIStyleSliceBorder buttonBorder = new UIStyleSliceBorder(48, 48, 24, 24);
            UIStyleSliceBorder panelBorder = new UIStyleSliceBorder(32, 32, 32, 32);
            UIStyleSliceBorder slotBorder = new UIStyleSliceBorder(32, 32, 32, 32);
            UIStyleSliceBorder tabBorder = new UIStyleSliceBorder(48, 48, 24, 24);
            UIStyleSliceBorder toggleBorder = new UIStyleSliceBorder(24, 24, 24, 24);
            UIStyleSliceBorder scrollbarBorder = new UIStyleSliceBorder(8, 8, 24, 24);
            UIStyleSliceBorder slideBarBorder = new UIStyleSliceBorder(18, 18, 6, 6);

            Add(requirements, UIStyleVisualSlot.ButtonNormal, "button_main_normal_background", 320, 160, UIStyleSpritePresentation.Sliced, buttonBorder);
            Add(requirements, UIStyleVisualSlot.ButtonHighlighted, "button_main_hovered_background", 320, 160, UIStyleSpritePresentation.Sliced, buttonBorder);
            Add(requirements, UIStyleVisualSlot.ButtonPressed, "button_main_pressed_background", 320, 160, UIStyleSpritePresentation.Sliced, buttonBorder);
            Add(requirements, UIStyleVisualSlot.ButtonSelected, "button_main_selected_background", 320, 160, UIStyleSpritePresentation.Sliced, buttonBorder);
            Add(requirements, UIStyleVisualSlot.ButtonDisabled, "button_main_disabled_background", 320, 160, UIStyleSpritePresentation.Sliced, buttonBorder);

            Add(requirements, UIStyleVisualSlot.Panel, "panel_background", 640, 400, UIStyleSpritePresentation.Sliced, panelBorder);

            Add(requirements, UIStyleVisualSlot.SlotNormal, "slot_background", 256, 256, UIStyleSpritePresentation.Sliced, slotBorder);
            Add(requirements, UIStyleVisualSlot.SlotHighlighted, "slot_highlight_background", 256, 256, UIStyleSpritePresentation.Sliced, slotBorder);
            Add(requirements, UIStyleVisualSlot.SlotPressed, "slot_pressed_background", 256, 256, UIStyleSpritePresentation.Sliced, slotBorder);
            Add(requirements, UIStyleVisualSlot.SlotSelected, "slot_selected_background", 256, 256, UIStyleSpritePresentation.Sliced, slotBorder);
            Add(requirements, UIStyleVisualSlot.SlotDisabled, "slot_disabled_background", 256, 256, UIStyleSpritePresentation.Sliced, slotBorder);

            Add(requirements, UIStyleVisualSlot.TabBackground, "tab_background", 384, 96, UIStyleSpritePresentation.Sliced, tabBorder);
            Add(requirements, UIStyleVisualSlot.TabSelectedOverlay, "tab_selected_overlay", 384, 96, UIStyleSpritePresentation.Sliced, tabBorder);

            Add(requirements, UIStyleVisualSlot.ToggleBackground, "toggle_background", 128, 128, UIStyleSpritePresentation.Sliced, toggleBorder);
            Add(requirements, UIStyleVisualSlot.ToggleSelectedIndicator, "toggle_selected_indicator", 96, 96, UIStyleSpritePresentation.Simple, default);

            Add(requirements, UIStyleVisualSlot.ScrollMenuBackground, "scrollmenu_background", 640, 400, UIStyleSpritePresentation.Sliced, panelBorder);
            Add(requirements, UIStyleVisualSlot.ScrollMenuScrollbarTrack, "scrollmenu_scrollbar_track", 32, 192, UIStyleSpritePresentation.Sliced, scrollbarBorder);
            Add(requirements, UIStyleVisualSlot.ScrollMenuScrollbarHandle, "scrollmenu_scrollbar_handle", 32, 96, UIStyleSpritePresentation.Sliced, scrollbarBorder);

            Add(requirements, UIStyleVisualSlot.SlideBarBackground, "slidebar_background", 160, 20, UIStyleSpritePresentation.Sliced, slideBarBorder);
            Add(requirements, UIStyleVisualSlot.SlideBarFill, "slidebar_filling", 160, 20, UIStyleSpritePresentation.Sliced, slideBarBorder);
            Add(requirements, UIStyleVisualSlot.SlideBarHandle, "slidebar_handle", 96, 96, UIStyleSpritePresentation.Simple, default);

            return requirements;
        }

        private static void Add(
            List<UIStyleAssemblyRequirement> requirements,
            UIStyleVisualSlot slot,
            string assetId,
            int width,
            int height,
            UIStyleSpritePresentation presentation,
            UIStyleSliceBorder border)
        {
            requirements.Add(new UIStyleAssemblyRequirement(
                slot,
                assetId,
                width,
                height,
                presentation,
                border));
        }
    }
}
