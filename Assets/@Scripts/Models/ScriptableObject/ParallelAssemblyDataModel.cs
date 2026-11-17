using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ParallelAssemblyDataModel : IDeviceNode
{
    public string parallelAssemblyName;
    public List<CellDataModel> cellsList = new List<CellDataModel>();
    public Transform parallelAssemblyPosition;
    public DeviceNodeValues values = new DeviceNodeValues();

    /// <summary>이 ParallelAssembly가 속한 Module. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public ModuleDataModel parentModule;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.ParallelAssembly;
    public string Name { get => parallelAssemblyName; set => parallelAssemblyName = value; }
    public Transform Position => parallelAssemblyPosition;
    public DeviceNodeValues Values => values;
    public IDeviceNode Parent => parentModule;
    public IReadOnlyList<IDeviceNode> Children => cellsList.ConvertAll(_item => (IDeviceNode)_item);
}
