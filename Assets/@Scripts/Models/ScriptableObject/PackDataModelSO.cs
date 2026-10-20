using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "PackDataModelSO", menuName = "ScriptableObjectAssets/PackDataModel")]
public class PackDataModelSO : ScriptableObject
{
    public List<PackDataModel> packDataModels;
}