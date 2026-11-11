using Incheol.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Incheol.Modules
{
    /// <summary>
    /// JSON 문자열(React 전달 / Addressable 로컬 파일)을 저장 데이터(JsonDataController.SaveJson)로 파싱해 보관한다.
    /// - React: ReactBridge.OnSectorEssListLoaded → LoadFromReactJson. 새 데이터가 오면 진행 중이던 이전 파싱은 취소된다.
    /// - isReactParsing / isReactParsed 로 파싱 진행 상태를 다른 컨트롤러가 폴링할 수 있다.
    /// - 파싱은 프레임을 나누어 처리하고, 끝난 뒤 한 번에 jsonData에 커밋해 부분 상태가 노출되지 않게 한다.
    /// </summary>
    public class JsonDataLoader : SingletonObject<JsonDataLoader>
    {
        protected override bool PersistAcrossScenes => true;

        #region Variable
        [SerializeField] private string key = "TestData";

        /// <summary>
        /// 가장 최근에 파싱을 마친 저장 데이터. 파싱 중에는 이전 값이 유지되다가 완료 시점에 한 번에 교체된다.
        /// </summary>
        public JsonDataController.SaveJson jsonData = new JsonDataController.SaveJson();

        // React 데이터 파싱 상태
        public bool isReactParsing = false;
        public bool isReactParsed = false;

        /// <summary>SendUnityReady 1회만 호출용 (JsonDataLoader 인스턴스 생성 후 React에 알림)</summary>
        private static bool _hasSentUnityReady;

        /// <summary>진행 중인 React 파싱 작업 취소용. 새 데이터 수신 시 이전 작업을 취소한다.</summary>
        private CancellationTokenSource _reactParseCts;

        /// <summary>Container 단위로 프레임을 나누어 처리. 1이면 매 Container마다 한 프레임 양보.</summary>
        [SerializeField] private int _yieldEveryNContainers = 1;
        #endregion

        #region LifeCycle
        private void Start()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if (_hasSentUnityReady)
                return;
            _hasSentUnityReady = true;
            try
            {
                ReactBridge.SendUnityReady();
            }
            catch (Exception e)
            {
                DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"SendUnityReady failed: {e.Message}");
            }
#endif
        }

        protected override void OnDestroy()
        {
            CancelReactParse();
            base.OnDestroy();
        }
        #endregion

        #region Method
        /// <summary>Addressable로 등록된 로컬 JSON(key)을 읽어 반영한다. (에디터 테스트용)</summary>
        public void Init()
        {
            _ = InitAsync();
        }

        private async Awaitable InitAsync()
        {
            JsonDataController.SaveJson root;
            try
            {
                root = await FromAddressableOrOverrideAsync<JsonDataController.SaveJson>(key);
            }
            catch (Exception e)
            {
                DebugLogManager.GenerateLogMessage<JsonDataLoader>($"{e.Message}");
                return;
            }

            if (!JsonDataMigration.TryMigrate(root))
            {
                jsonData = new JsonDataController.SaveJson();
                DebugLogManager.GenerateLogMessage<JsonDataLoader>("Container에 대한 Json 데이터가 없거나 읽을 수 없는 버전입니다.");
                return;
            }

            await BuildJsonDataAsync(
                root,
                _onFinished: (containers, packs, modules, cells) =>
                {
                    DebugLogManager.GenerateLogMessage<JsonDataLoader>(
                        $"{key} JsonData 반영 완료 (containers={containers}, packs={packs}, modules={modules}, cells={cells})");
                });
        }

        /// <summary>
        /// Json 데이터 역직렬화_Json파일이 Addressable Asset 일 경우.
        /// 실패 시 예외를 던진다.
        /// </summary>
        public async Awaitable<T> FromAddressableOrOverrideAsync<T>(string _key)
        {
            AsyncOperationHandle<TextAsset> handle = Addressables.LoadAssetAsync<TextAsset>(_key);
            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Addressables.Release(handle);
                throw new Exception($"Addressables 로드 실패: key={_key}");
            }

            string json = handle.Result.text;
            Addressables.Release(handle);

            await Awaitable.NextFrameAsync();

            try
            {
                return JsonUtility.FromJson<T>(json);
            }
            catch (Exception e)
            {
                throw new Exception($"Addressables JSON 파싱 실패: {e.Message}");
            }
        }

        /// <summary>
        /// 단일 Container 트리를 순회하며 개수를 세고 null 목록을 보정한다.
        /// </summary>
        private void ProcessContainer(JsonDataController.ContainerJson _container, ref int _packsCount, ref int _modulesCount, ref int _cellsCount)
        {
            if (_container.packs == null)
            {
                _container.packs = new List<JsonDataController.PackJson>();
            }

            if (_container.standaloneModules == null)
            {
                _container.standaloneModules = new List<JsonDataController.ModuleJson>();
            }

            _packsCount += _container.packs.Count;
            foreach (JsonDataController.PackJson pack in _container.packs)
            {
                if (pack == null)
                {
                    continue;
                }

                if (pack.modules == null)
                {
                    pack.modules = new List<JsonDataController.ModuleJson>();
                }

                CountModules(pack.modules, ref _modulesCount, ref _cellsCount);
            }

            CountModules(_container.standaloneModules, ref _modulesCount, ref _cellsCount);
        }

        private static void CountModules(List<JsonDataController.ModuleJson> _modules, ref int _modulesCount, ref int _cellsCount)
        {
            _modulesCount += _modules.Count;
            foreach (JsonDataController.ModuleJson module in _modules)
            {
                if (module == null)
                {
                    continue;
                }

                if (module.cells == null)
                {
                    module.cells = new List<JsonDataController.CellJson>();
                }

                _cellsCount += module.cells.Count;
            }
        }

        /// <summary>
        /// Container 목록을 프레임을 나누어 검사한 뒤 한 번에 커밋해, 프레임 분할 중에도 jsonData가 부분 상태로 남지 않게 함.
        /// </summary>
        private async Awaitable BuildJsonDataAsync(JsonDataController.SaveJson _source, Action<int, int, int, int> _onFinished, CancellationToken token = default)
        {
            int stride = Mathf.Max(1, _yieldEveryNContainers);
            List<JsonDataController.ContainerJson> built = new List<JsonDataController.ContainerJson>(_source.containers.Count);
            int packsCount = 0;
            int modulesCount = 0;
            int cellsCount = 0;
            int processedSinceYield = 0;

            foreach (JsonDataController.ContainerJson container in _source.containers)
            {
                token.ThrowIfCancellationRequested();

                if (container == null)
                {
                    continue;
                }

                ProcessContainer(container, ref packsCount, ref modulesCount, ref cellsCount);
                built.Add(container);
                processedSinceYield++;

                if (processedSinceYield >= stride)
                {
                    processedSinceYield = 0;
                    await Awaitable.NextFrameAsync(token);
                }
            }

            token.ThrowIfCancellationRequested();

            _source.containers = built;
            jsonData = _source;

            _onFinished?.Invoke(built.Count, packsCount, modulesCount, cellsCount);
        }

        /// <summary>
        /// React로부터 전달된 JSON 문자열을 파싱해 내부 데이터 구조를 갱신(비동기 방식)
        /// </summary>
        /// <param name="_json"></param>
        public void LoadFromReactJson(string _json)
        {
            CancelReactParse();
            _reactParseCts = new CancellationTokenSource();

            isReactParsing = true;
            isReactParsed = false;
            _ = LoadFromReactJsonAsync(_json, _reactParseCts.Token);
        }

        /// <summary>
        /// 진행 중인 React 파싱 작업을 취소하고 토큰 소스를 정리한다.
        /// </summary>
        private void CancelReactParse()
        {
            if (_reactParseCts != null)
            {
                _reactParseCts.Cancel();
                _reactParseCts.Dispose();
                _reactParseCts = null;
            }
        }

        /// <summary>
        /// 한 프레임 양보 후 JSON 파싱 및 내부 데이터 갱신. 더 최신 데이터가 도착하면 token으로 취소된다.
        /// </summary>
        /// <param name="_json"></param>
        /// <param name="token"></param>
        private async Awaitable LoadFromReactJsonAsync(string _json, CancellationToken token)
        {
            try
            {
                // 프레임을 한 번 양보해 메인 루프를 막지 않음 (Init 패턴과 유사한 비동기 흐름)
                await Awaitable.NextFrameAsync(token);

                if (string.IsNullOrEmpty(_json))
                {
                    DebugLogManager.GenerateErrorMessage<JsonDataLoader>("[JsonDataLoader] LoadFromReactJsonAsync: null or empty payload");
                    isReactParsing = false;
                    isReactParsed = false;
                    return;
                }

                DebugLogManager.GenerateLogMessage<JsonDataLoader>($"[JsonDataLoader] React JSON parsing start. length={_json.Length}");

                await Awaitable.NextFrameAsync(token);

                JsonDataController.SaveJson root;
                try
                {
                    root = JsonUtility.FromJson<JsonDataController.SaveJson>(_json);
                }
                catch (Exception e)
                {
                    string preview = _json.Length > 128 ? _json.Substring(0, 128) + "..." : _json;
                    DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"React JSON 파싱 실패: {e.Message} preview={preview}");

                    if (!token.IsCancellationRequested)
                    {
                        isReactParsing = false;
                        isReactParsed = false;
                    }
                    return;
                }

                token.ThrowIfCancellationRequested();

                if (!JsonDataMigration.TryMigrate(root))
                {
                    jsonData = new JsonDataController.SaveJson();
                    DebugLogManager.GenerateLogMessage<JsonDataLoader>("Container에 대한 Json 데이터가 없거나 읽을 수 없는 버전입니다.");
                    isReactParsing = false;
                    isReactParsed = false;
                    return;
                }

                await BuildJsonDataAsync(
                    root,
                    _onFinished: (containers, packs, modules, cells) =>
                    {
                        DebugLogManager.GenerateLogMessage<JsonDataLoader>($"React JsonData 파싱 완료 (containers={containers}, packs={packs}, modules={modules}, cells={cells})");
                        isReactParsing = false;
                        isReactParsed = true;
                    },
                    token);
            }
            catch (OperationCanceledException)
            {
                // 더 최신 React 데이터가 도착해 취소됨 - 플래그는 새 작업이 소유하므로 건드리지 않음
            }
        }
        #endregion
    }
}
