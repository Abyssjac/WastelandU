using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerControl : MonoBehaviour
{
    [Header("Action References (drag from .inputactions)")]
    [SerializeField] private InputActionReference move;
    [SerializeField] private InputActionReference interact;
    [SerializeField] private InputActionReference dash;

    [Header("Optional: Other actions to manage")]
    [Tooltip("额外想调试/校验/开关的 actions（比如 Look, Jump, Inventory, etc.）")]
    [SerializeField] private List<InputActionReference> extraActions = new();

    [Header("Init Debug / Validation")]
    [SerializeField] private bool logActionsOnInit = true;
    [SerializeField] private bool autoEnableOnStart = true;

    [Header("Startup Toggles")]
    [Tooltip("启动时禁用这些 action（强类型引用，不怕拼写错）")]
    [SerializeField] private List<InputActionReference> disableOnStart = new();

    [Tooltip("启动时启用这些 action（强类型引用）")]
    [SerializeField] private List<InputActionReference> enableOnStart = new();

    // === Public API ===
    public Vector2 MoveInput() => ReadValue<Vector2>(move);

    public bool InteractTriggered() => WasTriggered(interact);
    public bool DashTriggered() => WasTriggered(dash);

    /// <summary>Enable/Disable 任意 action（可给外部系统用）</summary>
    public void SetEnabled(InputActionReference actionRef, bool enabled)
    {
        var a = GetAction(actionRef);
        if (a == null) return;

        if (enabled) a.Enable();
        else a.Disable();
    }

    /// <summary>一次性开关多个 action</summary>
    public void SetEnabled(IEnumerable<InputActionReference> refs, bool enabled)
    {
        if (refs == null) return;
        foreach (var r in refs) SetEnabled(r, enabled);
    }

    private void Awake()
    {
        // 可选：如果你希望这个组件完全独立于 PlayerInput，也可以不 RequireComponent(PlayerInput)
        // 只要引用的 InputActionReference 来自同一个 asset，照样能工作。
    }

    private void OnEnable()
    {
        // InputActionReference 默认不会自动 Enable（取决于你的 PlayerInput/你是否手动 Enable）
        if (autoEnableOnStart)
        {
            EnableCoreIfValid();
            SetEnabled(enableOnStart, true);
            SetEnabled(disableOnStart, false);
        }

        if (logActionsOnInit)
            LogAllConfiguredActions();
    }

    private void OnDisable()
    {
        // 这里通常不强制 Disable，避免影响别的系统/组件
        // 如果你想彻底“组件下线就禁用输入”，可以改成 DisableCoreIfValid();
    }

    // =========================
    // Internal helpers
    // =========================

    private void EnableCoreIfValid()
    {
        // 核心 actions 如果绑定了就 enable
        TryEnable(move);
        TryEnable(interact);
        TryEnable(dash);

        // 额外 actions 也可启用（看你需求）
        if (extraActions != null)
        {
            foreach (var a in extraActions) TryEnable(a);
        }
    }

    private void DisableCoreIfValid()
    {
        TryDisable(move);
        TryDisable(interact);
        TryDisable(dash);

        if (extraActions != null)
        {
            foreach (var a in extraActions) TryDisable(a);
        }
    }

    private static InputAction GetAction(InputActionReference actionRef)
    {
        if (actionRef == null)
        {
            Debug.LogWarning("[PlayerControl] InputActionReference is NULL (not assigned).");
            return null;
        }

        if (actionRef.action == null)
        {
            Debug.LogWarning($"[PlayerControl] InputActionReference '{actionRef.name}' has NULL action. " +
                             $"(Did you drag the correct action from the asset?)");
            return null;
        }

        return actionRef.action;
    }

    private static void TryEnable(InputActionReference actionRef)
    {
        var a = GetAction(actionRef);
        if (a == null) return;

        if (!a.enabled) a.Enable();
    }

    private static void TryDisable(InputActionReference actionRef)
    {
        var a = GetAction(actionRef);
        if (a == null) return;

        if (a.enabled) a.Disable();
    }

    private static T ReadValue<T>(InputActionReference actionRef) where T : struct
    {
        var a = GetAction(actionRef);
        if (a == null) return default;
        return a.ReadValue<T>();
    }

    private static bool WasTriggered(InputActionReference actionRef)
    {
        var a = GetAction(actionRef);
        if (a == null) return false;
        return a.triggered;
    }

    private void LogAllConfiguredActions()
    {
        Debug.Log("[PlayerControl] ===== Configured Actions =====", this);

        LogOne("Move", move);
        LogOne("Interact", interact);
        LogOne("Dash", dash);

        if (extraActions != null && extraActions.Count > 0)
        {
            Debug.Log("[PlayerControl] ----- Extra Actions -----", this);
            foreach (var a in extraActions)
            {
                LogOne(a != null ? a.name : "NULL", a);
            }
        }

        if (enableOnStart != null && enableOnStart.Count > 0)
        {
            Debug.Log("[PlayerControl] ----- Enable On Start -----", this);
            foreach (var a in enableOnStart) LogOne(a != null ? a.name : "NULL", a);
        }

        if (disableOnStart != null && disableOnStart.Count > 0)
        {
            Debug.Log("[PlayerControl] ----- Disable On Start -----", this);
            foreach (var a in disableOnStart) LogOne(a != null ? a.name : "NULL", a);
        }

        Debug.Log("[PlayerControl] =============================", this);
    }

    private void LogOne(string label, InputActionReference actionRef)
    {
        if (actionRef == null)
        {
            Debug.LogWarning($"[PlayerControl] {label}: NOT ASSIGNED", this);
            return;
        }

        var a = actionRef.action;
        if (a == null)
        {
            Debug.LogWarning($"[PlayerControl] {label}: assigned ref '{actionRef.name}' but action is NULL", this);
            return;
        }

        string mapName = a.actionMap != null ? a.actionMap.name : "(no map)";
        Debug.Log($"[PlayerControl] {label}: action='{a.name}', map='{mapName}', enabled={a.enabled}, type={a.type}", this);
    }
}
