using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum IslandTravelLocation
{
    MainWorld = 0,
    OnIsland = 1,
}

[DisallowMultipleComponent]
public class IslandTravelManager : MonoBehaviour
{
    public static IslandTravelManager Instance { get; private set; }

    [Header("Main World")]
    [Tooltip("Presentation roots hidden while the island scene is active. Do not include this manager, PlayerManager, the player, or Ship Dock Root.")]
    [SerializeField] private List<GameObject> presentationRootsToHideOnIsland = new List<GameObject>();
    [SerializeField] private Transform mainWorldReturnAnchor;

    [Header("Ship")]
    [Tooltip("The transform that moves the ship as a whole. It must not be under any hidden presentation root.")]
    [SerializeField] private Transform shipDockRoot;
    [Tooltip("Paused while docked so voyage rotation and bobbing cannot overwrite the dock anchor pose.")]
    [SerializeField] private FlightShipVisualController shipVisualController;

    [Header("Debug")]
    [SerializeField] private bool logTransitions;

    private readonly Dictionary<GameObject, bool> originalPresentationRootStates = new Dictionary<GameObject, bool>();

    private IslandTravelLocation location = IslandTravelLocation.MainWorld;
    private bool isTransitioning;
    private Scene loadedIslandScene;
    private IslandController currentIslandController;
    private MapNodeRuntime currentIslandNode;
    private string currentIslandSceneKey;
    private PlayerAgent movementLockedPlayer;
    private Vector3 shipOriginalPosition;
    private Quaternion shipOriginalRotation;
    private bool hasShipOriginalTransform;

    public IslandTravelLocation Location => location;
    public bool IsTransitioning => isTransitioning;
    public MapNodeRuntime CurrentIslandNode => currentIslandNode;
    public IslandController CurrentIslandController => currentIslandController;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        ReleaseMovementLock();

        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// Returns whether the player can enter the island currently docked beside the ship.
    /// Route planning is allowed while docked, so this deliberately does not require
    /// <see cref="FlightState.Arrived"/>.
    /// </summary>
    public bool CanEnterCurrentDockedIsland(out string failReason)
    {
        failReason = string.Empty;

        if (isTransitioning)
        {
            failReason = "An island transition is already in progress.";
            return false;
        }

        if (location != IslandTravelLocation.MainWorld)
        {
            failReason = "Player is already on an island.";
            return false;
        }

        if (mainWorldReturnAnchor == null)
        {
            failReason = "Main World Return Anchor is not assigned.";
            return false;
        }

        if (shipDockRoot == null)
        {
            failReason = "Ship Dock Root is not assigned.";
            return false;
        }

        FlightManager flightManager = FlightManager.Instance;
        if (flightManager == null || !flightManager.TryGetCurrentIslandNode(out MapNodeRuntime dockedNode))
        {
            failReason = "No docked island is available.";
            return false;
        }

        if (dockedNode.Property == null || string.IsNullOrWhiteSpace(dockedNode.Property.islandSceneKey))
        {
            failReason = $"Map node '{dockedNode.DisplayName}' has no Island Scene Key.";
            return false;
        }

        if (MySceneManager.Instance == null)
        {
            failReason = "MySceneManager is not available.";
            return false;
        }

        if (MySceneManager.Instance.IsSceneLoaded(dockedNode.Property.islandSceneKey))
        {
            failReason = $"Island scene '{dockedNode.Property.islandSceneKey}' is already loaded.";
            return false;
        }

        return TryGetActivePlayerMovement(out _, out _, out failReason);
    }

    /// <summary>
    /// Legacy entry point retained for callers compiled against the former API.
    /// Its meaning is now the currently docked island, regardless of route-planning state.
    /// </summary>
    public bool CanEnterCurrentArrivedIsland(out string failReason)
    {
        return CanEnterCurrentDockedIsland(out failReason);
    }

    public bool CanReturnToMainWorld(out string failReason)
    {
        failReason = string.Empty;

        if (isTransitioning)
        {
            failReason = "An island transition is already in progress.";
            return false;
        }

        if (location != IslandTravelLocation.OnIsland)
        {
            failReason = "Player is not currently on an island.";
            return false;
        }

        if (mainWorldReturnAnchor == null)
        {
            failReason = "Main World Return Anchor is not assigned.";
            return false;
        }

        return TryGetActivePlayerMovement(out _, out _, out failReason);
    }

