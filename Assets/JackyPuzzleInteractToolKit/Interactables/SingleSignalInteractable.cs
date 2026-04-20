using UnityEngine;
using JackyPuzzleInteract;

public class SingleSignalInteractable : BaseInteractable
{
    [Header("Signal Setting")]
    [SerializeField] private PuzzleSignalType signalToSend = PuzzleSignalType.Signal_Activate;
    [SerializeField] private bool sendOnce = false;
    private bool hasSent = false;

    /// <summary>
    /// Call This Method to Send the Configured Signal to All Linked Receivers.
    /// 任何时候调用此方法都会发送相同的 signalToSend，适合只需要一个信号的交互源（如单向按钮、压力板、连线接点等）。
    /// </summary>
    public void SendSingleSignal()
    {
        if (sendOnce && hasSent) return;
        SendSignal(signalToSend);
        hasSent = true;
    }
}
