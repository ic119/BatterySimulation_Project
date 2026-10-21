using System;
using System.Collections.Generic;

[Serializable]
public class PackDataModel
{
    public string packName;
    public List<ModuleDataModel> modulesList;

    /// <summary>이 Pack을 담은 Container. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public ContainerDataModel parentContainer;
}