    public bool RequestEnterCurrentIsland()
    {
        if (!CanEnterCurrentDockedIsland(out string failReason))
        {
            Debug.LogWarning($"[{nameof(IslandTravelManager)}] Cannot enter island: {failReason}", this);
            return false;
        }

        if (!FlightManager.Instance.TryGetCurrentIslandNode(out MapNodeRuntime node))
        {
            Debug.LogWarning($"[{nameof(IslandTravelManager)}] Cannot enter island: No docked island is available.", this);
            return false;
        }

        isTransitioning = true;
        StartCoroutine(EnterIslandRoutine(node, node.Property.islandSceneKey));
        return true;
    }

    public bool RequestReturnToMainWorld()
    {
        if (!CanReturnToMainWorld(out string failReason))
        {
            Debug.LogWarning($"[{nameof(IslandTravelManager)}] Cannot return to main world: {failReason}", this);
            return false;
        }

        isTransitioning = true;
        StartCoroutine(ReturnToMainWorldRoutine());
        return true;
    }

    private IEnumerator EnterIslandRoutine(MapNodeRuntime node, string islandSceneKey)
    {
        if (!TryGetActivePlayerMovement(out PlayerAgent player, out PlayerMovementCC playerMovement, out string failReason))
        {
            AbortEnter(islandSceneKey, failReason);
            yield break;
        }

        if (!AcquireMovementLock(player))
        {
            AbortEnter(islandSceneKey, "Could not acquire the active player's movement lock.");
            yield break;
        }

        AsyncOperation loadOperation = MySceneManager.Instance.LoadAdditiveSceneAsync(islandSceneKey);
        if (loadOperation == null)
        {
            AbortEnter(islandSceneKey, "Additive scene load could not be started.");
            yield break;
        }

        yield return loadOperation;

        Scene islandScene = SceneManager.GetSceneByName(islandSceneKey);
        if (!islandScene.isLoaded || !TryFindIslandController(islandScene, out IslandController islandController, out failReason))
        {
            AbortEnter(islandSceneKey, failReason);
            yield break;
        }

        if (islandController.PlayerLandingAnchor == null || islandController.ShipDockAnchor == null)
        {
            AbortEnter(islandSceneKey, "IslandController requires both Player Landing Anchor and Ship Dock Anchor.");
            yield break;
        }

        CaptureShipTransform();
        HideMainWorldPresentationRoots();
        SetShipDocked(true);
        MoveShipTo(islandController.ShipDockAnchor);
        playerMovement.TeleportToPosition(
            islandController.PlayerLandingAnchor.position,
            islandController.PlayerLandingAnchor.rotation);

        loadedIslandScene = islandScene;
        currentIslandController = islandController;
        currentIslandNode = node;
        currentIslandSceneKey = islandSceneKey;
        location = IslandTravelLocation.OnIsland;
        isTransitioning = false;
        ReleaseMovementLock();
        Log($"Entered island scene '{islandSceneKey}' for node '{node.DisplayName}'.");
    }

    private IEnumerator ReturnToMainWorldRoutine()
    {
        if (!TryGetActivePlayerMovement(out PlayerAgent player, out PlayerMovementCC playerMovement, out string failReason))
        {
            AbortReturn(failReason);
            yield break;
        }

        if (!AcquireMovementLock(player))
        {
            AbortReturn("Could not acquire the active player's movement lock.");
            yield break;
        }

        RestoreMainWorldPresentationRoots();
        RestoreShipTransform();
        SetShipDocked(false);
        playerMovement.TeleportToPosition(mainWorldReturnAnchor.position, mainWorldReturnAnchor.rotation);

        string islandSceneKey = currentIslandSceneKey;
        if (!string.IsNullOrWhiteSpace(islandSceneKey) && MySceneManager.Instance != null && MySceneManager.Instance.IsAdditivelyLoaded(islandSceneKey))
        {
            AsyncOperation unloadOperation = MySceneManager.Instance.UnloadAdditiveSceneAsync(islandSceneKey);
            if (unloadOperation != null)
                yield return unloadOperation;
        }

        ClearIslandRuntimeState();
        location = IslandTravelLocation.MainWorld;
        isTransitioning = false;
        ReleaseMovementLock();
        Log("Returned to main world.");
    }

    private void AbortEnter(string islandSceneKey, string reason)
    {
        RestoreMainWorldPresentationRoots();
        RestoreShipTransform();
        SetShipDocked(false);

        if (!string.IsNullOrWhiteSpace(islandSceneKey) && MySceneManager.Instance != null && MySceneManager.Instance.IsAdditivelyLoaded(islandSceneKey))
            StartCoroutine(UnloadAfterFailedEnter(islandSceneKey));

        ClearIslandRuntimeState();
        location = IslandTravelLocation.MainWorld;
        isTransitioning = false;
        ReleaseMovementLock();
        Debug.LogWarning($"[{nameof(IslandTravelManager)}] Island entry aborted: {reason}", this);
    }

