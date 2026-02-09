using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System;
using Unity.VisualScripting;

public class ModulePanelUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Display")]
    [SerializeField] private Image moduleIcon;
    [SerializeField] private TextMeshProUGUI moduleNameText;
    [SerializeField] private TextMeshProUGUI moduleDescriptionText;

    [SerializeField] private Image selectedHighlightImage;

    [Header("Operation Panel")]
    [SerializeField] private GameObject operationPanelPrefab;
    [SerializeField] private float hoverDelaySeconds = 0.2f;
    [SerializeField] private float hoverMoveTolerancePixels = 2f;
    [SerializeField] private Vector2 operationPanelOffset = new Vector2(12f, 0f);

    private bool _isHoveringSelf;
    private bool _isHoveringOperationPanel;

    private Coroutine _hoverRoutine;

    private GameObject _operationPanelInstance;

    private ModuleRuntime curModuleRuntime;
    private CarriageAssembler curCarriageAssembler;

    public void SetModuleData(ModuleRuntime moduleRuntime, CarriageAssembler carriageAssembler) {
        curModuleRuntime = moduleRuntime;
        curCarriageAssembler = carriageAssembler;

        ModuleData moduleData = GameManager.Instance.GetModuleData(moduleRuntime?.moduleDataId);

        if (moduleData == null) {
            moduleIcon.sprite = null;
            moduleNameText.text = "Empty";
            moduleDescriptionText.text = "";
        } else {
            moduleIcon.sprite = moduleData.moduleIcon;
            moduleNameText.text = moduleData.displayName;
            moduleDescriptionText.text = moduleData.moduleDescription;
        }
    }

    private void OnDisable()
    {
        StopHoverRoutine();
        HideOperationPanelImmediate();
        _isHoveringSelf = false;
        _isHoveringOperationPanel = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHoveringSelf = true;

        if (_operationPanelInstance != null) return;

        StopHoverRoutine();
        _hoverRoutine = StartCoroutine(HoverDelayThenShow());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHoveringSelf = false;

        StopHoverRoutine();
        _hoverRoutine = StartCoroutine(DelayedCheckToHide());
    }

    private IEnumerator HoverDelayThenShow()
    {
        if (operationPanelPrefab == null)
            yield break;

        Vector2 startPos = Input.mousePosition;
        float elapsed = 0f;

        while (elapsed < hoverDelaySeconds)
        {
            if (!_isHoveringSelf) yield break;

            Vector2 nowPos = Input.mousePosition;
            if ((nowPos - startPos).sqrMagnitude > hoverMoveTolerancePixels * hoverMoveTolerancePixels)
            {
                // 鼠标动了：重新计时（要求“位置不动一段时间”）
                startPos = nowPos;
                elapsed = 0f;
            }
            else
            {
                elapsed += Time.unscaledDeltaTime;
            }

            yield return null;
        }

        if (_isHoveringSelf && _operationPanelInstance == null)
            ShowOperationPanel();
    }

    private IEnumerator DelayedCheckToHide()
    {
        // 给鼠标从 self 移动到 operation panel 一点缓冲
        yield return null;

        // 再稍微宽松一点（避免瞬间闪烁）
        float t = 0f;
        while (t < 0.05f)
        {
            if (_isHoveringSelf || _isHoveringOperationPanel)
                yield break;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_isHoveringSelf && !_isHoveringOperationPanel)
            HideOperationPanelImmediate();
    }

    private void ShowOperationPanel()
    {
        if (operationPanelPrefab == null || _operationPanelInstance != null)
            return;

        Transform parent = FindBestUIRoot(transform);
        _operationPanelInstance = Instantiate(operationPanelPrefab, parent);

        // 让 operation panel 也能汇报 hover 状态
        var operationPanel = _operationPanelInstance.GetComponent<OperationPanelUI>();
        if (operationPanel == null) operationPanel = _operationPanelInstance.AddComponent<OperationPanelUI>();

        if (curCarriageAssembler == null) { 
            Debug.LogError("ModulePanelUI: Attempting to show operation panel without valid CarriageAssmbler");
            return;
        }

        Action _onBuild = null;
        if (curModuleRuntime == null) {
            _onBuild = () =>
            {
                CraftableModuleListUI.Instance.ToggleCraftPanel(true);
                CarriageAssemblerUI.Instance.SetCurModulePanel(this, curModuleRuntime);
            };
        }
        Action _onUpgrade = null;
        //if (!curModuleRuntime.IsEmpty) {
        //    _onUpgrade = () => CraftableModuleListUI.Instance.ToggleCraftPanel(true);
        //}
        Action _onDelete = null;
        if (curModuleRuntime != null){
            _onDelete = () => curCarriageAssembler.Remove(curModuleRuntime.slotIndex);
        }

        OperationPanelData opData = new OperationPanelData
        {
            onEnter = OnEnterOperationPanel,
            onExit = OnExitOperationPanel,
            onBuild = _onBuild,
            onUpgrade = _onUpgrade,
            onDelete = _onDelete
        };

        operationPanel.Init(opData);

        // === 改动：定位到当前鼠标位置（屏幕坐标 -> parent RectTransform 本地坐标）===
        _operationPanelInstance.transform.position = Input.mousePosition + new Vector3(operationPanelOffset.x,operationPanelOffset.y,0);
    }

    private void HideOperationPanelImmediate()
    {
        if (_operationPanelInstance == null) return;
        Destroy(_operationPanelInstance);
        _operationPanelInstance = null;
    }

    private void StopHoverRoutine()
    {
        if (_hoverRoutine == null) return;
        StopCoroutine(_hoverRoutine);
        _hoverRoutine = null;
    }

    private static Transform FindBestUIRoot(Transform from)
    {
        // 优先挂到最近的 Canvas 下，避免层级/缩放问题
        var canvas = from.GetComponentInParent<Canvas>();
        return canvas != null ? canvas.transform : from.root;
    }


    public void ToggleSelected(bool isSelected) {
        selectedHighlightImage.enabled = isSelected;
    }

    private void OnEnterOperationPanel()
    {
        _isHoveringOperationPanel = true;
        // 停掉任何准备隐藏的协程
        StopHoverRoutine();
    }

    private void OnExitOperationPanel()
    {
        _isHoveringOperationPanel = false;
        StopHoverRoutine();
        _hoverRoutine = StartCoroutine(DelayedCheckToHide());
    }
}
