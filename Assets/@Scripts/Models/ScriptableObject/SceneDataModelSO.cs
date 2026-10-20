using System.Collections.Generic;
using UnityEngine;

namespace Incheol.Models.SO
{
    [CreateAssetMenu(fileName = "SceneDataModelSO", menuName = "ScriptableObjectAssets/SceneDataModel")]
    public class SceneDataModelSO : ScriptableObject
    {
        public List<SceneDataModel> sceneDataModels;
    }
}