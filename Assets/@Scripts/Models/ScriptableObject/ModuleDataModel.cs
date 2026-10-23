using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class ModuleDataModel : IDeviceNode
{
    public string moduleName;
    public List<CellDataModel> cellsList;
    public Transform modulePosition;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.Module;
    public string Name { get => moduleName; set => moduleName = value; }

    /// <summary>이 Module이 속한 Pack. Pack 없이 단독으로 만든 Module이면 null. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public PackDataModel parentPack;

    /// <summary>Pack 없이 단독으로 만든 Module을 담은 Container. Pack에 속한 Module이면 null. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public ContainerDataModel parentContainer;
}