using System;
using UnityEngine;

namespace JackyPuzzleInteract
{
    /// <summary>
    /// 所有交互触发源的基类。
    /// 子类决定 "何时" 发送哪种 PuzzleSignalType，基类负责分发给所有 linkedReceivers。
    /// </summary>
    public abstract class BaseInteractable : MonoBehaviour
    {
        [Header("Linked Receivers")]
        [SerializeField] private BaseReceiver[] linkedReceivers;

        public Action OnInteracted;

        /// <summary>
        /// 子类调用此方法将信号分发给所有关联的 Receiver
        /// </summary>
        protected void SendSignal(PuzzleSignalType signalType)
        {
            if (linkedReceivers == null) return;
            for (int i = 0; i < linkedReceivers.Length; i++)
            {
                if (linkedReceivers[i] != null)
                    linkedReceivers[i].ReceiveSignal(signalType, gameObject);
            }
            OnInteracted?.Invoke();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (linkedReceivers == null || linkedReceivers.Length == 0)
                Debug.LogWarning($"[{name}] BaseInteractable: 没有配置任何 Receiver！", this);
        }
#endif
    }
}
