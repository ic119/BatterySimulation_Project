using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "ParallelAssemblyDataModelSO", menuName = "ScriptableObjectAssets/ModuleAssemblyDataModel")]
public class ModuleAssemblyDataModelSO : ScriptableObject
{
    public List<ModuleAssemblyDataModel> moduleAssemblyDataModels;
}