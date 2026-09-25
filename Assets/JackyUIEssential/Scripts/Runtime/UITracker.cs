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
        [SerializeField] private Image _panelImage;

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

                default:
                    image = null;
                    return false;
            }

            return image != null;
        }
    }
}
