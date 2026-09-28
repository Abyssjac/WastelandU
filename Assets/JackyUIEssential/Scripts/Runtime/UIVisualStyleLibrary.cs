using AbyssToolKitUnity.Utility;
using UnityEngine;

namespace JackyUIEssential
{
    /// <summary>
    /// The complete Sprite set required by a Selectable using Sprite Swap.
    /// Normal is required; the other states may be null to fall back to Normal.
    /// </summary>
    public struct SelectableVisualSprites
    {
        public Sprite NormalSprite;
        public Sprite HighlightedSprite;
        public Sprite PressedSprite;
        public Sprite SelectedSprite;
        public Sprite DisabledSprite;

        public SelectableVisualSprites(
            Sprite normalSprite,
            Sprite highlightedSprite,
            Sprite pressedSprite,
            Sprite selectedSprite,
            Sprite disabledSprite)
        {
            NormalSprite = normalSprite;
            HighlightedSprite = highlightedSprite;
            PressedSprite = pressedSprite;
            SelectedSprite = selectedSprite;
            DisabledSprite = disabledSprite;
        }

        public bool HasAnySprite => NormalSprite != null;
    }

    /// <summary>
    /// The independent visual Sprite targets contained by one Scroll Menu.
    /// Each Sprite is optional so a style can be introduced incrementally.
    /// </summary>
    public struct ScrollMenuVisualSprites
    {
        public Sprite PanelSprite;
        public Sprite SlidingAreaSprite;
        public Sprite HandleSprite;

        public ScrollMenuVisualSprites(
            Sprite panelSprite,
            Sprite slidingAreaSprite,
            Sprite handleSprite)
        {
            PanelSprite = panelSprite;
            SlidingAreaSprite = slidingAreaSprite;
            HandleSprite = handleSprite;
        }

        public bool HasAnySprite => PanelSprite != null
                                    || SlidingAreaSprite != null
                                    || HandleSprite != null;
    }

    public struct TabVisualSprites
    {
        public Sprite BackgroundSprite;
        public Sprite SelectedSprite;

        public TabVisualSprites(Sprite backgroundSprite, Sprite selectedSprite)
        {
            BackgroundSprite = backgroundSprite;
            SelectedSprite = selectedSprite;
        }

        public bool HasAnySprite => BackgroundSprite != null || SelectedSprite != null;
    }

    public struct ToggleVisualSprites
    {
        public Sprite BackgroundSprite;
        public Sprite CheckmarkSprite;

        public ToggleVisualSprites(Sprite backgroundSprite, Sprite checkmarkSprite)
        {
            BackgroundSprite = backgroundSprite;
            CheckmarkSprite = checkmarkSprite;
        }

        public bool HasAnySprite => BackgroundSprite != null || CheckmarkSprite != null;
    }

    public struct ProgressBarVisualSprites
    {
        public Sprite BackgroundSprite;
        public Sprite FillSprite;

        public ProgressBarVisualSprites(Sprite backgroundSprite, Sprite fillSprite)
        {
            BackgroundSprite = backgroundSprite;
            FillSprite = fillSprite;
        }

        public bool HasAnySprite => BackgroundSprite != null || FillSprite != null;
    }

    public struct SliderVisualSprites
    {
        public Sprite BackgroundSprite;
        public Sprite FillSprite;
        public Sprite HandleSprite;

        public SliderVisualSprites(Sprite backgroundSprite, Sprite fillSprite, Sprite handleSprite)
        {
            BackgroundSprite = backgroundSprite;
            FillSprite = fillSprite;
            HandleSprite = handleSprite;
        }

        public bool HasAnySprite => BackgroundSprite != null || FillSprite != null || HandleSprite != null;
    }

