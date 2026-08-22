using UnityEngine;

/// <summary>
/// Registers a scene-placed NPC while its GameObject is enabled and unregisters
/// it before its additive scene is unloaded. Attach beside NPCBehaviour on every
/// NPC prefab that can be placed directly in a scene.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NPCBehaviour))]
public class NPCRegister : MonoBehaviour
{
    private NPCBehaviour _npcBehaviour;
    private bool _registered;

    private void Awake()
    {
        _npcBehaviour = GetComponent<NPCBehaviour>();
    }

    private void OnEnable()
    {
        TryRegister();
    }

    // NPCManager may be instantiated after this scene object is enabled.
    private void Start()
    {
        TryRegister();
    }

    private void OnDisable()
    {
        if (!_registered)
            return;

        NPCManager manager = NPCManager.Instance;
        if (manager != null)
            manager.UnregisterNPC(_npcBehaviour);

        _registered = false;
    }

    private void TryRegister()
    {
        if (_registered)
            return;

        if (_npcBehaviour == null)
            _npcBehaviour = GetComponent<NPCBehaviour>();

        NPCManager manager = NPCManager.Instance;
        if (manager == null || _npcBehaviour == null)
            return;

        _registered = manager.RegisterNPC(_npcBehaviour);
    }
}
