/// <summary>
/// Defines how a panel interacts with other currently open panels
/// when it requests to open via <see cref="AllUIManager.RequestOpen"/>.
/// </summary>
public enum PanelOpenType
{
    /// <summary>
    /// Close the top panel in the stack first, then open this panel.
    /// When this panel is closed, the previously active panel is automatically reopened.
    /// </summary>
    Stack = 0,

    /// <summary>
    /// Close ALL currently open panels before opening this panel.
    /// The stack is cleared; no panel is restored when this one closes.
    /// </summary>
    Override = 1,
}