    /// <summary>
    /// A project visual style used by the editor-only UI Track Manager.
    /// It contains visual Sprite choices only; it does not define UI layouts,
    /// Prefabs, events, or runtime behaviour.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UIVisualStyleLibrary",
        menuName = "Jacky UI Essential/UI Visual Style Library")]
    public sealed class UIVisualStyleLibrary : ScriptableObject
    {
        [Header("Button Visuals")]
        [InspectorName("Normal Sprite")]
        [SerializeField] private Sprite _buttonSprite;
        [SerializeField] private Sprite _buttonHighlightedSprite;
        [SerializeField] private Sprite _buttonPressedSprite;
        [SerializeField] private Sprite _buttonSelectedSprite;
        [SerializeField] private Sprite _buttonDisabledSprite;

        [Header("Slot Visuals")]
        [InspectorName("Normal Sprite")]
        [SerializeField] private Sprite _slotSprite;
        [SerializeField] private Sprite _slotHighlightedSprite;
        [SerializeField] private Sprite _slotPressedSprite;
        [SerializeField] private Sprite _slotSelectedSprite;
        [SerializeField] private Sprite _slotDisabledSprite;

        [Header("Panel Visuals")]
        [SerializeField] private Sprite _panelSprite;

        [Header("Scroll Menu Visuals")]
        [InspectorName("Panel Sprite")]
        [SerializeField] private Sprite _scrollMenuSprite;
        [SerializeField] private Sprite _scrollMenuSlidingAreaSprite;
        [SerializeField] private Sprite _scrollMenuHandleSprite;

        [Header("Tab Visuals")]
        [SerializeField] private Sprite _tabBackgroundSprite;
        [SerializeField] private Sprite _tabSelectedSprite;

        [Header("Toggle Visuals")]
        [SerializeField] private Sprite _toggleBackgroundSprite;
        [SerializeField] private Sprite _toggleCheckmarkSprite;

        [Header("Slide Bar Visuals (Shared by Progress Bar and Slider)")]
        [SerializeField] private Sprite _slideBarBackgroundSprite;
        [SerializeField] private Sprite _slideBarFillSprite;
        [SerializeField] private Sprite _slideBarHandleSprite;

        public bool TryGetButtonVisualSprites(out SelectableVisualSprites sprites)
        {
            sprites = new SelectableVisualSprites(
                _buttonSprite,
                _buttonHighlightedSprite,
                _buttonPressedSprite,
                _buttonSelectedSprite,
                _buttonDisabledSprite);

            return sprites.HasAnySprite;
        }

        public bool TryGetSlotVisualSprites(out SelectableVisualSprites sprites)
        {
            sprites = new SelectableVisualSprites(
                _slotSprite,
                _slotHighlightedSprite,
                _slotPressedSprite,
                _slotSelectedSprite,
                _slotDisabledSprite);

            return sprites.HasAnySprite;
        }

        public bool TryGetPanelSprite(out Sprite sprite)
        {
            sprite = _panelSprite;
            return sprite != null;
        }

        public bool TryGetScrollMenuSprite(out Sprite sprite)
        {
            sprite = _scrollMenuSprite;
            return sprite != null;
        }

        public bool TryGetScrollMenuVisualSprites(out ScrollMenuVisualSprites sprites)
        {
            sprites = new ScrollMenuVisualSprites(
                _scrollMenuSprite,
                _scrollMenuSlidingAreaSprite,
                _scrollMenuHandleSprite);

            return sprites.HasAnySprite;
        }

        public bool TryGetTabVisualSprites(out TabVisualSprites sprites)
        {
            sprites = new TabVisualSprites(_tabBackgroundSprite, _tabSelectedSprite);
            return sprites.HasAnySprite;
        }

        public bool TryGetToggleVisualSprites(out ToggleVisualSprites sprites)
        {
            sprites = new ToggleVisualSprites(_toggleBackgroundSprite, _toggleCheckmarkSprite);
            return sprites.HasAnySprite;
        }

        public bool TryGetProgressBarVisualSprites(out ProgressBarVisualSprites sprites)
        {
            sprites = new ProgressBarVisualSprites(
                _slideBarBackgroundSprite,
                _slideBarFillSprite);
            return sprites.HasAnySprite;
        }

        public bool TryGetSliderVisualSprites(out SliderVisualSprites sprites)
        {
            sprites = new SliderVisualSprites(
                _slideBarBackgroundSprite,
                _slideBarFillSprite,
                _slideBarHandleSprite);
            return sprites.HasAnySprite;
        }

        public bool TryGetSprite(CustomUIComponentType type, out Sprite sprite)
        {
            switch (type)
            {
                case CustomUIComponentType.Button:
                    sprite = _buttonSprite;
                    break;

                case CustomUIComponentType.Slot:
                    sprite = _slotSprite;
                    break;

                case CustomUIComponentType.Panel:
                    return TryGetPanelSprite(out sprite);

                case CustomUIComponentType.ScrollMenu:
                    return TryGetScrollMenuSprite(out sprite);

                case CustomUIComponentType.Tab:
                    sprite = _tabBackgroundSprite;
                    break;

                case CustomUIComponentType.Toggle:
                    sprite = _toggleBackgroundSprite;
                    break;

                case CustomUIComponentType.ProgressBar:
                    sprite = _slideBarBackgroundSprite;
                    break;

                case CustomUIComponentType.Slider:
                    sprite = _slideBarBackgroundSprite;
                    break;

                default:
                    sprite = null;
                    return false;
            }

            return sprite != null;
        }
    }
}
