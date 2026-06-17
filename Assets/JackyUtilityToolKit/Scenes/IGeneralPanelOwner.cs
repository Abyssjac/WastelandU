/// <summary>
/// Contract for any object that owns and fully controls a UI panel's lifecycle.
/// Implement this on the Manager (not the visual UI class) so that open and close
/// actions encompass the complete logic, not just the visual toggle.
///
/// Register with <see cref="AllUIManager.RequestOpen"/> to participate in
/// centralised panel management and unified keyboard input routing.
/// </summary>
public interface IGeneralPanelOwner
{
    /// <summary>
    /// Called by <see cref="AllUIManager"/> when this panel has been approved to open.
    /// Perform all logic required to enter the active state (e.g. EnterBuildMode).
    /// </summary>
    void OnPanelOpenRequested();

    /// <summary>
    /// Called by <see cref="AllUIManager"/> when this panel should close.
    /// Perform all cleanup logic required to fully exit the active state (e.g. ExitBuildMode).
    /// </summary>
    void OnPanelCloseRequested();
}
