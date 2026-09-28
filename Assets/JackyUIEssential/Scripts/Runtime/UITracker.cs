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
        [SerializeField] private Image _scrollMenuSlidingAreaImage;
        [SerializeField] private Image _scrollMenuHandleImage;
        [SerializeField] private Image _tabBackgroundImage;
        [SerializeField] private Image _tabSelectedImage;
        [SerializeField] private Image _toggleBackgroundImage;
        [SerializeField] private Image _toggleCheckmarkImage;
        [SerializeField] private Image _progressBarBackgroundImage;
        [SerializeField] private Image _progressBarFillImage;
        [SerializeField] private Image _sliderBackgroundImage;
        [SerializeField] private Image _sliderFillImage;
        [SerializeField] private Image _sliderHandleImage;

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

                case CustomUIComponentType.Tab:
                    image = _tabBackgroundImage;
                    break;

                case CustomUIComponentType.Toggle:
                    image = _toggleBackgroundImage;
                    break;

                case CustomUIComponentType.ProgressBar:
                    image = _progressBarBackgroundImage;
                    break;

                case CustomUIComponentType.Slider:
                    image = _sliderBackgroundImage;
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

        /// <summary>
        /// Gets the visual Images that belong to a Scroll Menu. The Panel,
        /// Sliding Area, and Handle remain one composite component owned by
        /// this single tracker.
        /// </summary>
        public bool TryGetScrollMenuImages(
            out Image panelImage,
            out Image slidingAreaImage,
            out Image handleImage)
        {
            if (_type != CustomUIComponentType.ScrollMenu)
            {
                panelImage = null;
                slidingAreaImage = null;
                handleImage = null;
                return false;
            }

            panelImage = _scrollMenuPanelImage;
            slidingAreaImage = _scrollMenuSlidingAreaImage;
            handleImage = _scrollMenuHandleImage;
            return panelImage != null || slidingAreaImage != null || handleImage != null;
        }

        public bool TryGetTabImages(out Image backgroundImage, out Image selectedImage)
        {
            if (_type != CustomUIComponentType.Tab)
            {
                backgroundImage = null;
                selectedImage = null;
                return false;
            }

            backgroundImage = _tabBackgroundImage;
            selectedImage = _tabSelectedImage;
            return backgroundImage != null || selectedImage != null;
        }

        public bool TryGetToggleImages(out Image backgroundImage, out Image checkmarkImage)
        {
            if (_type != CustomUIComponentType.Toggle)
            {
                backgroundImage = null;
                checkmarkImage = null;
                return false;
            }

            backgroundImage = _toggleBackgroundImage;
            checkmarkImage = _toggleCheckmarkImage;
            return backgroundImage != null || checkmarkImage != null;
        }

        public bool TryGetProgressBarImages(
            out Image backgroundImage,
            out Image fillImage)
        {
            if (_type != CustomUIComponentType.ProgressBar)
            {
                backgroundImage = null;
                fillImage = null;
                return false;
            }

            backgroundImage = _progressBarBackgroundImage;
            fillImage = _progressBarFillImage;
            return backgroundImage != null || fillImage != null;
        }

        public bool TryGetSliderImages(
            out Image backgroundImage,
            out Image fillImage,
            out Image handleImage)
        {
            if (_type != CustomUIComponentType.Slider)
            {
                backgroundImage = null;
                fillImage = null;
                handleImage = null;
                return false;
            }

            backgroundImage = _sliderBackgroundImage;
            fillImage = _sliderFillImage;
            handleImage = _sliderHandleImage;
            return backgroundImage != null || fillImage != null || handleImage != null;
        }
    }
}
