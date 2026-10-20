using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ModuleDataModelSO", menuName = "ScriptableObjectAssets/ModuleDataModel")]
public class ModuleDataModelSO : ScriptableObject
{
    public List<ModuleDataModel> moduleDataModels;
}