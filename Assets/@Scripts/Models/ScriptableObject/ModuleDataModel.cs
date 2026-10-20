using System;
using System.Collections.Generic;
using UnityEngine;


[Serializable]
public class ModuleDataModel
{
    public string moduleName;
    public List<CellDataModel> cellsList;
    public Transform modulePosition;

    /// <summary>이 Module이 속한 Pack. Pack 없이 단독으로 만든 Module이면 null. 순환 참조라 직렬화하지 않는다.</summary>
    [NonSerialized] public PackDataModel parentPack;
}