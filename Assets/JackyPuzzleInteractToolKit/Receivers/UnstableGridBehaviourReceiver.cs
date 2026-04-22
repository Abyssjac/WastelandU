using UnityEngine;
using System.Collections.Generic;
using JackyPuzzleInteract;
using UnityEngine;

/// <summary>
/// Scene-level coordinator + TwoSignalReceiver.
///
/// Coordinator role (runs in Start):
///   Iterates every PlacedBuildableData in the target EnemyGridBehaviour,
///   filters by the configured enum keys, adds UnstableObjBehaviour to those
///   GameObjects if not already present, and calls Initialize() to inject
///   teleport points, stable target and unstable target at runtime.
///
/// Receiver role (puzzle signal):
///   OnActivated  ¡ú triggers StableAnim on all managed UnstableObjBehaviours.
///   OnDeactivated ¡ú triggers UnstableAnim on all managed UnstableObjBehaviours.
/// </summary>
public class UnstableGridBehaviourReceiver : TwoSignalReceiver
{
    [Header("Grid Source")]
    [SerializeField] private EnemyGridBehaviour gridBehaviour;

    [Header("Filter ¡ª which buildable keys become unstable")]
    [Tooltip("Only buildables whose enum key appears in this list will be managed.\n" +
             "Leave empty to manage ALL spawned buildables.")]
    [SerializeField] private Key_BuildablePP[] targetBuildableKeys = new Key_BuildablePP[0];

    [Header("Runtime Injection Data")]
    [Tooltip("Teleport points passed to every managed UnstableObjBehaviour.")]
    [SerializeField] private Transform[] teleportPoints = new Transform[0];

    [Tooltip("Stable target transform passed to every managed UnstableObjBehaviour.")]
    [SerializeField] private Transform stableTarget;

    [Tooltip("Unstable target transform passed to every managed UnstableObjBehaviour.")]
    [SerializeField] private Transform unstableTarget;

    [Header("Initialization")]
    [Tooltip("If true, objects enter UnstableAnim immediately after Initialize().\n" +
             "If false, they start with FloatAnim.")]
    [SerializeField] private bool startUnstableOnInit = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    // All managed instances resolved at runtime
    private readonly List<UnstableObjBehaviour> managedObjects = new List<UnstableObjBehaviour>();

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Lifecycle ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    protected override void Awake()
    {
        base.Awake();

        if (gridBehaviour == null)
            gridBehaviour = GetComponentInChildren<EnemyGridBehaviour>();
    }

    private void Start()
    {
        InitializeManagedObjects();
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Coordinator ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    private void InitializeManagedObjects()
    {
        managedObjects.Clear();

        if (gridBehaviour == null)
        {
            Debug.LogError($"[{name}] UnstableGridBehaviourReceiver: gridBehaviour is not set.", this);
            return;
        }

        if (teleportPoints == null || teleportPoints.Length == 0)
        {
            Debug.LogError($"[{name}] UnstableGridBehaviourReceiver: teleportPoints is empty. " +
                           "UnstableObjBehaviour requires at least one point.", this);
            return;
        }

        var allPlaced = gridBehaviour.Grid?.AllPlaced;
        if (allPlaced == null || allPlaced.Count == 0)
        {
            if (debugLog)
                Debug.LogWarning($"[{name}] UnstableGridBehaviourReceiver: grid has no placed objects.", this);
            return;
        }

        HashSet<Key_BuildablePP> filterSet = BuildFilterSet();

        foreach (var kvp in allPlaced)
        {
            PlacedBuildableData data = kvp.Value;
            if (data?.SpawnedObject == null) continue;
            if (filterSet.Count > 0 && !filterSet.Contains(data.Property.EnumKey)) continue;

            GameObject go = data.SpawnedObject;

            UnstableObjBehaviour unstable = go.GetComponent<UnstableObjBehaviour>();
            if (unstable == null)
                unstable = go.AddComponent<UnstableObjBehaviour>();

            unstable.Initialize(teleportPoints, stableTarget, unstableTarget, startUnstableOnInit);
            managedObjects.Add(unstable);

            if (debugLog)
                Debug.Log($"[{name}] Initialized UnstableObjBehaviour on '{go.name}' " +
                          $"(key={data.Property.EnumKey}).", this);
        }

        if (debugLog)
            Debug.Log($"[{name}] Total managed UnstableObjBehaviours: {managedObjects.Count}", this);
    }

    private HashSet<Key_BuildablePP> BuildFilterSet()
    {
        var set = new HashSet<Key_BuildablePP>();
        if (targetBuildableKeys == null) return set;
        for (int i = 0; i < targetBuildableKeys.Length; i++)
            set.Add(targetBuildableKeys[i]);
        return set;
    }

    // ©¤©¤©¤©¤©¤©¤©¤©¤©¤ Receiver ©¤©¤©¤©¤©¤©¤©¤©¤©¤

    protected override void OnActivated(GameObject sender)
    {
        for (int i = 0; i < managedObjects.Count; i++)
        {
            if (managedObjects[i] != null)
                managedObjects[i].TriggerStableAnim();
        }
    }

    protected override void OnDeactivated(GameObject sender)
    {
        for (int i = 0; i < managedObjects.Count; i++)
        {
            if (managedObjects[i] != null)
                managedObjects[i].TriggerUnstableAnim();
        }
    }
}
