using UnityEngine;

public class FlightCloudPropBehaviour : FlightAmbientPropBehaviour
{
    [Header("Cloud Motion")]
    [SerializeField, Min(0f)] private float verticalBobAmplitude = 0.15f;
    [SerializeField, Min(0f)] private float verticalBobFrequency = 0.8f;

    private float bobPhase;

    protected override void OnSpawned(FlightAmbientSpawnData spawnData)
    {
        bobPhase = spawnData.RandomSeed * 0.017f;
    }

    protected override Vector3 GetAdditionalDisplacement(FlightVisualFrameContext frameContext)
    {
        if (verticalBobAmplitude <= 0f || verticalBobFrequency <= 0f)
            return Vector3.zero;

        float previousHeight = Mathf.Sin(bobPhase) * verticalBobAmplitude;
        bobPhase += frameContext.DeltaTime * verticalBobFrequency;
        float nextHeight = Mathf.Sin(bobPhase) * verticalBobAmplitude;
        return Vector3.up * (nextHeight - previousHeight);
    }

    protected override void OnBeforeReturnToPool()
    {
        bobPhase = 0f;
    }
}
