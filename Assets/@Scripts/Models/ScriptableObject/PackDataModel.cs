using System;
using System.Collections.Generic;

[Serializable]
public class PackDataModel : IDeviceNode
{
    public string packName;
    public List<ModuleDataModel> modulesList;

    public Incheol.Models.Define.DeviceType Kind => Incheol.Models.Define.DeviceType.Pack;
    public string Name { get => packName; set => packName = value; }

    /// <summary>이 Pack을 담은 Container. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public ContainerDataModel parentContainer;

    /// <summary>Container의 Pack 격자(x * z)에서 이 Pack이 놓인 열(x 방향)과 줄(z 방향). 칸을 기록한 적 없으면 -1.</summary>
    [NonSerialized] public int slotColumn = -1;
    [NonSerialized] public int slotRow = -1;
}