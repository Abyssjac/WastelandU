using UnityEngine;

/// <summary>
/// Central controller for an enemy. Holds references to grid-related components
/// and listens for the grid-fulfilled event to trigger death.
/// </summary>
public class EnemyManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyGridBehaviour gridBehaviour;

    private void Awake()
    {
        if (gridBehaviour == null)
            gridBehaviour = GetComponentInChildren<EnemyGridBehaviour>();
    }

    private void OnEnable()
    {
        if (gridBehaviour != null)
            gridBehaviour.OnGridFulfilled += OnEnemyDefeated;
    }

    private void OnDisable()
    {
        if (gridBehaviour != null)
            gridBehaviour.OnGridFulfilled -= OnEnemyDefeated;
    }

    private void OnEnemyDefeated()
    {
        Debug.Log($"[EnemyManager] Enemy '{gameObject.name}' defeated ¡ª all grid cells filled.");
        Destroy(gameObject);
    }
}
