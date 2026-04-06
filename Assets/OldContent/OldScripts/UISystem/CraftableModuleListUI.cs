using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using System;

public class CraftableModuleListUI : MonoBehaviour
{
    private static CraftableModuleListUI _instance;
    public static CraftableModuleListUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CraftableModuleListUI>();
                if (_instance == null)
                {
                    var go = new GameObject("CarriageAssemblerUI");
                    _instance = go.AddComponent<CraftableModuleListUI>();
                }
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        //DontDestroyOnLoad(gameObject);
    }

    [Header("UI Elements")]
    [SerializeField] private GameObject craftPanel;
    [SerializeField] private Transform craftableModuleListContainer;
    [SerializeField] private GameObject craftableModulePanelPrefab;

    [SerializeField] private Button backButton;

    private void Start()
    {
        Init();
    }

    private void Init()
    {
        IReadOnlyList<ModuleData> allModules = GameManager.Instance.ModuleDatabase.AllModules;

        // Clear existing UI elements
        foreach (Transform child in craftableModuleListContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var moduleData in allModules)
        {
            var modulePanel = Instantiate(craftableModulePanelPrefab, craftableModuleListContainer);
            CraftableModuleUI panelUI = modulePanel.GetComponent<CraftableModuleUI>();
            if (panelUI == null)
            {
                Debug.LogError("craftableModulePanelPrefab is missing CraftableModuleUI component.");
                continue;
            }
            panelUI.ApplyToUI(moduleData);
        }

        //Init Button Click
        backButton.onClick.RemoveAllListeners();
        backButton.onClick.AddListener(() => ToggleCraftPanel(false));  
    }

    public void ToggleCraftPanel(bool isOpen)
    { 
        craftPanel.SetActive(isOpen);
        CarriageAssemblerUI.Instance.CurModulePanel?.ToggleSelected(isOpen);
    }

}
