using UnityEngine;

public class FlightRoutePlanningPanelOwner : MonoBehaviour, IGeneralPanelOwner
{
    [SerializeField] private FlightMapPanelUI mapPanelUI;

    public bool IsRoutePlanningOpen { get; private set; }

    public void OnPanelOpenRequested()
    {
        IsRoutePlanningOpen = true;
        mapPanelUI?.EnterRoutePlanningMode();
    }

    public void OnPanelCloseRequested()
    {
        IsRoutePlanningOpen = false;
        mapPanelUI?.ExitRoutePlanningMode();
    }
}
