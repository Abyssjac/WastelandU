using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton UI manager that centralises all panel open/close logic and keyboard input routing.
/// Place on a DontDestroyOnLoad GameObject alongside other persistent managers.
///
/// Responsibilities:
///   - Maintain a stack of currently open <see cref="IGeneralPanelOwner"/> panels.
///   - Route Escape key to the correct panel (close top of stack, or open fallback if stack is empty).
///   - Allow external systems (e.g. BuildManager) to temporarily block UI input during
///     non-interruptible operations such as active placement or move actions.
/// </summary>
public class AllUIManager : MonoBehaviour
{
    public static AllUIManager Instance { get; private set; }

    // ── Inspector ────────────────────────────────────────────────
    [Header("Input")]
    [SerializeField] private KeyCode escapeKey = KeyCode.Escape;

    [Header("Debug")]
    [SerializeField] private bool debugEnabled;

    // ── State ────────────────────────────────────────────────────
    /// <summary>
    /// When false, Escape key input is suppressed.
    /// Set to false by systems such as BuildManager during Placing / Moving states.
    /// </summary>
    public bool IsUIInputEnabled { get; private set; } = true;

    private readonly Stack<IGeneralPanelOwner> _panelStack = new Stack<IGeneralPanelOwner>();

    /// <summary>
    /// Panel that opens when Escape is pressed and the stack is empty (e.g. UISettingPanel).
    /// Register with <see cref="RegisterFallbackPanel"/>.
    /// </summary>
    private IGeneralPanelOwner _fallbackPanel;

    // ── Lifecycle ────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (!IsUIInputEnabled) return;

        if (Input.GetKeyDown(escapeKey))
            HandleEscape();
    }

    // ── Public API ───────────────────────────────────────────────

    /// <summary>
    /// Register a panel as the fallback. The fallback opens when Escape is pressed
    /// and the panel stack is empty. Typically used by UISettingPanel.
    /// </summary>
    public void RegisterFallbackPanel(IGeneralPanelOwner panel)
    {
        _fallbackPanel = panel;

        if (debugEnabled)
            Debug.Log($"[AllUIManager] Fallback panel registered: {panel}");
    }

    /// <summary>
    /// Request to open a panel. The <paramref name="openType"/> determines how existing
    /// open panels are handled.
    /// </summary>
    /// <param name="requester">The panel owner requesting to open.</param>
    /// <param name="openType">
    /// <see cref="PanelOpenType.Stack"/>: suspends the current top panel and restores it on close.
    /// <see cref="PanelOpenType.Override"/>: closes all open panels before opening this one.
    /// </param>
    public void RequestOpen(IGeneralPanelOwner requester, PanelOpenType openType)
    {
        if (requester == null) return;

        // If this panel is already the active top of the stack, do nothing.
        if (_panelStack.Count > 0 && _panelStack.Peek() == requester) return;

        if (openType == PanelOpenType.Override)
        {
            CloseAllPanels();
        }
        else // Stack
        {
            if (_panelStack.Count > 0)
                _panelStack.Peek().OnPanelCloseRequested();
        }

        _panelStack.Push(requester);
        requester.OnPanelOpenRequested();

        if (debugEnabled)
            Debug.Log($"[AllUIManager] Opened ({openType}): {requester}. Stack depth: {_panelStack.Count}");
    }

    /// <summary>
    /// Request to close a specific panel. Only acts if the panel is the current top of stack.
    /// Use this for programmatic closes (e.g. a Close button), not for Escape handling.
    /// </summary>
    public void RequestClose(IGeneralPanelOwner requester)
    {
        if (requester == null) return;
        if (_panelStack.Count == 0 || _panelStack.Peek() != requester) return;

        CloseTopPanel();
    }

    /// <summary>
    /// Enable or disable UI keyboard input processing.
    /// Pass false during non-interruptible states (e.g. BuildManager Placing/Moving).
    /// Pass true when returning to an interruptible state.
    /// </summary>
    public void SetUIInputEnabled(bool enabled)
    {
        IsUIInputEnabled = enabled;

        if (debugEnabled)
            Debug.Log($"[AllUIManager] UI input enabled: {enabled}");
    }

    // ── Private ──────────────────────────────────────────────────

    private void HandleEscape()
    {
        if (_panelStack.Count > 0)
        {
            CloseTopPanel();
        }
        else if (_fallbackPanel != null)
        {
            RequestOpen(_fallbackPanel, PanelOpenType.Override);

            if (debugEnabled)
                Debug.Log($"[AllUIManager] Stack empty — opening fallback via stack: {_fallbackPanel}");
        }
    }

    /// <summary>
    /// Pops the top panel, closes it, and reopens the new top (Stack semantics).
    /// </summary>
    private void CloseTopPanel()
    {
        if (_panelStack.Count == 0) return;

        IGeneralPanelOwner top = _panelStack.Pop();
        top.OnPanelCloseRequested();

        if (debugEnabled)
            Debug.Log($"[AllUIManager] Closed: {top}. Stack depth: {_panelStack.Count}");

        // Stack semantics: restore the panel that was underneath
        if (_panelStack.Count > 0)
        {
            _panelStack.Peek().OnPanelOpenRequested();

            if (debugEnabled)
                Debug.Log($"[AllUIManager] Restored: {_panelStack.Peek()}");
        }
    }

    /// <summary>Close every panel in the stack without restoring any previous panel.</summary>
    private void CloseAllPanels()
    {
        while (_panelStack.Count > 0)
        {
            IGeneralPanelOwner panel = _panelStack.Pop();
            panel.OnPanelCloseRequested();

            if (debugEnabled)
                Debug.Log($"[AllUIManager] Override — closed: {panel}");
        }
    }
}
