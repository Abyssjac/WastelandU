using AbyssToolKitUnity.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace JackyUIEssential
{
    /// <summary>
    /// Marks the stable root of a UGUI component and identifies the Image whose
    /// sprite may be replaced by the editor-only UI visual style tools.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UITracker : MonoBehaviour
    {
        [SerializeField] private bool _isTracking = true;
        [SerializeField] private CustomUIComponentType _type = CustomUIComponentType.None;

        [SerializeField] private Image _buttonImage;
        [SerializeField] private Button _button;
        [SerializeField] private Image _panelImage;
        [SerializeField] private Image _slotImage;
        [SerializeField] private Button _slotButton;
        [SerializeField] private Image _scrollMenuPanelImage;

        public bool IsTracking => _isTracking;
        public CustomUIComponentType Type => _type;

        /// <summary>
        /// Gets the Image represented by this Tracker's currently selected
        /// visual type. Unsupported types deliberately return false.
        /// </summary>
        public bool TryGetTargetImage(out Image image)
        {
            switch (_type)
            {
                case CustomUIComponentType.Button:
                    image = _buttonImage;
                    break;

                case CustomUIComponentType.Panel:
                    image = _panelImage;
                    break;

                case CustomUIComponentType.Slot:
                    image = _slotImage;
                    break;

                case CustomUIComponentType.ScrollMenu:
                    image = _scrollMenuPanelImage;
                    break;

                default:
                    image = null;
                    return false;
            }

            return image != null;
        }

        /// <summary>
        /// Gets the Button whose Sprite Swap state belongs to this Tracker's
        /// Button or Slot visual. BetterButton is supported through this base
        /// type.
        /// </summary>
        public bool TryGetButton(out Button button)
        {
            switch (_type)
            {
                case CustomUIComponentType.Button:
                    button = _button;
                    break;

                case CustomUIComponentType.Slot:
                    button = _slotButton;
                    break;

                default:
                    button = null;
                    break;
            }

            return button != null;
        }

        /// <summary>
        /// Verifies that a Sprite Swap Button will drive the same Image used as
        /// this Tracker's normal Button or Slot visual.
        /// </summary>
        public bool HasMatchingButtonTargetGraphic()
        {
            switch (_type)
            {
                case CustomUIComponentType.Button:
                    return _button != null && _button.targetGraphic == _buttonImage;

                case CustomUIComponentType.Slot:
                    return _slotButton != null && _slotButton.targetGraphic == _slotImage;

                default:
                    return false;
            }
        }
    }
}
