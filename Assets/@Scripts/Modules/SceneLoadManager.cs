using Incheol.Models.SO;
using Incheol.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Incheol.Modules
{
    public class SceneLoadManager : SingletonObject<SceneLoadManager>
    {
        /// <summary>
        /// 씬 전환 중 자기 자신이 속한 씬이 언로드되어도 매니저가 파괴되지 않도록 유지한다.
        /// </summary>
        protected override bool PersistAcrossScenes => true;

        #region Variable
        [SerializeField] SceneDataModel currentSceneDataModel;
        private readonly SceneDataRegistry sceneDataRegistry = new SceneDataRegistry();

        /// <summary>
        /// Init이 성공 완료되었는지 여부
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// 0 ~ 100. Queue 업무가 완료될수록 100에 가까워진다.
        /// </summary>
        public float currentLoadProgressValue = 0.0f;

        /// <summary>진행률(0~100)이 바뀔 때마다 호출된다. 로딩바 UI 등이 구독한다.</summary>
        public event Action<float> OnLoadProgressChanged;

        private readonly Queue<ILoadTask> loadTaskQueue = new Queue<ILoadTask>();
        private int totalLoadTaskCount;
        private int completedLoadTaskCount;
        private string currentSceneTag;

        /// <summary>
        /// SceneLoadAsync가 진행 중인 동안 true. LoadSceneByTags의 중복/재진입 호출을 막는 데 사용한다.
        /// </summary>
        private bool isSceneLoading;

        /// <summary>씬 전환이 진행 중이면 true. 이 동안 들어온 LoadSceneByTags 요청은 무시된다.</summary>
        public bool IsSceneLoading => isSceneLoading;

        /// <summary>
        /// 이번 씬 전환의 진행률을 로딩바에 표시하는 중인지. 호출한 씬(예: Bootstrap)이 전환 도중 언로드되어도
        /// 씬이 유지되는 이 매니저가 로딩바 표시/진행률/숨김을 모두 맡는다.
        /// </summary>
        private bool reportToLoadingBar;

        private const string loadingBarTitle = "데이터를 불러오는 중...";
        #endregion

        #region Method
        /// <summary>
        /// SceneDataModelSO / AddressableAssetModelSO를 Addressable로 로드한다.
        /// 완료 시 _onComplete(true/false)를 반드시 호출하여 호출측이 무한 대기하지 않도록 한다.
        /// </summary>
        public void Init(Action<bool> _onComplete = null)
        {
            IsInitialized = false;
            sceneDataRegistry.Load(success =>
            {
                IsInitialized = success;
                _onComplete?.Invoke(success);
            });
        }

        /// <summary>
        /// SceneDataModelSO에 등록된 tag의 씬 구성으로 전환한다.
        /// 해당 tag의 preload Addressable을 먼저 모두 로드한 뒤, 필요한 씬만 Additive 로드하고 더 이상 필요 없는 이전 씬만 언로드한다.
        /// _showLoadingBar가 true이면 전환이 끝날 때까지 로딩바에 진행률을 표시하고, 끝나면 숨긴다.
        /// </summary>
        public void LoadSceneByTags(string _tagName, bool _showLoadingBar = false)
        {
            if (!IsInitialized || !sceneDataRegistry.IsLoaded)
            {
                DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"LoadSceneByTags 호출 전에 Init이 완료되지 않았습니다. tag : {_tagName}");
                return;
            }

            if (string.IsNullOrEmpty(_tagName))
            {
                DebugLogManager.GenerateErrorMessage<SceneLoadManager>("LoadSceneByTags에 빈 tag가 전달되었습니다.");
                return;
            }

            if (!sceneDataRegistry.TryGetScene(_tagName, out SceneDataModel targetSceneDataModel))
            {
                DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"존재하지 않는 Scene tag : {_tagName}. 등록된 tags : [{sceneDataRegistry.DescribeSceneTags()}]");
                return;
            }

            if (isSceneLoading)
            {
                // 이전 SceneLoadAsync가 끝나기 전에 재호출되면 loadTaskQueue/진행률 카운터가 두 요청 사이에서 꼬이므로 막는다.
                DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"이전 씬 로드가 아직 끝나지 않아 요청을 무시합니다. 요청한 tag : {_tagName}, 진행 중인 tag : {currentSceneTag}");
                return;
            }

            SceneDataModel previousSceneDataModel = currentSceneDataModel;

            currentSceneTag = _tagName;
            currentSceneDataModel = targetSceneDataModel;
            _ = SceneLoadAsync(previousSceneDataModel, currentSceneDataModel, _showLoadingBar);
        }

        /// <summary>
        /// 이전/대상 SceneDataModel을 비교해 필요한 씬만 로드하고, 더 이상 필요없는 씬만 언로드한다.
        /// </summary>
        private async Awaitable SceneLoadAsync(SceneDataModel _previous, SceneDataModel _target, bool _showLoadingBar)
        {
            isSceneLoading = true;

            try
            {
                reportToLoadingBar = _showLoadingBar && LoadingBarManager.Instance != null && LoadingBarManager.Instance.IsInitialized;
                if (reportToLoadingBar)
                {
                    LoadingBarManager.Instance.Show();
                    LoadingBarManager.Instance.UpdateTitle(loadingBarTitle);
                }

                // 이전 태그 세션에서 로드했던 Addressable 핸들 정리 (최초 실행 시 keyDictionary가 비어 있어 no-op).
                // 로딩바 인스턴스가 쓰는 에셋은 해제하면 안 되므로 보존한다.
                AddressableAssetManager.Instance.ReleaseAllHandler(LoadingBarManager.PreservedKeys);

                SetLoadProgress(0.0f);
                loadTaskQueue.Clear();
                totalLoadTaskCount = 0;
                completedLoadTaskCount = 0;

                List<string> targetSceneList = _target.loadedSceneList ?? new List<string>();

                // SceneDataModel은 [Serializable] class라서 Unity가 컴포넌트를 역직렬화할 때 _previous가 절대 null이 되지 않는다.
                // 그래서 "이전 SceneDataModel이 없다(최초 전환, 예: BootstrapScene)"는 tags가 비어있는지로 판별한다.
                bool hasPreviousModel = _previous != null && !string.IsNullOrEmpty(_previous.tags);

                // 이전 SceneDataModel이 없으면(최초 전환) 현재 활성 씬을 정리 대상으로 간주해 diff에 포함시킨다.
                List<string> previousSceneList = hasPreviousModel
                    ? (_previous.loadedSceneList ?? new List<string>())
                    : new List<string> { SceneManager.GetActiveScene().name };

                List<string> scenesToUnload = previousSceneList.Where(sceneName => !targetSceneList.Contains(sceneName)).ToList();

                EnqueueLoadTasks(_target);
                await ProcessLoadTaskQueueAsync();

                // Queue가 비워져 Progress가 100이 된 뒤에만 이전 씬을 정리한다.
                SetLoadProgress(100.0f);

                for (int i = 0; i < scenesToUnload.Count; i++)
                {
                    Scene scene = SceneManager.GetSceneByName(scenesToUnload[i]);
                    if (!scene.IsValid() || !scene.isLoaded)
                    {
                        continue;
                    }

                    AsyncOperation unloadOperation = SceneManager.UnloadSceneAsync(scene);
                    if (unloadOperation == null)
                    {
                        continue;
                    }

                    while (!unloadOperation.isDone)
                    {
                        await Awaitable.NextFrameAsync();
                    }
                }
            }
            finally
            {
                if (reportToLoadingBar)
                {
                    reportToLoadingBar = false;
                    LoadingBarManager.Instance?.Hide();
                }

                isSceneLoading = false;
            }
        }

        /// <summary>
        /// 진행률(0~100)을 저장하고, 로딩바를 표시 중이면 로딩바(0~1)에도 반영하며 구독자에게 알린다.
        /// </summary>
        private void SetLoadProgress(float _percent)
        {
            currentLoadProgressValue = _percent;

            if (reportToLoadingBar)
            {
                LoadingBarManager.Instance?.UpdateProgress(_percent / 100.0f);
            }

            OnLoadProgressChanged?.Invoke(_percent);
        }

        /// <summary>
        /// Addressable / Additive 씬 로드 업무를 Queue에 등록한다.
        /// </summary>
        private void EnqueueLoadTasks(SceneDataModel _target)
        {
            List<string> addressableKeys = sceneDataRegistry.CollectPreloadKeyStrings(currentSceneTag);
            for (int i = 0; i < addressableKeys.Count; i++)
            {
                loadTaskQueue.Enqueue(new AddressableLoadTask(addressableKeys[i]));
            }

            List<string> sceneTargets = _target.loadedSceneList ?? new List<string>();
            for (int i = 0; i < sceneTargets.Count; i++)
            {
                loadTaskQueue.Enqueue(new AdditiveSceneLoadTask(sceneTargets[i], _target.activeSceneName));
            }

            totalLoadTaskCount = loadTaskQueue.Count;
            completedLoadTaskCount = 0;
            UpdateProgressByQueue();
        }

        /// <summary>
        /// Queue에 쌓인 업무를 모두 동시에 시작한다.
        /// 태스크를 순차로 기다리면 하나가 느려질 때 전체 진행률이 함께 멈추는 문제가 있어,
        /// 각 업무를 병렬로 실행하고 완료된 개수만큼만 진행률을 올린다.
        /// </summary>
        private async Awaitable ProcessLoadTaskQueueAsync()
        {
            if (totalLoadTaskCount <= 0)
            {
                SetLoadProgress(100.0f);
                return;
            }

            while (loadTaskQueue.Count > 0)
            {
                ILoadTask task = loadTaskQueue.Dequeue();
                _ = RunLoadTaskAsync(task);
            }

            while (completedLoadTaskCount < totalLoadTaskCount)
            {
                await Awaitable.NextFrameAsync();
            }

            SetLoadProgress(100.0f);
        }

        private async Awaitable RunLoadTaskAsync(ILoadTask _task)
        {
            try
            {
                await _task.ExecuteAsync();
            }
            catch (Exception exception)
            {
                // 업무 하나가 예외를 던지더라도 completedLoadTaskCount가 증가하지 않으면
                // ProcessLoadTaskQueueAsync의 대기 루프가 영원히 끝나지 않으므로, 예외를 흡수하고 완료 처리는 finally에서 항상 수행한다.
                DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"로드 업무 처리 중 예외 발생 : {exception}");
            }
            finally
            {
                completedLoadTaskCount++;
                UpdateProgressByQueue();
            }
        }

        private void UpdateProgressByQueue()
        {
            if (totalLoadTaskCount <= 0)
            {
                SetLoadProgress(100.0f);
                return;
            }

            SetLoadProgress(((float)completedLoadTaskCount / totalLoadTaskCount) * 100.0f);
        }
        #endregion
    }
}
