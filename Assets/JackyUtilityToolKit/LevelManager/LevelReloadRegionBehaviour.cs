using UnityEngine;
using UnityEngine;
using JackyUtility;

/// <summary>
/// Triggers a full scene reload when an object with a matching tag
/// enters the linked BoxCollider region.
/// </summary>
public class LevelReloadRegionBehaviour : MonoBehaviour
{
    [Header("Region")]
    [SerializeField] private string regionName = "ReloadRegion";
    [SerializeField] private BoxCollider triggerCollider;

    [Header("Tag Filter")]
    [SerializeField] private string[] acceptedTags = new string[] { "Player" };

    [Header("Debug")]
    [SerializeField] private bool enableDebug = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!IsAcceptedTag(other.gameObject)) return;

        if (enableDebug)
            Debug.Log($"[LevelReloadRegion] '{regionName}' triggered by '{other.name}'. Reloading scene.", this);

        if (AllLevelManager.Instance == null)
        {
            Debug.LogError($"[LevelReloadRegion] '{regionName}': AllLevelManager.Instance is null. Cannot reload.", this);
            return;
        }

        AllLevelManager.Instance.ReloadCurrentScene();
    }

    private bool IsAcceptedTag(GameObject go)
    {
        if (acceptedTags == null || acceptedTags.Length == 0) return true;

        for (int i = 0; i < acceptedTags.Length; i++)
        {
            if (go.CompareTag(acceptedTags[i]))
                return true;
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!enableDebug) return;

        BoxCollider col = triggerCollider != null ? triggerCollider : GetComponent<BoxCollider>();
        if (col != null)
        {
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = col.transform.localToWorldMatrix;

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.2f);
            Gizmos.DrawCube(col.center, col.size);

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(col.center, col.size);

            Gizmos.matrix = oldMatrix;
        }

        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(transform.position, regionName);
    }
#endif
}
