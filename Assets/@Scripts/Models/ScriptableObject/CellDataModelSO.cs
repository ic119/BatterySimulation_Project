using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "CellDataModelSO", menuName = "ScriptableObjectAssets/CellDataModel")]
public class CellDataModelSO : ScriptableObject
{
    public List<CellDataModel> cellDataModels;
}