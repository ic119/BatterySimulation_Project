using Incheol.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Incheol.Modules
{
    public class AddressableAssetManager : SingletonObject<AddressableAssetManager>
    {
        /// <summary>
        /// keyDictionary 등 로드 캐시는 씬 전환을 넘어 유지되어야 하므로, 이 매니저 자신도 파괴되지 않아야 한다.
        /// </summary>
        protected override bool PersistAcrossScenes => true;

        #region Variable
        private readonly Dictionary<string, AsyncOperationHandle> keyDictionary = new Dictionary<string, AsyncOperationHandle>();
        private readonly HashSet<string> keyHashSet = new HashSet<string>();
        private readonly HashSet<string> loadingKeyHashSet = new HashSet<string>();
        private readonly HashSet<string> failedKeyHashSet = new HashSet<string>();

        private readonly Dictionary<string, List<Action>> loadingFailCallbackDictionary = new Dictionary<string, List<Action>>();
        private readonly Dictionary<string, AsyncOperationHandle> loadingHandleDictionary = new Dictionary<string, AsyncOperationHandle>();
        private readonly Dictionary<string, List<Action<UnityEngine.Object>>> loadingCallbackDictionary = new Dictionary<string, List<Action<UnityEngine.Object>>>();

        #endregion

        #region Method
        public void Init()
        {
            ReleaseAllHandler();
        }

        public void AddKeyHashSet(string _key)
        {
            keyHashSet.Add(_key);
        }

        public void DeleteKeyHashSet(string _key)
        {
            keyHashSet.Remove(_key);
        }

        public void LoadPrefabAddressFromHashSet(Action<string, GameObject> _onLoad = null)
        {
            if (keyHashSet.Count > 0)
            {
                foreach (var key in keyHashSet)
                {
                    LoadPrefabAddress<GameObject>(key, result => _onLoad?.Invoke(key, result));
                }
            }
        }

public void LoadPrefabAddress<T>(string _key, Action<T> _onLoad = null, Action _onFail = null) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(_key))
            {
                return;
            }

            if (GetHandler(_key, out var _handler))
            {
                if (_handler.Status == AsyncOperationStatus.Succeeded)
                {
                    if (_handler.Result is T result)
                    {
                        _onLoad?.Invoke(result);
                    }
                    else
                    {
                        DebugLogManager.GenerateErrorMessage<AddressableAssetManager>($"Addressable 캐시 타입 불일치 Key : {_key}, 캐시된 타입 : {_handler.Result?.GetType().Name}, 요청한 타입 : {typeof(T).Name}");
                    }
                }
                return;
            }

            if (loadingHandleDictionary.ContainsKey(_key))
            {
                RegisterLoadingCallback(_key, _onLoad);
                RegisterFailCallback(_key, _onFail);
                return;
            }

            // 재시도 시 이전 실패 기록 제거
            failedKeyHashSet.Remove(_key);
            loadingKeyHashSet.Add(_key);
            AsyncOperationHandle<T> handler;

            try
            {
                handler = Addressables.LoadAssetAsync<T>(_key);
            }
            catch (Exception exception)
            {
                loadingKeyHashSet.Remove(_key);
                failedKeyHashSet.Add(_key);
                DebugLogManager.GenerateErrorMessage<AddressableAssetManager>($"Addressable 로드 실패(잘못된 Key) Key : {_key}, Exception : {exception}");
                _onFail?.Invoke();
                return;
            }

            loadingHandleDictionary[_key] = handler;
            RegisterLoadingCallback(_key, _onLoad);
            RegisterFailCallback(_key, _onFail);

            handler.Completed += h =>
            {
                bool _isCurrent = loadingHandleDictionary.TryGetValue(_key, out var _currentHandle) && _currentHandle.Equals(handler);

                List<Action<UnityEngine.Object>> _callbacks = null;
                List<Action> _failCallbacks = null;

                if (_isCurrent)
                {
                    loadingKeyHashSet.Remove(_key);
                    loadingHandleDictionary.Remove(_key);
                    loadingCallbackDictionary.TryGetValue(_key, out _callbacks);
                    loadingCallbackDictionary.Remove(_key);
                    loadingFailCallbackDictionary.TryGetValue(_key, out _failCallbacks);
                    loadingFailCallbackDictionary.Remove(_key);
                }

                if (!_isCurrent)
                {
                    if (h.Status == AsyncOperationStatus.Succeeded)
                    {
                        Addressables.Release(h);
                    }
                    return;
                }

                if (h.Status == AsyncOperationStatus.Succeeded)
                {
                    if (!keyDictionary.ContainsKey(_key))
                    {
                        keyDictionary.Add(_key, h);
                    }

                    if (_callbacks != null)
                    {
                        foreach (var _callback in _callbacks)
                        {
                            // 콜백 하나가 예외를 던지더라도 같은 Key를 기다리던 다른 호출자의 콜백까지
                            // 함께 유실되지 않도록 각 콜백 호출을 개별적으로 격리한다.
                            try
                            {
                                _callback(h.Result);
                            }
                            catch (Exception exception)
                            {
                                DebugLogManager.GenerateErrorMessage<AddressableAssetManager>($"Addressable 로드 완료 콜백 처리 중 예외 발생 Key : {_key}, Exception : {exception}");
                            }
                        }
                    }
                }
                else
                {
                    failedKeyHashSet.Add(_key);
                    DebugLogManager.GenerateErrorMessage<AddressableAssetManager>($"Addressable 로드 실패 Key : {_key}, Status : {h.Status}, Exception : {h.OperationException}");

                    if (_failCallbacks != null)
                    {
                        foreach (var _failCallback in _failCallbacks)
                        {
                            try
                            {
                                _failCallback();
                            }
                            catch (Exception exception)
                            {
                                DebugLogManager.GenerateErrorMessage<AddressableAssetManager>($"Addressable 로드 실패 콜백 처리 중 예외 발생 Key : {_key}, Exception : {exception}");
                            }
                        }
                    }
                }
            };
        }

        private void RegisterLoadingCallback<T>(string _key, Action<T> _onLoad) where T : UnityEngine.Object
        {
            if (_onLoad == null)
            {
                return;
            }

            if (!loadingCallbackDictionary.TryGetValue(_key, out var _callbackList))
            {
                _callbackList = new List<Action<UnityEngine.Object>>();
                loadingCallbackDictionary[_key] = _callbackList;
            }

            _callbackList.Add(_result =>
            {
                if (_result is T typedResult)
                {
                    _onLoad.Invoke(typedResult);
                }
                else
                {
                    DebugLogManager.GenerateErrorMessage<AddressableAssetManager>($"Addressable 로딩 중인 Key를 다른 타입으로 재요청했습니다 Key : {_key}, 요청한 타입 : {typeof(T).Name}");
                }
            });
        }

        private void RegisterFailCallback(string _key, Action _onFail)
        {
            if (_onFail == null)
            {
                return;
            }

            if (!loadingFailCallbackDictionary.TryGetValue(_key, out var _callbackList))
            {
                _callbackList = new List<Action>();
                loadingFailCallbackDictionary[_key] = _callbackList;
            }

            _callbackList.Add(_onFail);
        }


        public bool IsLoading(string _key)
        {
            return !string.IsNullOrEmpty(_key) && loadingKeyHashSet.Contains(_key);
        }

        public bool HasLoadFailed(string _key)
        {
            return !string.IsNullOrEmpty(_key) && failedKeyHashSet.Contains(_key);
        }

        public bool IsLoaded(string _key)
        {
            return GetHandler(_key, out _);
        }

        /// <summary>
        /// _key의 현재 로딩 진행률(0~1)을 조회한다. 로딩 중이면 Addressables가 보고하는 실제
        /// AsyncOperationHandle.PercentComplete를, 이미 로드 완료된 상태면 1을 반환한다.
        /// 로딩 중도 아니고 완료도 안 된 경우(아직 시작 안 함/실패 등)는 false를 반환한다.
        /// </summary>
        public bool TryGetLoadingProgress(string _key, out float _percentComplete)
        {
            if (loadingHandleDictionary.TryGetValue(_key, out AsyncOperationHandle _loadingHandle))
            {
                _percentComplete = _loadingHandle.PercentComplete;
                return true;
            }

            if (GetHandler(_key, out _))
            {
                _percentComplete = 1f;
                return true;
            }

            _percentComplete = 0f;
            return false;
        }

        /// <summary>
        /// _onProgress를 넘기면 대기하는 동안 매 프레임 TryGetLoadingProgress로 조회한 실제 진행률을 알려준다 -
        /// 로딩바를 "0%에 머물다 끝나면 100%로 점프"가 아니라 실제 다운로드/인스턴스화 진행률에 맞춰 채울 수 있다.
        /// </summary>
        public async Awaitable WaitForLoadAsync(string _key, Func<bool> _isCancelled = null, Action<float> _onProgress = null)
        {
            while (true)
            {
                if (_isCancelled != null && _isCancelled())
                {
                    return;
                }

                if (GetHandler(_key, out _))
                {
                    return;
                }

                if (HasLoadFailed(_key))
                {
                    return;
                }

                if (_onProgress != null && TryGetLoadingProgress(_key, out float _percentComplete))
                {
                    _onProgress(_percentComplete);
                }

                await Awaitable.NextFrameAsync();
            }
        }

        public T InstantiatePrefab<T>(T _type, Transform _parent = null) where T : UnityEngine.Object
        {
            if (_type is GameObject go)
            {
                return GameObject.Instantiate(go, _parent) as T;
            }
            return _type;
        }

        public bool GetHandler(string _key, out AsyncOperationHandle _handler)
        {
            return keyDictionary.TryGetValue(_key, out _handler);
        }

        public void ReleaseHandler(string _key)
        {
            loadingKeyHashSet.Remove(_key);
            failedKeyHashSet.Remove(_key);

            if (GetHandler(_key, out var _handler))
            {
                Addressables.Release(_handler);
                keyDictionary.Remove(_key);
                DeleteKeyHashSet(_key);
                return;
            }

            if (loadingHandleDictionary.Remove(_key))
            {
                AbortLoadingKey(_key);
                DeleteKeyHashSet(_key);
                return;
            }

            keyHashSet.Remove(_key);
        }

        public void ReleaseAllHandler(ICollection<string> _preserveKeys = null)
        {
            List<string> keysToRelease = new List<string>();
            foreach (string key in keyDictionary.Keys)
            {
                if (_preserveKeys == null || !_preserveKeys.Contains(key))
                {
                    keysToRelease.Add(key);
                }
            }

            for (int i = 0; i < keysToRelease.Count; i++)
            {
                string key = keysToRelease[i];
                Addressables.Release(keyDictionary[key]);
                keyDictionary.Remove(key);
                keyHashSet.Remove(key);
            }

            // 로딩 중이던 핸들은 완료 전에는 안전하게 Release할 수 없으므로 추적만 제거한다.
            // 실제 Addressables.Release는 LoadPrefabAddress의 Completed 콜백이 완료 시점에 대신 처리한다
            // (Completed 핸들러가 _isCurrent를 false로 판정해 핸들만 정리하고 등록된 콜백은 호출하지 않는다).
            // 그 콜백을 기다리던 호출자가 아무 통보도 못 받은 채 방치되지 않도록, 여기서 직접 중단을 알린다.
            List<string> abortedLoadingKeys = new List<string>(loadingHandleDictionary.Keys);
            loadingHandleDictionary.Clear();

            for (int i = 0; i < abortedLoadingKeys.Count; i++)
            {
                AbortLoadingKey(abortedLoadingKeys[i]);
            }

            loadingKeyHashSet.Clear();
            failedKeyHashSet.Clear();
        }

        /// <summary>
        /// ReleaseHandler/ReleaseAllHandler가 아직 완료되지 않은 로드를 추적 대상에서 제외할 때 호출한다.
        /// 등록된 실패 콜백을 대신 호출해 대기 중인 호출자에게 중단 사실을 알리고, loadingFailCallbackDictionary에
        /// 항목이 남아 있다가 나중에 같은 Key로 새로 시작한 로드의 결과에 엉뚱하게 다시 불리는 것도 막는다.
        /// </summary>
        private void AbortLoadingKey(string _key)
        {
            loadingCallbackDictionary.Remove(_key);

            if (!loadingFailCallbackDictionary.TryGetValue(_key, out List<Action> _failCallbacks))
            {
                return;
            }

            loadingFailCallbackDictionary.Remove(_key);

            foreach (Action _failCallback in _failCallbacks)
            {
                try
                {
                    _failCallback();
                }
                catch (Exception exception)
                {
                    DebugLogManager.GenerateErrorMessage<AddressableAssetManager>($"Addressable 로드 중단 콜백 처리 중 예외 발생 Key : {_key}, Exception : {exception}");
                }
            }
        }
        #endregion
    }
}
