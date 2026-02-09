using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CraftableModuleUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image icon;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI cost;
    [SerializeField] private Button button;

    private ModuleData moduleData;
    //private Action<ModuleData> _onClicked;

    public ModuleData Data => moduleData;

    //public void Bind(ModuleData moduleData, Action<ModuleData> onClicked)
    //{
    //    _data = moduleData;
    //    _onClicked = onClicked;

    //    ApplyToUI(moduleData);

    //    if (button != null)
    //    {
    //        button.onClick.RemoveAllListeners();
    //        button.onClick.AddListener(HandleClick);
    //        button.interactable = moduleData != null;
    //    }
    //}

    private void Start()
    {
        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(HandleClick);
        }
    }

    //public void SetInteractable(bool interactable)
    //{
    //    if (button != null) button.interactable = interactable;
    //}

    private void HandleClick()
    {
        //if (_data == null) return;
        //_onClicked?.Invoke(_data);
        CarriageAssemblerUI.Instance.TryGetIndexByPanel(CarriageAssemblerUI.Instance.CurModulePanel, out int curIndex);
        CarriageAssemblerUI.Instance.CurCarriageAssembler.Install(curIndex, moduleData);
    }

    public void ApplyToUI(ModuleData moduleData)
    {
        if (moduleData == null)
        {
            if (icon != null) icon.sprite = null;
            if (nameText != null) nameText.text = "Empty";
            if (descriptionText != null) descriptionText.text = string.Empty;
            if(cost!= null) cost.text = string.Empty;
            return;
        }

        this.moduleData = moduleData;
        if (icon != null) icon.sprite = moduleData.moduleIcon;
        if (nameText != null) nameText.text = moduleData.displayName;
        if (descriptionText != null) descriptionText.text = moduleData.moduleDescription ?? string.Empty;
        if (cost != null) cost.text = moduleData.buildCost.ToString();
    }
}