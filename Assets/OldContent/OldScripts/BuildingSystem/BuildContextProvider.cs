using System;
using UnityEngine;

public class BuildContextProvider : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}

public struct BuildContext
{
    public Vector3 BuildPosition;
    public Quaternion BuildRotation;
    public ModuleData ModuleToBuild;
}

[Serializable]
public struct BuildFootPrint
{
    public Vector3Int Pivot; // 相对于建造中心的枢轴点坐标
    public Vector3Int[] OccupiedCells; // 相对于建造中心的占用格子坐标
    public int BuildLayer; // 建造所在层级（0-Weapon, 1-Utility, 2-Defense）    
    public bool allowRotation; // 是否允许旋转建造
}