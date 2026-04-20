using UnityEngine;
using UnityEngine;
using JackyPuzzleInteract;

/// <summary>
/// 区域触发行为：当带有指定 Tag 的物体进入触发区域时，调用关联的 SingleSignalInteractable 发送信号。
/// </summary>
public class SingleSignalRegionBehaviour : MonoBehaviour
{
    [SerializeField] private SingleSignalInteractable linkedInteractable;

    [Header("Tag Filter")]
    [Tooltip("允许触发的 Tag 列表，为空则任何物体都能触发")]
    [SerializeField] private string[] acceptedTags = new string[] { "Player" };

    private void Awake()
    {
        if (linkedInteractable == null)
        {
            linkedInteractable = GetComponentInParent<SingleSignalInteractable>();
        }
        if (linkedInteractable == null)
        {
            Debug.LogError($"[{name}] SingleSignalRegionBehaviour: 无法找到 SingleSignalInteractable！", this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (linkedInteractable == null) return;
        if (!IsAcceptedTag(other.gameObject)) return;

        linkedInteractable.SendSingleSignal();
    }

    private bool IsAcceptedTag(GameObject obj)
    {
        if (acceptedTags == null || acceptedTags.Length == 0) return true;

        for (int i = 0; i < acceptedTags.Length; i++)
        {
            if (obj.CompareTag(acceptedTags[i]))
                return true;
        }
        return false;
    }

#if UNITY_EDITOR
    [Header("Debug Gizmo")]
    [SerializeField] private bool debugEnable = false;
    [SerializeField] private GameObject gizmoTarget;
    [SerializeField] private Color gizmoColor = new Color(0f, 1f, 0.5f, 0.25f);

    private void OnDrawGizmos()
    {
        if (!debugEnable || gizmoTarget == null) return;

        var colliders = gizmoTarget.GetComponents<BoxCollider>();
        if (colliders == null || colliders.Length == 0) return;

        Color oldColor = Gizmos.color;
        Matrix4x4 oldMatrix = Gizmos.matrix;

        Gizmos.matrix = gizmoTarget.transform.localToWorldMatrix;

        for (int i = 0; i < colliders.Length; i++)
        {
            var col = colliders[i];
            Gizmos.color = gizmoColor;
            Gizmos.DrawCube(col.center, col.size);
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.DrawWireCube(col.center, col.size);
        }

        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
    }
#endif
}