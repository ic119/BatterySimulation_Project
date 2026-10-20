using Incheol.Models.Define;
using Incheol.Models.SO;
using Incheol.Utils;
using System;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Incheol.Modules
{
    /// <summary>
    /// SceneDataModelSO / AddressableAssetModelSO를 Addressable로 로드해 씬 태그별로 조회할 수 있게 들고 있다.
    /// 로드 결과가 비었거나 유효한 tags가 하나도 없으면 실패로 본다.
    /// </summary>
    internal class SceneDataRegistry
    {
        private const string SceneDataScriptableObjectName = "SceneDataModelSO";
        private const string AddressableAssetScriptableObjectName = "AddressableAssetModelSO";

        private Dictionary<string, SceneDataModel> sceneDataModelDictionary;
        private Dictionary<string, AddressableAssetModel> addressableAssetModelDictionary;

        /// <summary>두 테이블이 모두 로드돼 있는지.</summary>
        public bool IsLoaded => sceneDataModelDictionary != null && addressableAssetModelDictionary != null;

        /// <summary>
        /// 두 ScriptableObject를 차례로 로드한다. 완료 시 onComplete(true/false)를 반드시 호출한다.
        /// </summary>
        public void Load(Action<bool> onComplete)
        {
            LoadSceneDataModelSO(sceneSuccess =>
            {
                if (!sceneSuccess)
                {
                    onComplete?.Invoke(false);
                    return;
                }

                LoadAddressableAssetModelSO(onComplete);
            });
        }

        public bool TryGetScene(string tag, out SceneDataModel model)
        {
            model = null;
            return sceneDataModelDictionary != null && sceneDataModelDictionary.TryGetValue(tag, out model);
        }

        /// <summary>등록된 씬 tags(오류 로그용).</summary>
        public string DescribeSceneTags()
        {
            return sceneDataModelDictionary == null ? string.Empty : string.Join(", ", sceneDataModelDictionary.Keys);
        }

        /// <summary>
        /// AddressableAssetModelSO에서 씬 태그에 매칭되는 preload Key 목록을 가져온다.
        /// </summary>
        public List<string> CollectPreloadKeyStrings(string tagName)
        {
            List<string> keyStrings = new List<string>();

            if (addressableAssetModelDictionary == null || string.IsNullOrEmpty(tagName))
            {
                return keyStrings;
            }

            if (!addressableAssetModelDictionary.TryGetValue(tagName, out AddressableAssetModel model) || model == null)
            {
                DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"AddressableAssetModelSO에 tag '{tagName}'에 대한 preload 설정이 없습니다.");
                return keyStrings;
            }

            List<AddressableAssetKey> preloadKeys = model.preloadAddressableKeys;
            if (preloadKeys == null)
            {
                return keyStrings;
            }

            for (int i = 0; i < preloadKeys.Count; i++)
            {
                AddressableAssetKey key = preloadKeys[i];
                if (key == AddressableAssetKey.None)
                {
                    continue;
                }

                keyStrings.Add(key.ToString());
            }

            return keyStrings;
        }

        private void LoadSceneDataModelSO(Action<bool> onComplete)
        {
            AsyncOperationHandle<SceneDataModelSO> handle;

            try
            {
                handle = Addressables.LoadAssetAsync<SceneDataModelSO>(SceneDataScriptableObjectName);
            }
            catch (Exception exception)
            {
                DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"SceneDataModelSO 로드 실패(잘못된 Key) Key : {SceneDataScriptableObjectName}, Exception : {exception}");
                onComplete?.Invoke(false);
                return;
            }

            handle.Completed += result =>
            {
                if (result.Status != AsyncOperationStatus.Succeeded || result.Result == null)
                {
                    sceneDataModelDictionary = null;

                    DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"SceneDataModelSO 로드 실패(Addressables Status : {result.Status}) Key : {SceneDataScriptableObjectName}");
                    onComplete?.Invoke(false);
                    return;
                }

                sceneDataModelDictionary = new Dictionary<string, SceneDataModel>();
                List<SceneDataModel> models = result.Result.sceneDataModels;

                if (models == null || models.Count == 0)
                {
                    DebugLogManager.GenerateErrorMessage<SceneLoadManager>("SceneDataModelSO의 sceneDataModels 리스트가 비어 있습니다.");
                    onComplete?.Invoke(false);
                    return;
                }

                for (int i = 0; i < models.Count; i++)
                {
                    SceneDataModel model = models[i];
                    if (model == null || string.IsNullOrEmpty(model.tags))
                    {
                        continue;
                    }

                    if (!sceneDataModelDictionary.ContainsKey(model.tags))
                    {
                        sceneDataModelDictionary.Add(model.tags, model);
                    }
                }

                if (sceneDataModelDictionary.Count == 0)
                {
                    DebugLogManager.GenerateErrorMessage<SceneLoadManager>("SceneDataModelSO의 모든 항목이 유효하지 않은 tags를 갖고 있습니다.");
                    onComplete?.Invoke(false);
                    return;
                }

                onComplete?.Invoke(true);
            };
        }

        private void LoadAddressableAssetModelSO(Action<bool> onComplete)
        {
            AsyncOperationHandle<AddressableAssetModelSO> handle;

            try
            {
                handle = Addressables.LoadAssetAsync<AddressableAssetModelSO>(AddressableAssetScriptableObjectName);
            }
            catch (Exception exception)
            {
                DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"AddressableAssetModelSO 로드 실패(잘못된 Key) Key : {AddressableAssetScriptableObjectName}, Exception : {exception}");
                onComplete?.Invoke(false);
                return;
            }

            handle.Completed += result =>
            {
                if (result.Status != AsyncOperationStatus.Succeeded || result.Result == null)
                {
                    addressableAssetModelDictionary = null;

                    DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"AddressableAssetModelSO 로드 실패(Addressables Status : {result.Status}) Key : {AddressableAssetScriptableObjectName}");
                    onComplete?.Invoke(false);
                    return;
                }

                addressableAssetModelDictionary = new Dictionary<string, AddressableAssetModel>();
                List<AddressableAssetModel> models = result.Result.addressableAssetModels;

                if (models == null || models.Count == 0)
                {
                    DebugLogManager.GenerateErrorMessage<SceneLoadManager>("AddressableAssetModelSO의 addressableAssetModels 리스트가 비어 있습니다.");
                    onComplete?.Invoke(false);
                    return;
                }

                for (int i = 0; i < models.Count; i++)
                {
                    AddressableAssetModel model = models[i];
                    if (model == null || string.IsNullOrEmpty(model.tags))
                    {
                        continue;
                    }

                    if (!addressableAssetModelDictionary.ContainsKey(model.tags))
                    {
                        addressableAssetModelDictionary.Add(model.tags, model);
                    }
                }

                if (addressableAssetModelDictionary.Count == 0)
                {
                    DebugLogManager.GenerateErrorMessage<SceneLoadManager>("AddressableAssetModelSO의 모든 항목이 유효하지 않은 tags를 갖고 있습니다.");
                    onComplete?.Invoke(false);
                    return;
                }

                onComplete?.Invoke(true);
            };
        }
    }
}
