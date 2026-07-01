using UnityEngine;

[System.Serializable]
public class FlightTimeCalculator
{
    [SerializeField, Min(1)] private int timePerGrid = 1;

    public FlightTimeCalculator()
    {
    }

    public FlightTimeCalculator(int timePerGrid)
    {
        this.timePerGrid = Mathf.Max(1, timePerGrid);
    }

    public int TimePerGrid
    {
        get => timePerGrid;
        set => timePerGrid = Mathf.Max(1, value);
    }

    public int GetManhattanDistance(Vector2Int from, Vector2Int to)
    {
        return Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
    }

    public int CalculateTimeUnits(Vector2Int from, Vector2Int to)
    {
        return GetManhattanDistance(from, to) * timePerGrid;
    }
}
