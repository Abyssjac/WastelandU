using UnityEngine;

[CreateAssetMenu(menuName = "LoadoutMVP/CarriageData", fileName = "CD_")]
public class CarriageData : ScriptableObject
{
    [Header("Identity")]
    public string id;
    public string displayName;

    [Header("Prefab")]
    public GameObject prefab; // CarriageView prefab
}