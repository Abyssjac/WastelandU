using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class OperationPanelUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Action _onEnter;
    private Action _onExit;

    [SerializeField] private Button buildButton;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private Button deleteButton;

    //public void Init(Action onEnter, Action onExit, Action onBuild, Action onUpgrade,Action onDelete)
    //{
    //    _onEnter = onEnter;
    //    _onExit = onExit;

    //    BindButton(buildButton, onBuild);
    //    BindButton(upgradeButton, onUpgrade);
    //    BindButton(deleteButton, onDelete);
    //}
    public void Init(OperationPanelData opData) {
        _onEnter = opData.onEnter;
        _onExit = opData.onExit;
        BindButton(buildButton, opData.onBuild);
        BindButton(upgradeButton, opData.onUpgrade);
        BindButton(deleteButton, opData.onDelete);
    }


    public void OnPointerEnter(PointerEventData eventData)
    {
        _onEnter?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _onExit?.Invoke();
    }

    private void BindButton(Button tarButton, Action bindAction)
    {
        if (bindAction == null) { 
            tarButton.interactable = false;
        }

        if (tarButton != null)
        {
            tarButton.onClick.RemoveAllListeners();
            tarButton.onClick.AddListener(() => bindAction?.Invoke());
        }
        else { 
            Debug.LogWarning("Attempted to bind action to a null button reference.");
        }
    }
}

public struct OperationPanelData
{
    public Action onEnter;
    public Action onExit;
    public Action onBuild;
    public Action onUpgrade;
    public Action onDelete;
    public OperationPanelData(Action onEnter, Action onExit, Action onBuild, Action onUpgrade, Action onDelete)
    {
        this.onEnter = onEnter;
        this.onExit = onExit;
        this.onBuild = onBuild;
        this.onUpgrade = onUpgrade;
        this.onDelete = onDelete;
    }
}