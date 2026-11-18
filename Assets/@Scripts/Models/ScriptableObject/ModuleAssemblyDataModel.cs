using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ModuleAssemblyDataModel : IDeviceNode
{
    public string moduleAssemblyName;
    public List<ModuleDataModel> modulesList = new List<ModuleDataModel>();
    public Transform moduleAssemblyPosition;
    public DeviceNodeValues values = new DeviceNodeValues();

    /// <summary>이 ModuleAssembly가 속한 Pack. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public PackDataModel parentPack;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.moduleassembly;
    public string Name { get => moduleAssemblyName; set => moduleAssemblyName = value; }
    public Transform Position => moduleAssemblyPosition;
    public DeviceNodeValues Values => values;
    public IDeviceNode Parent => parentPack;
    public IReadOnlyList<IDeviceNode> Children => modulesList.ConvertAll(_item => (IDeviceNode)_item);
}
