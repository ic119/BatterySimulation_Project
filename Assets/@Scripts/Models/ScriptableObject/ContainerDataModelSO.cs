using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ContainerDataModelSO", menuName = "ScriptableObjectAssets/ContainerDataModel")]
public class ContainerDataModelSO : ScriptableObject
{
    public List<ContainerDataModel> containerDataModels;
}