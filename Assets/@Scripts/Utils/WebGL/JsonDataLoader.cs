using Incheol.Models;
using Incheol.Models.Define;
using DeviceType = Incheol.Models.Define.DeviceType;
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
    /// - React: LoadFromReactJson. 새 데이터가 오면 진행 중이던 이전 파싱은 취소된다.
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

        /// <summary>React 데이터 파싱이 끝나 jsonData에 반영된 직후에 발생한다. 인자는 파싱된 저장 데이터. 구독하기 전에 이미 끝났다면 isReactParsed로 확인한다.</summary>
        public event Action<JsonDataController.SaveJson> ReactParsed;

        /// <summary>
        /// 가장 최근 OnPartSelected로 받아 저장한 부품 모델. part.kind에 해당하는 최상단 모델이며(Pack ~ Cell), 그 아래 하위 모델까지 채워져 있다.
        /// 파싱·저장에 실패하면 이전 값이 유지된다.
        /// </summary>
        public IDeviceNode SelectedPart { get; private set; }

        /// <summary>가장 최근 OnPartUpdated로 받아 저장한 Pack 모델(Pack부터 Cell까지 전체). 저장에 실패하면 이전 값이 유지된다.</summary>
        public PackDataModel UpdatedPack { get; private set; }

        /// <summary>OnPartSelected 파싱·저장이 끝난 직후 발생한다. 인자는 part.kind 최상단 모델(SelectedPart). 구독자가 씬에 만들고 React에 OnSceneLoaded로 응답한다.</summary>
        public event Action<IDeviceNode> SelectedPartParsed;

        /// <summary>OnPartUpdated 파싱·저장이 끝난 직후 발생한다. 인자는 Pack 전체 모델(UpdatedPack). 구독자가 씬에 만들고 React에 OnSceneUpdated로 응답한다.</summary>
        public event Action<PackDataModel> UpdatedPackParsed;

        /// <summary>SendUnityReady 1회만 호출용 (MainSceneManager가 뷰 바인딩을 끝낸 뒤 NotifyUnityReady로 React에 알림)</summary>
        private static bool _hasSentUnityReady;

        /// <summary>진행 중인 React 파싱 작업 취소용. 새 데이터 수신 시 이전 작업을 취소한다.</summary>
        private CancellationTokenSource _reactParseCts;

        /// <summary>Pack 단위로 프레임을 나누어 처리. 1이면 매 Pack마다 한 프레임 양보.</summary>
        [SerializeField] private int _yieldEveryNPacks = 1;
        #endregion

        #region LifeCycle
        private ReactBridge _subscribedBridge;

        private void Start()
        {
            // React → Unity 부품 JSON(OnPartSelected)을 ReactBridge 이벤트로 받아 파싱한다.
            _subscribedBridge = ReactBridge.Instance;
            if (_subscribedBridge != null)
            {
                _subscribedBridge.OnPartSelectedReceived += OnPartSelectedReceived;
                _subscribedBridge.OnPartUpdatedReceived += OnPartUpdatedReceived;
            }
        }

        private void OnPartSelectedReceived(string _json)
        {
            // 파싱·저장이 성공하면 구독자(MainSceneManager)가 씬에 만들고, 다 만든 뒤 OnSceneLoaded로 응답한다.
            if (LoadFromPartJson(_json))
            {
                if (SelectedPartParsed == null)
                {
                    DebugLogManager.GenerateWarningMessage<JsonDataLoader>("[JsonDataLoader] SelectedPartParsed 구독자가 없어 씬에 만들지 못했습니다.");
                    return;
                }

                SelectedPartParsed.Invoke(SelectedPart);
            }
        }

        private void OnPartUpdatedReceived(string _json)
        {
            // 파싱·저장이 성공하면 구독자(MainSceneManager)가 씬에 만들고, 다 만든 뒤 OnSceneUpdated로 응답한다.
            if (LoadFromPartUpdatedJson(_json))
            {
                if (UpdatedPackParsed == null)
                {
                    DebugLogManager.GenerateWarningMessage<JsonDataLoader>("[JsonDataLoader] UpdatedPackParsed 구독자가 없어 씬에 만들지 못했습니다.");
                    return;
                }

                UpdatedPackParsed.Invoke(UpdatedPack);
            }
        }

        /// <summary>
        /// React에 OnUnityReady를 알린다. 최초 1회만 전송되며 이후 호출은 무시한다.
        /// MainSceneManager가 UI 프리팹 인스턴스화와 BindViews를 마친 직후에 호출한다.
        /// </summary>
        public void NotifyUnityReady()
        {
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
        }

        protected override void OnDestroy()
        {
            if (_subscribedBridge != null)
            {
                _subscribedBridge.OnPartSelectedReceived -= OnPartSelectedReceived;
                _subscribedBridge.OnPartUpdatedReceived -= OnPartUpdatedReceived;
                _subscribedBridge = null;
            }

            CancelReactParse();
            ReactParsed = null;
            SelectedPartParsed = null;
            UpdatedPackParsed = null;
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

            if (root == null || root.version != JsonDataController.CurrentVersion)
            {
                jsonData = new JsonDataController.SaveJson();
                DebugLogManager.GenerateLogMessage<JsonDataLoader>("Pack에 대한 Json 데이터가 없거나 읽을 수 없는 버전입니다.");
                return;
            }

            await BuildJsonDataAsync(
                root,
                _onFinished: (packs, moduleAssemblies, modules, parallelAssemblies, cells) =>
                {
                    DebugLogManager.GenerateLogMessage<JsonDataLoader>(
                        $"{key} JsonData 반영 완료 (packs={packs}, moduleAssemblies={moduleAssemblies}, modules={modules}, parallelAssemblies={parallelAssemblies}, cells={cells})");
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
        /// <summary>
        /// 저장 데이터의 Pack 트리를 순회하며 종류별 개수를 세고 null 목록을 보정한다. null 항목은 건너뛴다.
        /// </summary>
        private static void Count(JsonDataController.PackJson _pack, ref int _moduleAssemblies, ref int _modules, ref int _parallelAssemblies, ref int _cells)
        {
            _pack.moduleAssemblies ??= new List<JsonDataController.ModuleAssemblyJson>();
            _moduleAssemblies += _pack.moduleAssemblies.Count;
            foreach (JsonDataController.ModuleAssemblyJson moduleAssembly in _pack.moduleAssemblies)
            {
                if (moduleAssembly == null)
                {
                    continue;
                }

                moduleAssembly.modules ??= new List<JsonDataController.ModuleJson>();
                _modules += moduleAssembly.modules.Count;
                foreach (JsonDataController.ModuleJson module in moduleAssembly.modules)
                {
                    if (module == null)
                    {
                        continue;
                    }

                    module.parallelAssemblies ??= new List<JsonDataController.ParallelAssemblyJson>();
                    _parallelAssemblies += module.parallelAssemblies.Count;
                    foreach (JsonDataController.ParallelAssemblyJson parallelAssembly in module.parallelAssemblies)
                    {
                        if (parallelAssembly == null)
                        {
                            continue;
                        }

                        parallelAssembly.cells ??= new List<JsonDataController.CellJson>();
                        _cells += parallelAssembly.cells.Count;
                    }
                }
            }
        }

        /// <summary>
        /// Pack 목록을 프레임을 나누어 검사한 뒤 한 번에 커밋해, 프레임 분할 중에도 jsonData가 부분 상태로 남지 않게 함.
        /// </summary>
        private async Awaitable BuildJsonDataAsync(JsonDataController.SaveJson _source, Action<int, int, int, int, int> _onFinished, CancellationToken token = default)
        {
            int stride = Mathf.Max(1, _yieldEveryNPacks);
            _source.packs ??= new List<JsonDataController.PackJson>();
            List<JsonDataController.PackJson> built = new List<JsonDataController.PackJson>(_source.packs.Count);
            int moduleAssembliesCount = 0;
            int modulesCount = 0;
            int parallelAssembliesCount = 0;
            int cellsCount = 0;
            int processedSinceYield = 0;

            foreach (JsonDataController.PackJson pack in _source.packs)
            {
                token.ThrowIfCancellationRequested();

                if (pack == null)
                {
                    continue;
                }

                Count(pack, ref moduleAssembliesCount, ref modulesCount, ref parallelAssembliesCount, ref cellsCount);
                built.Add(pack);
                processedSinceYield++;

                if (processedSinceYield >= stride)
                {
                    processedSinceYield = 0;
                    await Awaitable.NextFrameAsync(token);
                }
            }

            token.ThrowIfCancellationRequested();

            _source.packs = built;
            jsonData = _source;

            _onFinished?.Invoke(built.Count, moduleAssembliesCount, modulesCount, parallelAssembliesCount, cellsCount);
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

                if (root == null || root.version != JsonDataController.CurrentVersion)
                {
                    jsonData = new JsonDataController.SaveJson();
                    DebugLogManager.GenerateLogMessage<JsonDataLoader>("Pack에 대한 Json 데이터가 없거나 읽을 수 없는 버전입니다.");
                    isReactParsing = false;
                    isReactParsed = false;
                    return;
                }

                await BuildJsonDataAsync(
                    root,
                    _onFinished: (packs, moduleAssemblies, modules, parallelAssemblies, cells) =>
                    {
                        DebugLogManager.GenerateLogMessage<JsonDataLoader>($"React JsonData 파싱 완료 (packs={packs}, moduleAssemblies={moduleAssemblies}, modules={modules}, parallelAssemblies={parallelAssemblies}, cells={cells})");
                        isReactParsing = false;
                        isReactParsed = true;
                        ReactParsed?.Invoke(jsonData);
                    },
                    token);
            }
            catch (OperationCanceledException)
            {
                // 더 최신 React 데이터가 도착해 취소됨 - 플래그는 새 작업이 소유하므로 건드리지 않음
            }
        }

        /// <summary>
        /// React로부터 전달된 부품 JSON({ requestId, part })을 파싱한다.
        /// part 존재 여부를 먼저 확인하고, part.kind를 DeviceType으로 변환해 종류별로 분기한다.
        /// 성공하면 true, 실패하면 에러 로그를 남기고 false.
        /// </summary>
        public bool LoadFromPartJson(string _json)
        {
            if (string.IsNullOrEmpty(_json))
            {
                DebugLogManager.GenerateErrorMessage<JsonDataLoader>("[JsonDataLoader] LoadFromPartJson: null or empty payload");
                return false;
            }

            // 1단계: requestId와 kind만 읽어 part 존재 여부와 부품 종류를 확인한다.
            PartHeaderRoot header;
            try
            {
                header = JsonUtility.FromJson<PartHeaderRoot>(_json);
            }
            catch (Exception e)
            {
                string preview = _json.Length > 128 ? _json.Substring(0, 128) + "..." : _json;
                DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"[JsonDataLoader] Part JSON 파싱 실패: {e.Message} preview={preview}");
                return false;
            }

            // part 존재 여부 필수 체크 (JsonUtility는 없는 객체를 기본 인스턴스로 채우므로 kind까지 확인)
            if (header == null || header.part == null || string.IsNullOrEmpty(header.part.kind))
            {
                DebugLogManager.GenerateErrorMessage<JsonDataLoader>(
                    $"[JsonDataLoader] Part JSON에 part 또는 part.kind가 없습니다. requestId={(header != null ? header.requestId : -1)}");
                return false;
            }

            if (!Enum.TryParse(header.part.kind, true, out DeviceType deviceType) || !Enum.IsDefined(typeof(DeviceType), deviceType))
            {
                DebugLogManager.GenerateErrorMessage<JsonDataLoader>(
                    $"[JsonDataLoader] 알 수 없는 part.kind: '{header.part.kind}' (requestId={header.requestId})");
                return false;
            }

            // 2단계: 종류별로 실제 타입으로 다시 파싱한다.
            try
            {
                switch (deviceType)
                {
                    case DeviceType.pack:
                    {
                        PackPartJson part = JsonUtility.FromJson<PartRoot<PackPartJson>>(_json).part;
                        // 최상단(pack)부터 최하위(Cell)까지 모델에 저장한다.
                        PackDataModel model = new PackDataModel();
                        if (!model.ApplyFrom(part))
                        {
                            DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"[JsonDataLoader] Pack 저장 실패 (id={part.id}, requestId={header.requestId})");
                            return false;
                        }

                        SelectedPart = model;
                        DebugLogManager.GenerateLogMessage<JsonDataLoader>(
                            $"[JsonDataLoader] Pack 파싱·저장 완료 (id={part.id}, name={part.name}, moduleAssemblies={part.moduleAssemblies.Count})");
                        break;
                    }
                    case DeviceType.moduleassembly:
                    {
                        ModuleAssemblyPartJson part = JsonUtility.FromJson<PartRoot<ModuleAssemblyPartJson>>(_json).part;
                        // 최상단(moduleassembly)부터 최하위(Cell)까지 모델에 저장한다.
                        ModuleAssemblyDataModel model = new ModuleAssemblyDataModel();
                        if (!model.ApplyFrom(part))
                        {
                            DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"[JsonDataLoader] ModuleAssembly 저장 실패 (id={part.id}, requestId={header.requestId})");
                            return false;
                        }

                        SelectedPart = model;
                        DebugLogManager.GenerateLogMessage<JsonDataLoader>(
                            $"[JsonDataLoader] ModuleAssembly 파싱·저장 완료 (id={part.id}, name={part.name}, modules={part.modules.Count}, numLevels={part.numLevels})");
                        break;
                    }
                    case DeviceType.module:
                    {
                        ModulePartJson part = JsonUtility.FromJson<PartRoot<ModulePartJson>>(_json).part;
                        // 최상단(module)부터 최하위(Cell)까지 모델에 저장한다.
                        ModuleDataModel model = new ModuleDataModel();
                        if (!model.ApplyFrom(part))
                        {
                            DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"[JsonDataLoader] Module 저장 실패 (id={part.id}, requestId={header.requestId})");
                            return false;
                        }

                        SelectedPart = model;
                        DebugLogManager.GenerateLogMessage<JsonDataLoader>(
                            $"[JsonDataLoader] Module 파싱·저장 완료 (id={part.id}, name={part.name}, topology={part.topology}, numSeriesAssemblies={part.numSeriesAssemblies})");
                        break;
                    }
                    case DeviceType.parallelassembly:
                    {
                        ParallelAssemblyPartJson part = JsonUtility.FromJson<PartRoot<ParallelAssemblyPartJson>>(_json).part;
                        // 최상단(parallelassembly)부터 최하위(Cell)까지 모델에 저장한다.
                        ParallelAssemblyDataModel model = new ParallelAssemblyDataModel();
                        if (!model.ApplyFrom(part))
                        {
                            DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"[JsonDataLoader] ParallelAssembly 저장 실패 (id={part.id}, requestId={header.requestId})");
                            return false;
                        }

                        SelectedPart = model;
                        DebugLogManager.GenerateLogMessage<JsonDataLoader>(
                            $"[JsonDataLoader] ParallelAssembly 파싱·저장 완료 (id={part.id}, name={part.name}, numParallelCells={part.numParallelCells}, rows={part.rows})");
                        break;
                    }
                    case DeviceType.cell:
                    {
                        CellPartJson part = JsonUtility.FromJson<PartRoot<CellPartJson>>(_json).part;
                        // 최상단(cell)부터 최하위(Cell)까지 모델에 저장한다.
                        CellDataModel model = new CellDataModel();
                        if (!model.ApplyFrom(part))
                        {
                            DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"[JsonDataLoader] Cell 저장 실패 (id={part.id}, requestId={header.requestId})");
                            return false;
                        }

                        SelectedPart = model;
                        DebugLogManager.GenerateLogMessage<JsonDataLoader>(
                            $"[JsonDataLoader] Cell 파싱·저장 완료 (id={part.id}, name={part.name}, shape={part.geometry?.shape})");
                        break;
                    }
                    default:
                        DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"[JsonDataLoader] 처리되지 않은 DeviceType: {deviceType}");
                        return false;
                }
            }
            catch (Exception e)
            {
                DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"[JsonDataLoader] {deviceType} 파싱 실패: {e.Message} (requestId={header.requestId})");
                return false;
            }

            DebugLogManager.GenerateLogMessage<JsonDataLoader>(
                $"[JsonDataLoader] Part JSON 파싱 완료 (requestId={header.requestId}, kind={deviceType})");
            return true;
        }

        /// <summary>
        /// React가 OnPartUpdated로 보낸 Pack 전체 JSON({ requestId, part })을 파싱한다.
        /// OnPartSelected와 달리 kind로 종류를 나누지 않고, part가 존재하는지만 확인한 뒤
        /// Pack → ModuleAssembly → Module → ParallelAssembly → Cell 트리 전체를 파싱한다.
        /// 성공하면 true, 실패하면 에러 로그를 남기고 false.
        /// </summary>
        public bool LoadFromPartUpdatedJson(string _json)
        {
            if (string.IsNullOrEmpty(_json))
            {
                DebugLogManager.GenerateErrorMessage<JsonDataLoader>("[JsonDataLoader] LoadFromPartUpdatedJson: null or empty payload");
                return false;
            }

            // part 존재 여부 필수 체크. JsonUtility는 없거나 null인 part도 빈 기본 인스턴스로 채우므로, 원본에 part 객체가 있는지 먼저 확인한다.
            if (!System.Text.RegularExpressions.Regex.IsMatch(_json, "\"part\"\\s*:\\s*\\{"))
            {
                DebugLogManager.GenerateErrorMessage<JsonDataLoader>("[JsonDataLoader] Part Updated JSON에 part가 없습니다.");
                return false;
            }

            PartRoot<PackPartJson> root;
            try
            {
                root = JsonUtility.FromJson<PartRoot<PackPartJson>>(_json);
            }
            catch (Exception e)
            {
                string preview = _json.Length > 128 ? _json.Substring(0, 128) + "..." : _json;
                DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"[JsonDataLoader] Part Updated JSON 파싱 실패: {e.Message} preview={preview}");
                return false;
            }

            if (root == null || root.part == null)
            {
                DebugLogManager.GenerateErrorMessage<JsonDataLoader>(
                    $"[JsonDataLoader] Part Updated JSON에 part가 없습니다. requestId={(root != null ? root.requestId : -1)}");
                return false;
            }

            // 트리를 훑어 종류별 개수를 센다. JsonUtility는 없는 하위 객체도 빈 인스턴스로 채우므로 id가 있는 것만 센다.
            int moduleAssemblies = 0;
            int modules = 0;
            int parallelAssemblies = 0;
            int cells = 0;

            root.part.moduleAssemblies ??= new List<ModuleAssemblyPartJson>();
            foreach (ModuleAssemblyPartJson moduleAssembly in root.part.moduleAssemblies)
            {
                if (moduleAssembly == null)
                {
                    continue;
                }

                moduleAssemblies++;
                moduleAssembly.modules ??= new List<ModulePartJson>();
                foreach (ModulePartJson module in moduleAssembly.modules)
                {
                    if (module == null)
                    {
                        continue;
                    }

                    modules++;
                    if (module.parallelAssembly != null && !string.IsNullOrEmpty(module.parallelAssembly.id))
                    {
                        parallelAssemblies++;
                        if (module.parallelAssembly.cell != null && !string.IsNullOrEmpty(module.parallelAssembly.cell.id))
                        {
                            cells++;
                        }
                    }
                }
            }

            // 최상단 Pack부터 Cell까지 전체를 새 모델에 저장한다. 실패하면 이전 UpdatedPack을 유지한다.
            PackDataModel pack = new PackDataModel();
            if (!pack.ApplyFrom(root.part))
            {
                DebugLogManager.GenerateErrorMessage<JsonDataLoader>($"[JsonDataLoader] Pack 저장 실패 (id={root.part.id}, requestId={root.requestId})");
                return false;
            }

            UpdatedPack = pack;
            DebugLogManager.GenerateLogMessage<JsonDataLoader>(
                $"[JsonDataLoader] Part Updated JSON 파싱 완료 (requestId={root.requestId}, id={root.part.id}, name={root.part.name}, moduleAssemblies={moduleAssemblies}, modules={modules}, parallelAssemblies={parallelAssemblies}, cells={cells})");
            return true;
        }
        #endregion
    }
}
