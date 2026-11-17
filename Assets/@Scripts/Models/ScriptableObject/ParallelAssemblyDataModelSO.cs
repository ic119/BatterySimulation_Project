using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "ParallelAssemblyDataModelSO", menuName = "ScriptableObjectAssets/ParallelAssemblyDataModel")]
public class ParallelAssemblyDataModelSO : ScriptableObject
{
    public List<ParallelAssemblyDataModel> parallelAssemblyDataModels;
}