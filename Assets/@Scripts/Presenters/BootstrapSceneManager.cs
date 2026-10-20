using Incheol.Modules;
using Incheol.Utils;
using System.Threading;
using UnityEngine;

namespace Incheol.Presenter
{
    /// <summary>
    /// BootstrapScene의 진입점. 필요한 매니저를 순서대로 초기화(SequenceManager)한 뒤 첫 씬으로 전환한다.
    /// 순서: Addressable 초기화 → 로딩바 생성 → SceneLoadManager 초기화(SceneDataModelSO/AddressableAssetModelSO 로드) → 첫 씬 전환.
    /// </summary>
    public class BootstrapSceneManager : MonoBehaviour
    {
        /// <summary>SceneDataModelSO에 등록된 첫 전환 대상 tag.</summary>
        [SerializeField] private string firstSceneTag = "MainScene";

        private class AddressableAssetManage : ISequenceStep
        {
            public string StepName => "어드레서블 에셋 초기화";

            public async Awaitable<bool> Execute(CancellationToken _cancellationToken)
            {
                if (AddressableAssetManager.Instance == null)
                {
                    DebugLogManager.GenerateErrorMessage<BootstrapSceneManager>("AddressableAssetManager.Instance가 null입니다.");
                    return false;
                }

                AddressableAssetManager.Instance.Init();

                await Awaitable.NextFrameAsync(_cancellationToken);

                return true;
            }
        }

        private class LoadingBarManage : ISequenceStep
        {
            public string StepName => "로딩바 생성";

            public async Awaitable<bool> Execute(CancellationToken _cancellationToken)
            {
                if (LoadingBarManager.Instance == null)
                {
                    DebugLogManager.GenerateErrorMessage<BootstrapSceneManager>("LoadingBarManager.Instance가 null입니다.");
                    return false;
                }

                bool isDone = false;
                bool isSuccess = false;

                LoadingBarManager.Instance.Init(success =>
                {
                    isSuccess = success;
                    isDone = true;
                });

                while (!isDone)
                {
                    await Awaitable.NextFrameAsync(_cancellationToken);
                }

                if (!isSuccess)
                {
                    DebugLogManager.GenerateErrorMessage<BootstrapSceneManager>("LoadingBarManager Init 실패로 Bootstrap 시퀀스를 중단합니다.");
                    return false;
                }

                LoadingBarManager.Instance.Show();

                return true;
            }
        }

        private class LoadSceneManage : ISequenceStep
        {
            public string StepName => "씬 로드 매니저 초기화";

            public async Awaitable<bool> Execute(CancellationToken _cancellationToken)
            {
                if (SceneLoadManager.Instance == null)
                {
                    DebugLogManager.GenerateErrorMessage<BootstrapSceneManager>("SceneLoadManager.Instance가 null입니다.");
                    return false;
                }

                bool isDone = false;
                bool isSuccess = false;

                SceneLoadManager.Instance.Init(success =>
                {
                    isSuccess = success;
                    isDone = true;
                });

                while (!isDone)
                {
                    await Awaitable.NextFrameAsync(_cancellationToken);
                }

                if (!isSuccess)
                {
                    DebugLogManager.GenerateErrorMessage<BootstrapSceneManager>("SceneLoadManager Init 실패로 Bootstrap 시퀀스를 중단합니다.");
                }

                return isSuccess;
            }
        }

        private class ChangeSceneManage : ISequenceStep
        {
            private readonly string sceneTag;

            public ChangeSceneManage(string _sceneTag)
            {
                sceneTag = _sceneTag;
            }

            public string StepName => "첫 씬으로 전환";

            public async Awaitable<bool> Execute(CancellationToken _cancellationToken)
            {
                await Awaitable.NextFrameAsync(_cancellationToken);

                if (SceneLoadManager.Instance == null || !SceneLoadManager.Instance.IsInitialized)
                {
                    DebugLogManager.GenerateErrorMessage<BootstrapSceneManager>("SceneLoadManager가 초기화되지 않아 씬으로 전환할 수 없습니다.");
                    return false;
                }

                // 로딩바를 켠 채 전환한다. 전환이 끝나면 SceneLoadManager가 로딩바를 숨긴다.
                SceneLoadManager.Instance.LoadSceneByTags(sceneTag, true);

                return true;
            }
        }

        private ISequenceStep[] bootstrapSteps;

        #region LifeCycle
        private void Start()
        {
            // AddressableAssetManager 초기화(핸들 리셋)는 이 매니저로 프리팹을 로드하는 단계(LoadingBarManage 등)보다 반드시 앞에 와야 한다.
            // 새 매니저/데이터 로드 단계는 LoadSceneManage와 ChangeSceneManage 사이에 Enqueue하면 된다.
            bootstrapSteps = new ISequenceStep[]
            {
                new AddressableAssetManage(),
                new LoadingBarManage(),
                new LoadSceneManage(),
                new ChangeSceneManage(firstSceneTag),
            };

            for (int i = 0; i < bootstrapSteps.Length; i++)
            {
                SequenceManager.Instance.Enqueue(bootstrapSteps[i]);
            }

            SequenceManager.Instance.OnStepCompleted += OnBootstrapStepCompleted;
            SequenceManager.Instance.DoSequenceAction(OnBootstrapSequenceCompleted);
        }

        /// <summary>
        /// 단계가 하나 끝날 때마다 (완료 수 / 전체 수)만큼 로딩바 진행률을 올리고, 방금 끝난 단계 이름을 타이틀로 표시한다.
        /// 로딩바가 아직 생성되기 전 단계(Addressable 초기화)의 완료는 IsShowing 검사로 무시된다.
        /// </summary>
        private void OnBootstrapStepCompleted(int _completedCount, int _totalCount)
        {
            if (LoadingBarManager.Instance == null || !LoadingBarManager.Instance.IsShowing || _totalCount <= 0)
            {
                return;
            }

            LoadingBarManager.Instance.UpdateProgress((float)_completedCount / _totalCount);

            int stepIndex = _completedCount - 1;
            if (bootstrapSteps != null && stepIndex >= 0 && stepIndex < bootstrapSteps.Length)
            {
                LoadingBarManager.Instance.UpdateTitle($"{bootstrapSteps[stepIndex].StepName} 완료");
            }
        }

        private void OnDestroy()
        {
            if (SequenceManager.Instance != null)
            {
                SequenceManager.Instance.OnStepCompleted -= OnBootstrapStepCompleted;
            }
        }

        private void OnBootstrapSequenceCompleted(bool _isSuccess)
        {
            if (!_isSuccess)
            {
                DebugLogManager.GenerateErrorMessage<BootstrapSceneManager>("부트스트랩 시퀀스가 실패로 종료되었습니다.");
                return;
            }

            DebugLogManager.GenerateLogMessage<BootstrapSceneManager>("부트스트랩 시퀀스가 완료되었습니다.");
        }
        #endregion
    }
}
