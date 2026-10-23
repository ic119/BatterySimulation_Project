using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ContainerDataModel : IDeviceNode
{
    public string containerName;
    public List<PackDataModel> packsList;
    public List<ModuleDataModel> modulesList;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.Container;
    public string Name { get => containerName; set => containerName = value; }

    /// <summary>이 Container의 씬 오브젝트(빈 GameObject). Pack과 단독 Module이 이 아래에 놓인다. 저장하지 않는다.</summary>
    [NonSerialized] public Transform containerPosition;
}
