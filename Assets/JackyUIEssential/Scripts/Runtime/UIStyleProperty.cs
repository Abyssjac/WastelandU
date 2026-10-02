using AbyssToolKitUnity.Utility;
using JackyUtility;
using UnityEngine;

namespace JackyUIEssential
{
    /// <summary>
    /// Stable identifiers for complete authored UI styles. Append new style
    /// values rather than changing existing values, because Properties and
    /// future runtime lookups use these as persistent identifiers.
    /// </summary>
    public enum Key_UIStylePP
    {
        None = 0,
        GibiliArt_0 = 1,
        Cyberpunk_0 = 2,
        Cyberpunk_1 = 3,
        Lightsteampunk_0 = 4,
    }

    /// <summary>
    /// Pairs the Prefab creation library and visual Sprite library that belong
    /// to one authored UI style. The two libraries deliberately remain content
    /// assets; this Property is the single enum-keyed style identity.
    /// </summary>
    [CreateAssetMenu(fileName = "UIStylePP_", menuName = "AllProperties/UIStyleProperty")]
    public class UIStyleProperty : EnumStringKeyedProperty<Key_UIStylePP>
    {
        [Header("Style Libraries")]
        [SerializeField] private CustomUIComponentLibrary _componentLibrary;
        [SerializeField] private UIVisualStyleLibrary _visualStyleLibrary;

        public CustomUIComponentLibrary ComponentLibrary => _componentLibrary;
        public UIVisualStyleLibrary VisualStyleLibrary => _visualStyleLibrary;
        public bool IsComplete => _componentLibrary != null && _visualStyleLibrary != null;
    }
}
