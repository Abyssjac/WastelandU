using UnityEngine;

[System.Serializable]
public class FlightTimeController
{
    [SerializeField, Min(1)] private int timePerTravelUnit = 1;

    private float elapsedTimeUnits;
    private int totalTimeUnits;
    private bool isRunning;

    public FlightTimeController()
    {
    }

    public FlightTimeController(int timePerTravelUnit)
    {
        this.timePerTravelUnit = Mathf.Max(1, timePerTravelUnit);
    }

    public int TimePerTravelUnit
    {
        get => timePerTravelUnit;
        set => timePerTravelUnit = Mathf.Max(1, value);
    }

    public int ElapsedTimeUnits => Mathf.FloorToInt(elapsedTimeUnits);
    public float ElapsedTimeUnitsFloat => elapsedTimeUnits;
    public int TotalTimeUnits => totalTimeUnits;
    public bool IsRunning => isRunning;
    public bool IsFinished => isRunning && elapsedTimeUnits >= totalTimeUnits;
    public float Progress01 => totalTimeUnits > 0
        ? Mathf.Clamp01((float)elapsedTimeUnits / totalTimeUnits)
        : 0f;

    public int CalculateTimeUnits(float travelDistance)
    {
        return Mathf.CeilToInt(Mathf.Max(0f, travelDistance) * timePerTravelUnit);
    }

    public void StartSegment(float travelDistance, float progress01 = 0f)
    {
        totalTimeUnits = Mathf.Max(0, CalculateTimeUnits(travelDistance));
        elapsedTimeUnits = totalTimeUnits * Mathf.Clamp01(progress01);
        isRunning = totalTimeUnits > 0 && elapsedTimeUnits < totalTimeUnits;
    }

    public void Advance(float timeUnits)
    {
        if (!isRunning)
            return;

        elapsedTimeUnits = Mathf.Clamp(
            elapsedTimeUnits + Mathf.Max(0, timeUnits),
            0,
            totalTimeUnits);
    }

    public void Complete()
    {
        if (totalTimeUnits <= 0)
            return;

        elapsedTimeUnits = totalTimeUnits;
    }

    public void Reset()
    {
        elapsedTimeUnits = 0;
        totalTimeUnits = 0;
        isRunning = false;
    }

    public void Stop()
    {
        isRunning = false;
    }
}
