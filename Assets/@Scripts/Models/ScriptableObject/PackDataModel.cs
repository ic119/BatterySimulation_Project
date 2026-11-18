using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PackDataModel : IDeviceNode
{
    public string packName;
    public DeviceNodeValues values = new DeviceNodeValues();
    public List<ModuleAssemblyDataModel> moduleAssembliesList = new List<ModuleAssemblyDataModel>();

    /// <summary>이 Pack의 씬 오브젝트. Root 바로 아래에 놓인다. 저장하지 않는다.</summary>
    [NonSerialized] public Transform packPosition;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.pack;
    public string Name { get => packName; set => packName = value; }
    public Transform Position => packPosition;
    public DeviceNodeValues Values => values;
    public IDeviceNode Parent => null;
    public IReadOnlyList<IDeviceNode> Children => moduleAssembliesList.ConvertAll(_item => (IDeviceNode)_item);
}
