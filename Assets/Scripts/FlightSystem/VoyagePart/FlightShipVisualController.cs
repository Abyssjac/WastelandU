using UnityEngine;

public class FlightShipVisualController : MonoBehaviour
{
    [SerializeField] private Transform shipModel;
    [SerializeField, Min(0f)] private float turnSpeedDegreesPerSecond = 540f;
    [SerializeField] private Vector3 modelRotationOffset;
    [SerializeField, Min(0f)] private float bobAmplitude = 0.25f;
    [SerializeField, Min(0f)] private float bobFrequency = 1.1f;

    private Vector3 initialLocalPosition;
    private bool hasInitialLocalPosition;
    private float bobTime;

    private Transform ShipModel => shipModel != null ? shipModel : transform;

    private void Awake()
    {
        initialLocalPosition = ShipModel.localPosition;
        hasInitialLocalPosition = true;
    }

    public void Tick(FlightVisualFrameContext frameContext)
    {
        Transform model = ShipModel;
        if (frameContext.Forward.sqrMagnitude > Mathf.Epsilon)
        {
            Quaternion targetRotation = Quaternion.LookRotation(frameContext.Forward, Vector3.up)
                * Quaternion.Euler(modelRotationOffset);
            model.rotation = Quaternion.RotateTowards(
                model.rotation,
                targetRotation,
                turnSpeedDegreesPerSecond * frameContext.DeltaTime);
        }

        if (!hasInitialLocalPosition)
        {
            initialLocalPosition = model.localPosition;
            hasInitialLocalPosition = true;
        }

        bobTime += frameContext.DeltaTime * bobFrequency;
        Vector3 localPosition = initialLocalPosition;
        localPosition.y += Mathf.Sin(bobTime) * bobAmplitude;
        model.localPosition = localPosition;
    }
}
