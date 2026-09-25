using AbyssToolKitUnity.Utility;
using UnityEngine;

namespace JackyUIEssential
{
    /// <summary>
    /// The complete Sprite set required by a Button using Sprite Swap.
    /// Normal is required; the other states may be null to fall back to Normal.
    /// </summary>
    public struct ButtonVisualSprites
    {
        public Sprite NormalSprite;
        public Sprite HighlightedSprite;
        public Sprite PressedSprite;
        public Sprite SelectedSprite;
        public Sprite DisabledSprite;

        public ButtonVisualSprites(
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

        [Header("Panel Visuals")]
        [SerializeField] private Sprite _panelSprite;

        public bool TryGetButtonVisualSprites(out ButtonVisualSprites sprites)
        {
            sprites = new ButtonVisualSprites(
                _buttonSprite,
                _buttonHighlightedSprite,
                _buttonPressedSprite,
                _buttonSelectedSprite,
                _buttonDisabledSprite);

            return sprites.NormalSprite != null;
        }

        public bool TryGetPanelSprite(out Sprite sprite)
        {
            sprite = _panelSprite;
            return sprite != null;
        }

        public bool TryGetSprite(CustomUIComponentType type, out Sprite sprite)
        {
            switch (type)
            {
                case CustomUIComponentType.Button:
                    sprite = _buttonSprite;
                    break;

                case CustomUIComponentType.Panel:
                    return TryGetPanelSprite(out sprite);

                default:
                    sprite = null;
                    return false;
            }

            return sprite != null;
        }
    }
}
