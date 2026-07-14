using System;
using UnityEngine;

[Serializable]
public class FlightDirectionMapper
{
    [Tooltip("Optional yaw-only visual basis. Leave empty to use world axes directly.")]
    [SerializeField] private Transform directionSpace;
    [SerializeField] private float yawOffset;
    [SerializeField] private bool invertMapX;
    [SerializeField] private bool invertMapY;

    public Vector3 GetWorldForward(Vector2Int from, Vector2Int to)
    {
        float mapX = to.x - from.x;
        float mapY = to.y - from.y;

        if (invertMapX)
            mapX = -mapX;

        if (invertMapY)
            mapY = -mapY;

        // Map up maps to +X, while map left maps to +Z.
        Vector3 localDirection = new Vector3(mapY, 0f, -mapX);
        if (localDirection.sqrMagnitude <= Mathf.Epsilon)
            return Vector3.forward;

        Quaternion yawRotation = Quaternion.Euler(
            0f,
            yawOffset + (directionSpace != null ? directionSpace.eulerAngles.y : 0f),
            0f);

        return (yawRotation * localDirection).normalized;
    }

    public Vector3 GetWorldRight(Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        return right.sqrMagnitude > Mathf.Epsilon ? right.normalized : Vector3.right;
    }
}
