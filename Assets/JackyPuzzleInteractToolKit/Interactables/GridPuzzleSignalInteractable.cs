using UnityEngine;
using JackyPuzzleInteract;

/// <summary>
/// Bridges an EnemyGridBehaviour's fulfilled state into puzzle signals.
/// Sends one initial signal on enable, then only sends again when state changes.
/// </summary>
public class GridPuzzleSignalInteractable : BaseInteractable
{
    [Header("Grid Source")]
    [SerializeField] private EnemyGridBehaviour targetGrid;
    [SerializeField] private Renderer gridPuzzleBaseRenderer;
    [SerializeField] private Material activeMaterial;
    [SerializeField] private Material deactiveMaterial;

    [Header("Signal Mapping")]
    [SerializeField] private PuzzleSignalType fulfilledSignal = PuzzleSignalType.Signal_Activate;
    [SerializeField] private PuzzleSignalType unfulfilledSignal = PuzzleSignalType.Signal_Deactivate;

    private bool hasLastState;
    private bool lastState;

    private void Awake()
    {
        //ResolveGridIfNeeded();
    }


    private void Start()
    {
        //SendSignal(targetGrid.IsGridFulfilled ? fulfilledSignal : unfulfilledSignal);
    }

    private void OnEnable()
    {
        //ResolveGridIfNeeded();
        if (targetGrid == null) return;

        //targetGrid.OnGridStateChanged += OnGridStateChanged;
        targetGrid.OnGridChanged += OnGridChanged;

        //bool initialState = targetGrid.IsGridFulfilled;
        //lastState = initialState;
        //hasLastState = true;
        //SendSignal(initialState ? fulfilledSignal : unfulfilledSignal);
        OnGridChanged();
    }

    private void OnDisable()
    {
        if (targetGrid != null) {
            //targetGrid.OnGridStateChanged -= OnGridStateChanged;
            targetGrid.OnGridChanged -= OnGridChanged;
        }
    }

    private void OnGridStateChanged(bool isFulfilled)
    {
        if (!hasLastState || lastState != isFulfilled)
        {
            lastState = isFulfilled;
            hasLastState = true;
            SendSignal(isFulfilled ? fulfilledSignal : unfulfilledSignal);
            UpdateGridPuzzleBaseMaterial(isFulfilled);
        }
    }

    private void OnGridChanged()
    {
        if (targetGrid == null) return;
        bool isFulfilled = targetGrid.IsGridFulfilled;
        SendSignal(isFulfilled ? fulfilledSignal : unfulfilledSignal);
        UpdateGridPuzzleBaseMaterial(isFulfilled);
    }

    //private void ResolveGridIfNeeded()
    //{
    //    if (targetGrid != null) return;
    //    targetGrid = GetComponentInParent<EnemyGridBehaviour>();
    //}

    private void UpdateGridPuzzleBaseMaterial(bool isFulfilled)
    {
        if (gridPuzzleBaseRenderer == null) return;
        gridPuzzleBaseRenderer.material = isFulfilled ? activeMaterial : deactiveMaterial;
    }
}   