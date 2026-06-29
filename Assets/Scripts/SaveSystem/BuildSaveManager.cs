/// <summary>
/// Compatibility wrapper for existing prefabs that still reference BuildSaveManager.
/// New code should use <see cref="GameSaveManager"/> directly.
/// </summary>
public class BuildSaveManager : GameSaveManager
{
}
