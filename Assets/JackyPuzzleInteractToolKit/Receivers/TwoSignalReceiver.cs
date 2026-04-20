using System;
using UnityEngine;

namespace JackyPuzzleInteract
{
    /// <summary>
    /// 双信号响应者基类。
    /// 适用于"信号A激活、信号B停用、可反复触发"的通用模式（门、阀门、平台等）。
    /// 子类只需 override OnActivated / OnDeactivated 实现具体表现。
    /// </summary>
    public abstract class TwoSignalReceiver : BaseReceiver
    {
        public Action OnReceiverActivated;
        public Action OnReceiverDeactivated;

        protected sealed override void HandleActivate(GameObject sender)
        {
            OnActivated(sender);
            OnReceiverActivated?.Invoke();
        }

        protected sealed override void HandleDeactivate(GameObject sender)
        {
            OnDeactivated(sender);
            OnReceiverDeactivated?.Invoke();
        }

        /// <summary> 子类实现：激活时的具体表现 </summary>
        protected abstract void OnActivated(GameObject sender);

        /// <summary> 子类实现：停用时的具体表现 </summary>
        protected abstract void OnDeactivated(GameObject sender);
    }
}
