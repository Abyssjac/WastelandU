namespace JackyPuzzleInteract
{
    /// <summary>
    /// Receiver 的运行时状态快照，传递给 LogicProperty 用于复杂判定
    /// </summary>
    public struct PuzzleReceiverState
    {
        public int CurrentActiveCount;
        public int TotalActivationCount;
        public bool IsLocked;
        public bool IsCurrentlyActive;

        public PuzzleReceiverState(int currentActive, int totalActivation, bool locked, bool active)
        {
            CurrentActiveCount = currentActive;
            TotalActivationCount = totalActivation;
            IsLocked = locked;
            IsCurrentlyActive = active;
        }
    }
}
