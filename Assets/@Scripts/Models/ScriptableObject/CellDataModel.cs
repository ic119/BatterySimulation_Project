using System;
using UnityEngine;

[Serializable]
public class CellDataModel
{
    public string cellName;
    public Transform cellPosition;
    public ModuleDataModel parentModule;
}