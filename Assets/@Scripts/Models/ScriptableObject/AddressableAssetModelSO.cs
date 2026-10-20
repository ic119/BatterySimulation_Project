using System;
using System.Collections.Generic;
using UnityEngine;
using Incheol.Models.Define;
using Incheol.Utils;

namespace Incheol.Models.SO
{
    [CreateAssetMenu(fileName = "AddressableAssetModelSO", menuName = "ScriptableObjectAssets/AddressableAssetModel")]
    public class AddressableAssetModelSO : ScriptableObject
    {
        public List<AddressableAssetModel> addressableAssetModels;

#if UNITY_EDITOR
        // 유효하지 않은(enum에 정의되지 않은) 값이 Inspector에 직접 입력되는 경우를 에디터 타임에 잡아낸다.
        // 런타임까지 가면 Addressables.LoadAssetAsync에서 InvalidKeyException으로만 드러나 원인 파악이 어렵다.
        private void OnValidate()
        {
            if (addressableAssetModels == null)
            {
                return;
            }

            foreach (var model in addressableAssetModels)
            {
                if (model?.preloadAddressableKeys == null)
                {
                    continue;
                }

                foreach (var key in model.preloadAddressableKeys)
                {
                    if (!Enum.IsDefined(typeof(AddressableAssetKey), key))
                    {
                        DebugLogManager.GenerateErrorMessage<AddressableAssetModelSO>(
                            $"tag '{model.tags}'의 preloadAddressableKeys에 정의되지 않은 AddressableAssetKey 값이 있습니다 : {(int)key}");
                    }
                }
            }
        }
#endif
    }
}