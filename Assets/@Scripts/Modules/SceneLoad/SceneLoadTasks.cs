using Incheol.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Incheol.Modules
{
    /// <summary>
    /// SceneLoadManager가 씬 전환 중 순차/병렬로 처리하는 단일 로드 업무.
    /// </summary>
    internal interface ILoadTask
    {
        Awaitable ExecuteAsync();
    }

    /// <summary>
    /// Addressable 프리팹 하나를 미리 로드한다. 이미 캐시돼 있으면 즉시 끝나고, 실패/타임아웃은 로그만 남기고 넘어간다.
    /// </summary>
    internal class AddressableLoadTask : ILoadTask
    {
        private readonly string key;
        private const float loadTimeoutSeconds = 30.0f;

        public AddressableLoadTask(string _key)
        {
            key = _key;
        }

        public async Awaitable ExecuteAsync()
        {
            if (AddressableAssetManager.Instance == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            AddressableAssetManager controller = AddressableAssetManager.Instance;
            controller.AddKeyHashSet(key);
            controller.LoadPrefabAddress<GameObject>(key);

            // 이미 캐시되어 있으면 즉시 완료
            if (controller.IsLoaded(key))
            {
                return;
            }

            // 핸들 폴링 + NextFrameAsync 루프는 AddressableAssetController.WaitForLoadAsync로 공통화되어 있으므로
            // 여기서는 타임아웃 조건만 전달해 중복 구현을 피한다.
            float startTime = Time.unscaledTime;
            await controller.WaitForLoadAsync(key, () => Time.unscaledTime - startTime >= loadTimeoutSeconds);

            if (controller.IsLoaded(key))
            {
                return;
            }

            if (controller.HasLoadFailed(key))
            {
                DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"Addressable 프리로드 실패 Key : {key}");
                return;
            }

            // 위 두 경우가 아니라면 타임아웃으로 대기가 중단된 것이다.
            DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"Addressable 프리로드 타임아웃 Key : {key}, Timeout : {loadTimeoutSeconds}s");
        }
    }

    /// <summary>
    /// 씬 하나를 Additive로 로드한다. 이전 씬과 공유되는 씬은 이미 로드돼 있으므로 다시 로드하지 않고 활성 씬 지정만 한다.
    /// </summary>
    internal class AdditiveSceneLoadTask : ILoadTask
    {
        private readonly string sceneName;
        private readonly string activeSceneName;

        public AdditiveSceneLoadTask(string _sceneName, string _activeSceneName)
        {
            sceneName = _sceneName;
            activeSceneName = _activeSceneName;
        }

        public async Awaitable ExecuteAsync()
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return;
            }

            // diff 전환 과정에서 이전 씬과 공유되는 씬은 이미 로드되어 있을 수 있으므로,
            // 중복 로드하지 않고 활성 씬 처리만 한다.
            Scene existingScene = SceneManager.GetSceneByName(sceneName);
            if (existingScene.IsValid() && existingScene.isLoaded)
            {
                if (activeSceneName == sceneName)
                {
                    SceneManager.SetActiveScene(existingScene);
                }
                return;
            }

            AsyncOperation async = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            if (async == null)
            {
                // 빌드 세팅에 없는 씬 이름 등으로 로드 자체가 시작되지 못한 경우.
                // null 상태로 async.isDone에 접근하면 NullReferenceException이 발생하므로 여기서 방어한다.
                DebugLogManager.GenerateErrorMessage<SceneLoadManager>($"Additive Scene 로드 실패(빌드 세팅에 없는 씬일 수 있음) SceneName : {sceneName}");
                return;
            }

            while (!async.isDone)
            {
                await Awaitable.NextFrameAsync();
            }

            if (activeSceneName == sceneName)
            {
                Scene targetActiveScene = SceneManager.GetSceneByName(sceneName);
                if (targetActiveScene.IsValid())
                {
                    SceneManager.SetActiveScene(targetActiveScene);
                }
            }
        }
    }
}
