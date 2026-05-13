/// <summary>
/// Contract that any panel opened by a <see cref="BasePanelInteractable"/> must fulfil.
/// The panel is responsible for calling <see cref="BasePanelInteractable.NotifyPanelClosed"/>
/// when the player finishes interacting so the interact state can be correctly released.
/// </summary>
public interface IInteractablePanel
{
    /// <summary>
    /// Open this panel, binding it to the given <paramref name="owner"/> interactable.
    /// The panel must hold the <paramref name="owner"/> reference and call
    /// <see cref="BasePanelInteractable.NotifyPanelClosed"/> when the player exits.
    /// </summary>
    void OpenPanel(BasePanelInteractable owner);

    /// <summary>
    /// Forcibly close the panel without notifying the owner interactable.
    /// Use this only for external cleanup (e.g. scene unload).
    /// </summary>
    void ClosePanel();
}
