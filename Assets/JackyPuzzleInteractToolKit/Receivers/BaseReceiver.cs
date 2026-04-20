using System;
using UnityEngine;
using JackyUtility;

namespace JackyPuzzleInteract
{
    /// <summary>
    /// 所有响应者的基类。
    /// 通过 logicKey 从 Database 查找对应的 PuzzleInteractLogicProperty，
    /// 由 LogicProperty.EvaluateSignal 决定输出，再由子类执行具体行为。
    /// </summary>
    public abstract class BaseReceiver : MonoBehaviour
    {
        [Header("Logic Configuration")]
        [SerializeField] private Key_PuzzleLogicPP logicKey = Key_PuzzleLogicPP.None;

        private PuzzleInteractLogicProperty _resolvedLogic;
        private int _currentActiveCount;
        private int _totalActivationCount;
        private bool _isLocked;
        private bool _isCurrentlyActive;

        public Action<PuzzleOutputType> OnOutputChanged;

        protected virtual void Awake()
        {
            ResolveLogic();
        }

        private void ResolveLogic()
        {
            if (logicKey == Key_PuzzleLogicPP.None)
            {
                Debug.LogError($"[{name}] BaseReceiver: logicKey 为 None，无法查找 LogicProperty！", this);
                return;
            }

            if (PropertyDatabaseManager.Instance == null)
            {
                Debug.LogError($"[{name}] BaseReceiver: PropertyDatabaseManager 不存在！", this);
                return;
            }

            var db = PropertyDatabaseManager.Instance.GetDatabase<PuzzleInteractLogicDatabase>();
            if (db != null)
                _resolvedLogic = db.GetByEnum(logicKey);

            if (_resolvedLogic == null)
                Debug.LogError($"[{name}] BaseReceiver: 无法通过 Key [{logicKey}] 找到 LogicProperty！", this);
        }

        public void ReceiveSignal(PuzzleSignalType signalType, GameObject sender)
        {
            if (_isLocked || _resolvedLogic == null) return;

            var state = new PuzzleReceiverState(
                _currentActiveCount,
                _totalActivationCount,
                _isLocked,
                _isCurrentlyActive
            );

            PuzzleOutputType output = _resolvedLogic.EvaluateSignal(signalType, state);

            if (output == PuzzleOutputType.None) return;

            _totalActivationCount++;
            ExecuteOutput(output, sender);
            OnOutputChanged?.Invoke(output);
        }

        private void ExecuteOutput(PuzzleOutputType output, GameObject sender)
        {
            switch (output)
            {
                case PuzzleOutputType.Activate:
                    _currentActiveCount++;
                    if (_currentActiveCount >= _resolvedLogic.RequiredActiveCount)
                    {
                        _isCurrentlyActive = true;
                        HandleActivate(sender);
                    }
                    break;

                case PuzzleOutputType.Deactivate:
                    _currentActiveCount = Mathf.Max(0, _currentActiveCount - 1);
                    if (_currentActiveCount < _resolvedLogic.RequiredActiveCount)
                    {
                        _isCurrentlyActive = false;
                        HandleDeactivate(sender);
                    }
                    break;

                case PuzzleOutputType.Toggle:
                    _isCurrentlyActive = !_isCurrentlyActive;
                    if (_isCurrentlyActive)
                        HandleActivate(sender);
                    else
                        HandleDeactivate(sender);
                    break;

                case PuzzleOutputType.PermanentActivate:
                    _isCurrentlyActive = true;
                    _isLocked = _resolvedLogic.LockOnActivate;
                    HandleActivate(sender);
                    break;
            }
        }

        /// <summary> 子类实现：激活时的具体行为（开门、转阀门、亮灯等） </summary>
        protected abstract void HandleActivate(GameObject sender);

        /// <summary> 子类实现：停用时的具体行为（关门、停转、灭灯等） </summary>
        protected abstract void HandleDeactivate(GameObject sender);
    }
}
