using System;
using System.Collections.Generic;
using UnityEngine;
using JackyUtility;

namespace JackyPuzzleInteract
{
    [CreateAssetMenu(fileName = "PuzzleLogicPP_", menuName = "AllProperties/PuzzleInteractLogicProperty")]
    public class PuzzleInteractLogicProperty : EnumStringKeyedProperty<Key_PuzzleLogicPP>
    {
        [Serializable]
        public struct SignalMapping
        {
            public PuzzleSignalType inputSignal;
            public PuzzleOutputType outputResult;
        }

        [Header("Signal → Output Mappings")]
        [SerializeField] private SignalMapping[] signalMappings = new SignalMapping[0];

        [Header("Logic Settings")]
        [Tooltip("需要同时满足多少路激活信号才真正输出（AND 门逻辑）")]
        [SerializeField] private int requiredActiveCount = 1;

        [Tooltip("激活后是否永久锁定，不再接受新信号")]
        [SerializeField] private bool lockOnActivate = false;

        public int RequiredActiveCount => requiredActiveCount;
        public bool LockOnActivate => lockOnActivate;

        private Dictionary<PuzzleSignalType, PuzzleOutputType> _mappingCache;

        /// <summary>
        /// 核心方法：接收一个信号 + 当前 Receiver 状态，返回应当执行的输出。
        /// 子类可 override 实现序列验证、计数器、状态机等复杂逻辑。
        /// </summary>
        public virtual PuzzleOutputType EvaluateSignal(PuzzleSignalType signal, PuzzleReceiverState state)
        {
            EnsureMappingCache();

            if (_mappingCache.TryGetValue(signal, out var output))
                return output;

            return PuzzleOutputType.None;
        }

        private void EnsureMappingCache()
        {
            if (_mappingCache != null) return;
            _mappingCache = new Dictionary<PuzzleSignalType, PuzzleOutputType>();
            for (int i = 0; i < signalMappings.Length; i++)
            {
                var m = signalMappings[i];
                if (!_mappingCache.ContainsKey(m.inputSignal))
                    _mappingCache.Add(m.inputSignal, m.outputResult);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _mappingCache = null;
        }
#endif
    }
}
