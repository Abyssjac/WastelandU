using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One reusable option button in <see cref="NPCInteractionMenuUI"/>.
/// Create it as a UI prefab and assign its Button and label in the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class NPCInteractionOptionButton : MonoBehaviour
{
    [SerializeField] private Button _button;
    [SerializeField] private TextMeshProUGUI _label;

    private NPCInteractionType _interactionType;
    private Action<NPCInteractionType> _onSelected;

    private void Reset()
    {
        _button = GetComponent<Button>();
        _label = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void Awake()
    {
        if (_button == null)
            _button = GetComponent<Button>();

        if (_label == null)
            _label = GetComponentInChildren<TextMeshProUGUI>(true);

        if (_button != null)
            _button.onClick.AddListener(HandleClicked);
    }

    private void OnDestroy()
    {
        if (_button != null)
            _button.onClick.RemoveListener(HandleClicked);
    }

    public void Bind(
        NPCInteractionType interactionType,
        string label,
        Action<NPCInteractionType> onSelected)
    {
        _interactionType = interactionType;
        _onSelected = onSelected;

        if (_label != null)
            _label.text = label;

        if (_button != null)
            _button.interactable = true;
    }

    private void HandleClicked()
    {
        _onSelected?.Invoke(_interactionType);
    }
}