    private IEnumerator UnloadAfterFailedEnter(string islandSceneKey)
    {
        AsyncOperation unloadOperation = MySceneManager.Instance.UnloadAdditiveSceneAsync(islandSceneKey);
        if (unloadOperation != null)
            yield return unloadOperation;
    }

    private void AbortReturn(string reason)
    {
        isTransitioning = false;
        ReleaseMovementLock();
        Debug.LogWarning($"[{nameof(IslandTravelManager)}] Return aborted: {reason}", this);
    }

    private bool TryGetActivePlayerMovement(out PlayerAgent player, out PlayerMovementCC playerMovement, out string failReason)
    {
        player = null;
        playerMovement = null;
        failReason = string.Empty;

        if (PlayerManager.Instance == null || !PlayerManager.Instance.TryGetActivePlayer(out player))
        {
            failReason = "PlayerManager has no active player.";
            return false;
        }

        playerMovement = player.PlayerMovementCC;
        if (playerMovement == null)
        {
            failReason = "Active PlayerAgent has no PlayerMovementCC reference.";
            return false;
        }

        return true;
    }

    private bool AcquireMovementLock(PlayerAgent player)
    {
        if (player == null)
            return false;

        if (player.AcquireMovementLock(this) || player.HasMovementLockOwner(this))
        {
            movementLockedPlayer = player;
            return true;
        }

        return false;
    }

    private void ReleaseMovementLock()
    {
        if (movementLockedPlayer == null)
            return;

        movementLockedPlayer.ReleaseMovementLock(this);
        movementLockedPlayer = null;
    }

    private void CaptureShipTransform()
    {
        if (shipDockRoot == null)
            return;

        shipOriginalPosition = shipDockRoot.position;
        shipOriginalRotation = shipDockRoot.rotation;
        hasShipOriginalTransform = true;
    }

    private void MoveShipTo(Transform dockAnchor)
    {
        if (shipDockRoot == null || dockAnchor == null)
            return;

        shipDockRoot.SetPositionAndRotation(dockAnchor.position, dockAnchor.rotation);
    }

    private void RestoreShipTransform()
    {
        if (!hasShipOriginalTransform || shipDockRoot == null)
            return;

        shipDockRoot.SetPositionAndRotation(shipOriginalPosition, shipOriginalRotation);
        hasShipOriginalTransform = false;
    }

    private void SetShipDocked(bool docked)
    {
        shipVisualController?.SetDocked(docked);
    }

    private void HideMainWorldPresentationRoots()
    {
        originalPresentationRootStates.Clear();

        for (int i = 0; i < presentationRootsToHideOnIsland.Count; i++)
        {
            GameObject root = presentationRootsToHideOnIsland[i];
            if (root == null)
                continue;

            if (root == gameObject || transform.IsChildOf(root.transform))
            {
                Debug.LogWarning($"[{nameof(IslandTravelManager)}] Ignoring presentation root '{root.name}' because it contains this manager.", this);
                continue;
            }

            originalPresentationRootStates[root] = root.activeSelf;
            root.SetActive(false);
        }
    }

    private void RestoreMainWorldPresentationRoots()
    {
        foreach (KeyValuePair<GameObject, bool> pair in originalPresentationRootStates)
        {
            if (pair.Key != null)
                pair.Key.SetActive(pair.Value);
        }

        originalPresentationRootStates.Clear();
    }

    private static bool TryFindIslandController(Scene islandScene, out IslandController islandController, out string failReason)
    {
        islandController = null;
        failReason = string.Empty;

        List<IslandController> controllers = new List<IslandController>();
        GameObject[] roots = islandScene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
            controllers.AddRange(roots[i].GetComponentsInChildren<IslandController>(true));

        if (controllers.Count != 1)
        {
            failReason = controllers.Count == 0
                ? $"No {nameof(IslandController)} was found in scene '{islandScene.name}'."
                : $"Scene '{islandScene.name}' has {controllers.Count} {nameof(IslandController)} components; exactly one is required.";
            return false;
        }

        islandController = controllers[0];
        return true;
    }

    private void ClearIslandRuntimeState()
    {
        loadedIslandScene = default;
        currentIslandController = null;
        currentIslandNode = null;
        currentIslandSceneKey = null;
    }

    private void Log(string message)
    {
        if (logTransitions)
            Debug.Log($"[{nameof(IslandTravelManager)}] {message}", this);
    }
}
