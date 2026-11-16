using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class CellDataModel : IDeviceNode
{
    public string cellName;
    public Transform cellPosition;

    /// <summary>이 Cell이 속한 ParallelAssembly. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public ParallelAssemblyDataModel parentParallelAssembly;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.Cell;
    public string Name { get => cellName; set => cellName = value; }
    public Transform Position => cellPosition;
    public IDeviceNode Parent => parentParallelAssembly;
    public IReadOnlyList<IDeviceNode> Children => Array.Empty<IDeviceNode>();
}
