using System;
using UnityEngine;

[Serializable]
public class CellDataModel : IDeviceNode
{
    public string cellName;
    public Transform cellPosition;
    public ModuleDataModel parentModule;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.Cell;
    public string Name { get => cellName; set => cellName = value; }
}