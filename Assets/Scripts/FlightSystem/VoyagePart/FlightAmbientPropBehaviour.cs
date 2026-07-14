using UnityEngine;

public class FlightAmbientPropBehaviour : MonoBehaviour
{
    [Header("Base Motion")]
    [SerializeField] private Vector2 localDriftSpeedRange = new Vector2(0f, 0.25f);
    [SerializeField] private Vector2 rotationSpeedRange = new Vector2(-4f, 4f);

    private Vector3 localDriftVelocity;
    private float rotationSpeed;
    private bool isSpawned;

    public bool IsSpawned => isSpawned;

    public void Spawn(FlightAmbientSpawnData spawnData)
    {
        transform.SetPositionAndRotation(spawnData.Position, spawnData.Rotation);
        transform.localScale = spawnData.Scale;

        System.Random random = new System.Random(spawnData.RandomSeed);
        localDriftVelocity = new Vector3(
            RandomRange(random, -1f, 1f),
            RandomRange(random, -0.25f, 0.25f),
            RandomRange(random, -1f, 1f)).normalized * RandomRange(random, localDriftSpeedRange.x, localDriftSpeedRange.y);
        rotationSpeed = RandomRange(random, rotationSpeedRange.x, rotationSpeedRange.y);
        isSpawned = true;

        OnSpawned(spawnData);
    }

    public void Simulate(FlightVisualFrameContext frameContext)
    {
        if (!isSpawned)
            return;

        Vector3 displacement = frameContext.FlowDirection * frameContext.ScrollDelta;
        displacement += localDriftVelocity * frameContext.DeltaTime;
        displacement += GetAdditionalDisplacement(frameContext);
        transform.position += displacement;

        if (!Mathf.Approximately(rotationSpeed, 0f))
            transform.Rotate(Vector3.up, rotationSpeed * frameContext.DeltaTime, Space.Self);

        OnSimulated(frameContext);
    }

    public bool IsBeyondRecycleRadius(Vector3 shipAnchorPosition, float recycleRadius)
    {
        Vector3 offset = transform.position - shipAnchorPosition;
        offset.y = 0f;
        return offset.sqrMagnitude > recycleRadius * recycleRadius;
    }

    public void ResetForPool()
    {
        if (!isSpawned)
            return;

        OnBeforeReturnToPool();
        localDriftVelocity = Vector3.zero;
        rotationSpeed = 0f;
        isSpawned = false;
    }

    protected virtual void OnSpawned(FlightAmbientSpawnData spawnData)
    {
    }

    protected virtual Vector3 GetAdditionalDisplacement(FlightVisualFrameContext frameContext)
    {
        return Vector3.zero;
    }

    protected virtual void OnSimulated(FlightVisualFrameContext frameContext)
    {
    }

    protected virtual void OnBeforeReturnToPool()
    {
    }

    private static float RandomRange(System.Random random, float min, float max)
    {
        return Mathf.Lerp(min, max, (float)random.NextDouble());
    }
}
