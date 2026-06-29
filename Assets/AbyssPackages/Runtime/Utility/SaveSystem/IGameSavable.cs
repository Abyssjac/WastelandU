/// <summary>
/// Implemented by systems that contribute one section to the global game save file.
/// Each implementation owns its own section format.
/// </summary>
public interface IGameSavable
{
    string SaveKey { get; }
    int SaveVersion { get; }
    int LoadOrder { get; }

    string CaptureSaveJson();
    void RestoreSaveJson(string json);
    void OnNoSaveData();
}
