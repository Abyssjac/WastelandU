using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace JackyUIEssential
{
    /// <summary>
    /// Controls whether a BetterButton keeps its EventSystem selection after it
    /// has been activated.
    /// </summary>
    public enum BetterButtonSelectionBehaviour
    {
        OneShot = 0,
        KeepSelect = 1,
    }

    /// <summary>
    /// A standard UGUI Button with an explicit post-activation selection policy.
    /// It remains fully assignable anywhere a <see cref="Button"/> is expected.
    /// </summary>
    [AddComponentMenu("UI (Canvas)/Better Button", 30)]
    public class BetterButton : Button
    {
        [UnityEngine.SerializeField]
        private BetterButtonSelectionBehaviour _selectionBehaviour = BetterButtonSelectionBehaviour.KeepSelect;

        public BetterButtonSelectionBehaviour SelectionBehaviour => _selectionBehaviour;

        public override void OnPointerClick(PointerEventData eventData)
        {
            bool activated = eventData.button == PointerEventData.InputButton.Left
                             && IsActive()
                             && IsInteractable();
            base.OnPointerClick(eventData);

            if (activated)
                ClearSelectionAfterActivationIfNeeded();
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            bool activated = IsActive() && IsInteractable();
            base.OnSubmit(eventData);

            if (activated)
                ClearSelectionAfterActivationIfNeeded();
        }

        private void ClearSelectionAfterActivationIfNeeded()
        {
            if (_selectionBehaviour != BetterButtonSelectionBehaviour.OneShot)
            {
                return;
            }

            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == gameObject)
                eventSystem.SetSelectedGameObject(null);
        }
    }
}
