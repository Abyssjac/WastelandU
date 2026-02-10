using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class CarriageAssemblerUI : MonoBehaviour
{
    private static CarriageAssemblerUI _instance;
    public static CarriageAssemblerUI Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<CarriageAssemblerUI>();
                if (_instance == null)
                {
                    var go = new GameObject("CarriageAssemblerUI");
                    _instance = go.AddComponent<CarriageAssemblerUI>();
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
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private Transform moduleListContainer;
    [SerializeField] private GameObject moduleListItemPrefab;

    [Header("Runtime Logic & Authority Data")]
    private CarriageAssembler curCarriageAssembler;
    public CarriageAssembler CurCarriageAssembler => curCarriageAssembler;
    private ModulePanelUI curModulePanel;
    public ModulePanelUI CurModulePanel => curModulePanel;
    private ModuleRuntime curModuleRuntime;
    public ModuleRuntime CurModuleRuntime => curModuleRuntime;

    [Header("Panel <-> Index Mapping")]
    [SerializeField] private bool rebuildMappingOnRefresh = true;

    private readonly List<ModulePanelUI> _panelByIndex = new List<ModulePanelUI>();
    private readonly Dictionary<ModulePanelUI, int> _indexByPanel = new Dictionary<ModulePanelUI, int>();


    /// <summary>
    /// index -> panel (read-only view)
    /// </summary>
    public IReadOnlyList<ModulePanelUI> PanelByIndex => _panelByIndex;

    /// <summary>
    /// panel -> index (read-only view)
    /// </summary>
    public IReadOnlyDictionary<ModulePanelUI, int> IndexByPanel => _indexByPanel;

    public bool TryGetIndexByPanel(ModulePanelUI panel, out int index)
    {
        if (panel == null)
        {
            index = -1;
            return false;
        }
        return _indexByPanel.TryGetValue(panel, out index);
    }

    public bool TryGetPanelByIndex(int index, out ModulePanelUI panel)
    {
        panel = null;

        if (index < 0 || index >= _panelByIndex.Count)
            return false;

        panel = _panelByIndex[index];
        return panel != null;
    }

    private void RefreshCarraigeAssmeblerUI()
    {
        if (uiPanel == null || !uiPanel.activeSelf) return;
        if (curCarriageAssembler == null) return;

        ClearCarriageAssmeblerUI();

        if (rebuildMappingOnRefresh)
            ClearPanelIndexMapping();

        CarriageRuntime runtime = curCarriageAssembler.runtime;

        int moduleCount = runtime.modulesBySlot.Length;

        // 预分配容量，避免扩容
        if (rebuildMappingOnRefresh)
        {
            _panelByIndex.Capacity = Mathf.Max(_panelByIndex.Capacity, moduleCount);
        }

        for (int i = 0; i < moduleCount; i++)
        {
            var modulePanelGO = Instantiate(moduleListItemPrefab, moduleListContainer);
            ModulePanelUI panelUI = modulePanelGO.GetComponent<ModulePanelUI>();
            if (panelUI == null)
            {
                Debug.LogError("ModuleListItem prefab is missing ModulePanelUI component.");
                continue;
            }

            ModuleRuntime moduleRuntime = runtime.modulesBySlot[i];
            panelUI.SetModuleData(moduleRuntime, curCarriageAssembler);

            if (rebuildMappingOnRefresh)
                RegisterPanelIndex(panelUI, i);
        }
    }

    private void RegisterPanelIndex(ModulePanelUI panel, int index)
    {
        // 确保 list 大小足够容纳 index
        while (_panelByIndex.Count <= index)
            _panelByIndex.Add(null);

        _panelByIndex[index] = panel;
        _indexByPanel[panel] = index;
    }

    private void ClearPanelIndexMapping()
    {
        _panelByIndex.Clear();
        _indexByPanel.Clear();
    }

    private void ClearCarriageAssmeblerUI()
    {
        // selection runtime reset
        curModulePanel = null;
        curModuleRuntime = null;

        // mapping reset
        ClearPanelIndexMapping();

        // Clear existing UI elements
        foreach (Transform child in moduleListContainer)
        {
            Destroy(child.gameObject);
        }
    }

    public void OpenUIPanel(CarriageAssembler carriageAssembler)
    {
        uiPanel.SetActive(true);
        curCarriageAssembler = carriageAssembler;
        carriageAssembler.OnChanged += RefreshCarraigeAssmeblerUI;
        RefreshCarraigeAssmeblerUI();
    }

    public void CloseUIPanel()
    {
        CraftableModuleListUI.Instance.ToggleCraftPanel(false);

        curCarriageAssembler.OnChanged -= RefreshCarraigeAssmeblerUI;
        ClearCarriageAssmeblerUI();
        curCarriageAssembler = null;
        uiPanel.SetActive(false);
    }

    public void SetCurModulePanel(ModulePanelUI panel, ModuleRuntime moduleRuntime)
    {
        if (curModulePanel == panel)
            return;

        // Debug信息
        Debug.Log($"[ModuleUI] Current Panel Changed: {curModulePanel} -> {panel}");

        // 旧panel关闭
        if (curModulePanel != null)
        {
            curModulePanel.ToggleSelected(false);
        }

        curModulePanel = panel;

        // 新panel打开
        if (curModulePanel != null)
        {
            curModulePanel.ToggleSelected(true);
        }

        curModuleRuntime = moduleRuntime;
    }
}
