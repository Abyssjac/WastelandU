using AbyssToolKitUnity.Utility;
using UnityEngine;

namespace JackyUIEssential
{
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
        [SerializeField] private Sprite _buttonSprite;
        [SerializeField] private Sprite _panelSprite;

        public bool TryGetSprite(CustomUIComponentType type, out Sprite sprite)
        {
            switch (type)
            {
                case CustomUIComponentType.Button:
                    sprite = _buttonSprite;
                    break;

                case CustomUIComponentType.Panel:
                    sprite = _panelSprite;
                    break;

                default:
                    sprite = null;
                    return false;
            }

            return sprite != null;
        }
    }
}
