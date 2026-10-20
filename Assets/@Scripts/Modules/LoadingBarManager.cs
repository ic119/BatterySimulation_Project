using Incheol.Models.Define;
using Incheol.Utils;
using Incheol.View.UI;
using System;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// UI_LoadingBarView를 Addressable로 한 번 로드해 인스턴스화해 두고, 어느 씬에서든 Show/Hide/진행률 갱신을 할 수 있게 한다.
    /// 씬 전환 중에도 유지되어야 하므로 이 매니저(와 자식인 로딩바 인스턴스)는 파괴되지 않는다.
    /// </summary>
    public class LoadingBarManager : SingletonObject<LoadingBarManager>
    {
        protected override bool PersistAcrossScenes => true;

        /// <summary>
        /// 씬 전환(ReleaseAllHandler)에서 해제되지 않도록 지켜야 하는 Addressable Key.
        /// 인스턴스가 참조하는 폰트/머티리얼 등이 에셋 해제로 깨지지 않게 한다.
        /// </summary>
        public static readonly string[] PreservedKeys = { nameof(AddressableAssetKey.UI_LoadingBarView) };

        private UI_LoadingBarView loadingBarView;

        public bool IsInitialized => loadingBarView != null;
        public bool IsShowing => loadingBarView != null && loadingBarView.gameObject.activeSelf;

        /// <summary>
        /// 로딩바 프리팹을 로드해 숨겨진 상태로 인스턴스화한다. 완료 시 _onComplete(true/false)를 반드시 호출한다.
        /// </summary>
        public void Init(Action<bool> _onComplete = null)
        {
            if (IsInitialized)
            {
                _onComplete?.Invoke(true);
                return;
            }

            string key = AddressableAssetKey.UI_LoadingBarView.ToString();
            AddressableAssetManager.Instance.LoadPrefabAddress<GameObject>(
                key,
                prefab =>
                {
                    GameObject instance = AddressableAssetManager.Instance.InstantiatePrefab(prefab, transform);
                    if (instance == null || !instance.TryGetComponent(out loadingBarView))
                    {
                        DebugLogManager.GenerateErrorMessage<LoadingBarManager>("UI_LoadingBarView 프리팹에 UI_LoadingBarView 컴포넌트가 없습니다.");
                        _onComplete?.Invoke(false);
                        return;
                    }

                    instance.SetActive(false);
                    _onComplete?.Invoke(true);
                },
                () =>
                {
                    DebugLogManager.GenerateErrorMessage<LoadingBarManager>($"UI_LoadingBarView 로드 실패 Key : {key}");
                    _onComplete?.Invoke(false);
                });
        }

        /// <summary>이미 떠 있으면 그대로 두고, 아니면 진행률/타이틀을 비우고 표시한다.</summary>
        public void Show(string _title = "")
        {
            if (loadingBarView == null || IsShowing)
            {
                return;
            }

            loadingBarView.UpdateProgress(0f);
            loadingBarView.UpdateTitle(_title);
            loadingBarView.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (loadingBarView == null)
            {
                return;
            }

            loadingBarView.UpdateTitle(string.Empty);
            loadingBarView.gameObject.SetActive(false);
        }

        /// <summary>진행률(0.0f~1.0f)을 반영한다.</summary>
        public void UpdateProgress(float _progress)
        {
            loadingBarView?.UpdateProgress(_progress);
        }

        public void UpdateTitle(string _title)
        {
            loadingBarView?.UpdateTitle(_title);
        }
    }
}
