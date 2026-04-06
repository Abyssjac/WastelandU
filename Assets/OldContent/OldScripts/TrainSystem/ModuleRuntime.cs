using System;
using UnityEngine;

[Serializable]
public class ModuleRuntime
{
    public string moduleInstanceId;
    public string moduleDataId;
    public int slotIndex;

    public bool IsEmpty => string.IsNullOrEmpty(moduleDataId);
}

[Serializable]
public class CarriageRuntime
{
    public string carriageInstanceId;
    public string carriageDataId;

    // null = empty
    public ModuleRuntime[] modulesBySlot;

    public CarriageRuntime(int slotCount)
    {
        modulesBySlot = new ModuleRuntime[slotCount];
    }
}
