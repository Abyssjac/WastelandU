using UnityEngine;

[System.Serializable]
public class FlightTimeController
{
    [SerializeField, Min(1)] private int timePerGrid = 1;

    private float elapsedTimeUnits;
    private int totalTimeUnits;
    private bool isRunning;

    public FlightTimeController()
    {
    }

    public FlightTimeController(int timePerGrid)
    {
        this.timePerGrid = Mathf.Max(1, timePerGrid);
    }

    public int TimePerGrid
    {
        get => timePerGrid;
        set => timePerGrid = Mathf.Max(1, value);
    }

    public int ElapsedTimeUnits => Mathf.FloorToInt(elapsedTimeUnits);
    public float ElapsedTimeUnitsFloat => elapsedTimeUnits;
    public int TotalTimeUnits => totalTimeUnits;
    public bool IsRunning => isRunning;
    public bool IsFinished => isRunning && elapsedTimeUnits >= totalTimeUnits;
    public float Progress01 => totalTimeUnits > 0
        ? Mathf.Clamp01((float)elapsedTimeUnits / totalTimeUnits)
        : 0f;

    public float GetEuclideanDistance(Vector2Int from, Vector2Int to)
    {
        return Vector2Int.Distance(from, to);
    }

    public int CalculateTimeUnits(Vector2Int from, Vector2Int to)
    {
        return Mathf.CeilToInt(GetEuclideanDistance(from, to) * timePerGrid);
    }

    public void StartSegment(Vector2Int from, Vector2Int to)
    {
        totalTimeUnits = Mathf.Max(0, CalculateTimeUnits(from, to));
        elapsedTimeUnits = 0;
        isRunning = totalTimeUnits > 0;
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
