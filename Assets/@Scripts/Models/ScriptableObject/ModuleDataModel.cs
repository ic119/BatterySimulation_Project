using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ModuleDataModel : IDeviceNode
{
    public string moduleName;
    public List<ParallelAssemblyDataModel> parallelAssembliesList = new List<ParallelAssemblyDataModel>();
    public Transform modulePosition;
    public DeviceNodeValues values = new DeviceNodeValues();

    /// <summary>이 Module이 속한 ModuleAssembly. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public ModuleAssemblyDataModel parentModuleAssembly;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.module;
    public string Name { get => moduleName; set => moduleName = value; }
    public Transform Position => modulePosition;
    public DeviceNodeValues Values => values;
    public IDeviceNode Parent => parentModuleAssembly;
    public IReadOnlyList<IDeviceNode> Children => parallelAssembliesList.ConvertAll(_item => (IDeviceNode)_item);
}
