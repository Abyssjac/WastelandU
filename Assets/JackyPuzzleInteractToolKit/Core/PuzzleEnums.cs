namespace JackyPuzzleInteract
{
    /// <summary>
    /// LogicProperty 的 Database Key，按逻辑行为模式分类
    /// </summary>
    public enum Key_PuzzleLogicPP
    {
        None,
        PuzzleLogic_OneShot,
        PuzzleLogic_OneShotRevocable,
        PuzzleLogic_Toggle,
        PuzzleLogic_Sustained,
        PuzzleLogic_Sequence,
        PuzzleLogic_Counter,
        PuzzleLogic_TwoSignal,
    }

    /// <summary>
    /// 触发源发出的信号种类（泛型化，不绑定具体交互形式）
    /// </summary>
    public enum PuzzleSignalType
    {
        None,
        Signal_Activate,
        Signal_Deactivate,
        Signal_Toggle,
        Signal_Pulse,
        Signal_Increment,
        Signal_Decrement,
        Signal_Reset,
        Signal_Step,
    }

    /// <summary>
    /// LogicProperty 计算后的输出结果
    /// </summary>
    public enum PuzzleOutputType
    {
        None,
        Activate,
        Deactivate,
        Toggle,
        PermanentActivate,
    }
}
