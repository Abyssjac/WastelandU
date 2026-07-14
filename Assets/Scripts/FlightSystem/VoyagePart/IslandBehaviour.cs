using UnityEngine;

public class IslandBehaviour : MonoBehaviour
{
    [SerializeField] private Transform approachAnchor;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Vector3 initialLocalScale;
    private bool hasInitialTransform;

    public Transform ApproachAnchor => approachAnchor != null ? approachAnchor : transform;

    private void Awake()
    {
        CacheInitialTransform();
    }

    public void PrepareForSpawn()
    {
        CacheInitialTransform();
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialLocalScale;
    }

    public void AlignApproachOutward(Vector3 desiredOutward)
    {
        Vector3 desired = Vector3.ProjectOnPlane(desiredOutward, Vector3.up);
        Vector3 current = Vector3.ProjectOnPlane(ApproachAnchor.forward, Vector3.up);
        if (desired.sqrMagnitude <= Mathf.Epsilon || current.sqrMagnitude <= Mathf.Epsilon)
            return;

        Quaternion rotationDelta = Quaternion.FromToRotation(current.normalized, desired.normalized);
        transform.rotation = rotationDelta * transform.rotation;
    }

    public void SetApproachAnchorPosition(Vector3 targetPosition)
    {
        transform.position += targetPosition - ApproachAnchor.position;
    }

    public void Move(Vector3 displacement)
    {
        transform.position += displacement;
    }

    public bool IsBeyondRecycleRadius(Vector3 shipAnchorPosition, float recycleRadius)
    {
        Vector3 offset = transform.position - shipAnchorPosition;
        offset.y = 0f;
        return offset.sqrMagnitude > recycleRadius * recycleRadius;
    }

    public void ResetForPool()
    {
        CacheInitialTransform();
        transform.localPosition = initialLocalPosition;
        transform.localRotation = initialLocalRotation;
        transform.localScale = initialLocalScale;
    }

    private void CacheInitialTransform()
    {
        if (hasInitialTransform)
            return;

        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
        initialLocalScale = transform.localScale;
        hasInitialTransform = true;
    }
}
