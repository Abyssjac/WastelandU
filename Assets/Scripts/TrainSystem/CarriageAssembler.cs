using UnityEngine;
using System;
public class CarriageAssembler : MonoBehaviour
{
    [Header("Spawn Info")]
    //public Transform carriageParent;
    [SerializeField] private CarriageData initialCarriageData;

    [Header("Debug")]
    public CarriageView currentCarriageView;
    public CarriageRuntime runtime;

    // 场景里每个slot当前的ModuleView（用于快速销毁/替换）
    private ModuleView[] _moduleViewsBySlot;

    public event Action OnChanged; // UI刷新用

    private void Start()
    {
        if (initialCarriageData != null)
        {
            Build(initialCarriageData);
        }
    }

    public void Build(CarriageData carriageData)
    {
        if (carriageData == null || carriageData.prefab == null)
        {
            Debug.LogError("[CarriageAssembler] CarriageData or prefab is null.");
            return;
        }

        // 清理旧车厢
        if (currentCarriageView != null)
        {
            Destroy(currentCarriageView.gameObject);
            currentCarriageView = null;
        }

        //// 生成新车厢
        //var go = Instantiate(carriageData.prefab, carriageParent ? carriageParent : transform);
        //currentCarriageView = go.GetComponent<CarriageView>();
        //if (currentCarriageView == null)
        //{
        //    Debug.LogError("[CarriageAssembler] Carriage prefab must have CarriageView component.");
        //    Destroy(go);
        //    return;
        //}

        currentCarriageView = GetComponent<CarriageView>();

        // 创建runtime
        runtime = new CarriageRuntime(currentCarriageView.SlotCount)
        {
            carriageInstanceId = Guid.NewGuid().ToString("N"),
            carriageDataId = carriageData.id
        };

        _moduleViewsBySlot = new ModuleView[currentCarriageView.SlotCount];

        OnChanged?.Invoke();
    }

    public bool Install(int slotIndex, ModuleData moduleData)
    {
        if (!EnsureReady()) return false;
        Debug.Log($"[CarriageAssembler] Installing module {moduleData.displayName} to slot {slotIndex}");
        if (!IsSlotValid(slotIndex)) return false;
        Debug.Log($"[CarriageAssembler] Slot {slotIndex} is valid.");

        if (moduleData == null || moduleData.prefab == null)
        {
            Debug.LogWarning("[CarriageAssembler] moduleData or prefab is null.");
            return false;
        }

        // slotType校验
        var slotType = currentCarriageView.GetSlotType(slotIndex);
        if (slotType != moduleData.slotType)
        {
            Debug.LogWarning($"[CarriageAssembler] SlotType mismatch. Slot={slotType}, Module={moduleData.slotType}");
            return false;
        }

        // 如果已有模块，先移除
        Remove(slotIndex);

        var anchor = currentCarriageView.GetAnchor(slotIndex);
        if (anchor == null)
        {
            Debug.LogError("[CarriageAssembler] Slot anchor not found.");
            return false;
        }

        // 实例化模块并挂到anchor
        var moduleGO = Instantiate(moduleData.prefab, anchor);
        Debug.Log($"[CarriageAssembler] Instantiated module prefab: {moduleGO.name}");
        moduleGO.transform.localPosition = Vector3.zero;
        moduleGO.transform.localRotation = Quaternion.identity;
        moduleGO.transform.localScale = Vector3.one;

        var view = moduleGO.GetComponent<ModuleView>();
        if (view == null) view = moduleGO.AddComponent<ModuleView>();

        // 写入runtime
        var instanceId = Guid.NewGuid().ToString("N");
        view.moduleInstanceId = instanceId;
        view.moduleDataId = moduleData.id;

        runtime.modulesBySlot[slotIndex] = new ModuleRuntime
        {
            moduleInstanceId = instanceId,
            moduleDataId = moduleData.id,
            slotIndex = slotIndex
        };

        _moduleViewsBySlot[slotIndex] = view;

        OnChanged?.Invoke();
        return true;
    }

    public bool Remove(int slotIndex)
    {
        if (!EnsureReady()) return false;
        if (!IsSlotValid(slotIndex)) return false;

        // 删场景对象
        var view = _moduleViewsBySlot[slotIndex];
        if (view != null)
        {
            Destroy(view.gameObject);
            _moduleViewsBySlot[slotIndex] = null;
        }

        // 清runtime
        runtime.modulesBySlot[slotIndex] = null;

        OnChanged?.Invoke();
        return true;
    }

    public string GetSlotStatusText(int slotIndex)
    {
        if (!EnsureReady()) return "No Carriage";
        if (!IsSlotValid(slotIndex)) return "Invalid Slot";

        var r = runtime.modulesBySlot[slotIndex];
        return r == null ? "Empty" : $"Installed: {r.moduleDataId}";
    }

    private void OnTriggerEnter(Collider collision)
    {
        Debug.Log($"[CarriageAssembler] OnTriggerEnter2D with {collision.name}");
        CarriageAssemblerUI.Instance.OpenUIPanel(this);
    }

    private void OnTriggerExit(Collider collision)
    {
        Debug.Log($"[CarriageAssembler] OnTriggerEnter2D with {collision.name}");
        CarriageAssemblerUI.Instance.CloseUIPanel();
    }


    private bool EnsureReady()
    {
        return currentCarriageView != null && runtime != null && runtime.modulesBySlot != null;
    }

    private bool IsSlotValid(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < currentCarriageView.SlotCount;
    }
}
