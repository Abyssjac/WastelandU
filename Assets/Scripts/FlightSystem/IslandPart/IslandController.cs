using UnityEngine;

[DisallowMultipleComponent]
public class IslandController : MonoBehaviour
{
    [Header("Anchors")]
    [SerializeField] private Transform playerLandingAnchor;
    [SerializeField] private Transform shipDockAnchor;

    public Transform PlayerLandingAnchor => playerLandingAnchor;
    public Transform ShipDockAnchor => shipDockAnchor;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawAnchor(playerLandingAnchor, Color.cyan, "Player Landing");
        DrawAnchor(shipDockAnchor, new Color(1f, 0.75f, 0.15f), "Ship Dock");
    }

    private static void DrawAnchor(Transform anchor, Color color, string label)
    {
        if (anchor == null)
            return;

        Gizmos.color = color;
        Gizmos.DrawWireSphere(anchor.position, 0.6f);
        Gizmos.DrawLine(anchor.position, anchor.position + anchor.forward * 2f);
        UnityEditor.Handles.Label(anchor.position + Vector3.up * 0.8f, label);
    }
#endif
}
