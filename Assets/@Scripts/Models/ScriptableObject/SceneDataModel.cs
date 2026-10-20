using System;
using System.Collections.Generic;

namespace Incheol.Models.SO
{
    [Serializable]
    public class SceneDataModel
    {
        public string tags;
        public List<string> loadedSceneList;
        public string activeSceneName;
    }
}