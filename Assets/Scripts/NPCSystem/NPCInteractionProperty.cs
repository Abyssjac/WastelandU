using JackyUtility;
using UnityEngine;

/// <summary>
/// Static interaction configuration for one NPC. Runtime availability is owned by
/// <see cref="NPCManager"/> and must never be written back into this asset.
/// </summary>
[CreateAssetMenu(fileName = "NPCInteractionPP_", menuName = "AllProperties/NPCInteractionProperty")]
public class NPCInteractionProperty : EnumStringKeyedProperty<Key_NPC>
{
    [Header("Menu")]
    [Tooltip("When exactly one interaction is visible, execute it without opening the option menu.")]
    [SerializeField] private bool directExecuteWhenSingleOption = true;

    [Header("Talk")]
    [Tooltip("Whether this NPC exposes a Talk interaction.")]
    [SerializeField] private bool talkEnabled = true;

    [Tooltip("Yarn node started by the Talk interaction. Leave empty to disable Talk at runtime.")]
    [SerializeField] private string yarnStartNode = string.Empty;

    [Header("Store")]
    [Tooltip("Optional store opened by this NPC. A null reference means this NPC has no Store interaction.")]
    [SerializeField] private StoreInventoryProperty storeInventoryProperty;

    [Tooltip("Whether the configured Store interaction is available before any runtime unlock is granted.")]
    [SerializeField] private bool storeInitiallyUnlocked = true;

    public bool DirectExecuteWhenSingleOption => directExecuteWhenSingleOption;
    public bool TalkEnabled => talkEnabled && !string.IsNullOrWhiteSpace(yarnStartNode);
    public string YarnStartNode => yarnStartNode;
    public StoreInventoryProperty StoreInventoryProperty => storeInventoryProperty;
    public bool StoreInitiallyUnlocked => storeInitiallyUnlocked;
}

/// <summary>
/// Stable interaction categories supported by the first NPC interaction menu.
/// The pair of <see cref="Key_NPC"/> and this enum identifies one menu action.
/// </summary>
public enum NPCInteractionType
{
    None = 0,
    Talk = 1,
    OpenNPCPanel = 2,
    OpenStore = 3
}

/// <summary>
/// Persistent relationship state of an NPC. Spawned state deliberately does not
/// appear here because it is a scene-lifetime concern, not player progress.
/// </summary>
public enum NPCStatus
{
    Unrecruited = 0,
    Recruited = 1
}
