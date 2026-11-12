using Incheol.Utils;
using System;
using UnityEngine;

namespace Incheol.Modules
{
    /// <summary>
    /// React(웹페이지) ↔ Unity(WebGL) 통신 브리지.
    /// - React → Unity: unityInstance.SendMessage("MonitoringBridge", 메서드명, json) 으로 호출되는 public 메서드들.
    ///   수신한 JSON은 이벤트로 구독자에게 전달한다. (SendMessage는 메인 스레드에서 호출되므로 별도 디스패처 없이 바로 처리)
    /// - Unity → React: Send* 정적 메서드 → WebGLPlugin.jslib → window.dispatchReactUnityEvent / window.&lt;이름&gt;
    /// React가 SendMessage의 대상 오브젝트 이름으로 "MonitoringBridge"를 쓰므로 GameObject 이름은 클래스명과 달라도 반드시 이 값을 유지한다.
    /// </summary>
    public class ReactBridge : MonoBehaviour
    {
        #region Variable
        private const string GameObjectName = "MonitoringBridge";

        private static ReactBridge _instance;
        public static ReactBridge Instance
        {
            get
            {
                if (_instance == null)
                {
                    CreateSingleton();
                }
                return _instance;
            }
        }

        // React → Unity 수신 이벤트 (페이로드는 원본 JSON 문자열)
        public event Action<string> OnSectorEssListLoadedReceived;
        public event Action<string> OnEssSelectedReceived;
        public event Action<string> OnEssListUpdateReceived;
        public event Action<string> OnEssEventListReceived;

        public event Action<string> OnBatterySelectedReceived; // 장비 선택 시 해당 장비 정보 수신
        public event Action<string> OnBatteryUpdatedReceived;
        public event Action<string> OnBatteryListLoadedReceived;
        #endregion

        #region LifeCycle
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            CreateSingleton();
        }

        private static void CreateSingleton()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject go = GameObject.Find(GameObjectName);

            if (go == null)
            {
                go = new GameObject(GameObjectName);
            }

            _instance = go.GetComponent<ReactBridge>();

            if (_instance == null)
            {
                _instance = go.AddComponent<ReactBridge>();
            }

            DontDestroyOnLoad(go);
        }

        private void OnDestroy()
        {
            OnSectorEssListLoadedReceived = null;
            OnEssSelectedReceived = null;
            OnEssListUpdateReceived = null;
            OnEssEventListReceived = null;
        }
        #endregion

        #region Method (React → Unity)
        public void OnSectorEssListLoaded(string _json)
        {
            Receive(nameof(OnSectorEssListLoaded), _json, OnSectorEssListLoadedReceived);

            // JsonDataLoader를 통해 파싱 및 내부 데이터 반영
            if (string.IsNullOrEmpty(_json))
            {
                return;
            }

            JsonDataLoader loader = JsonDataLoader.Instance;
            if (loader == null)
            {
                DebugLogManager.GenerateErrorMessage<ReactBridge>("[ReactBridge] JsonDataLoader.Instance is null - cannot parse incoming data");
                return;
            }

            try
            {
                loader.LoadFromReactJson(_json);
                DebugLogManager.GenerateLogMessage<ReactBridge>("[ReactBridge] Delegated to JsonDataLoader.LoadFromReactJson");
            }
            catch (Exception e)
            {
                DebugLogManager.GenerateErrorMessage<ReactBridge>($"[ReactBridge] Delegation to JsonDataLoader failed: {e.Message}");
            }
        }

        public void OnEssSelected(string _json)
        {
            Receive(nameof(OnEssSelected), _json, OnEssSelectedReceived);
        }

        public void OnEssListUpdated(string _json)
        {
            Receive(nameof(OnEssListUpdated), _json, OnEssListUpdateReceived);
        }

        public void OnEssEventList(string _json)
        {
            Receive(nameof(OnEssEventList), _json, OnEssEventListReceived);
        }

        private void Receive(string _name, string _json, Action<string> _handlers)
        {
            if (string.IsNullOrEmpty(_json))
            {
                DebugLogManager.GenerateErrorMessage<ReactBridge>($"[ReactBridge] {_name}: null or empty payload");
                return;
            }

            DebugLogManager.GenerateLogMessage<ReactBridge>($"[ReactBridge] {_name} received. length={_json.Length}");

            try
            {
                _handlers?.Invoke(_json);
                DebugLogManager.GenerateLogMessage<ReactBridge>($"[ReactBridge] {_name}: event dispatched to subscribers");
            }
            catch (Exception e)
            {
                DebugLogManager.GenerateErrorMessage<ReactBridge>($"{_name} handler error: {e}");
            }
        }
        #endregion

        #region Method (Unity → React)
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void OnEssSelectedFromUnity(string message);
        [DllImport("__Internal")]
        private static extern void OnUnityReady(string message);
        [DllImport("__Internal")]
        private static extern void OnSceneLoaded(string message);
        [DllImport("__Internal")]
        private static extern void OnSceneUpdated(string message);
#endif

        /// <summary>Unity → React: Unity에서 ESS를 선택했을 때 호출. 예) {"deviceId":"..."}</summary>
        public static void SendEssSelectedFromUnity(string _json)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                OnEssSelectedFromUnity(_json ?? string.Empty);
            }
            catch (Exception e)
            {
                DebugLogManager.GenerateErrorMessage<ReactBridge>($"OnEssSelectedFromUnity failed: {e.Message}");
            }
#else
            DebugLogManager.GenerateLogMessage<ReactBridge>($"(Editor) Unity → React OnEssSelectedFromUnity: {_json}");
#endif
        }

        /// <summary>Unity → React: Bootstrap 완료 시 호출. JSON {"isReady":true} 전달.</summary>
        public static void SendUnityReady()
        {
            const string payload = "{\"isReady\":true}";
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                OnUnityReady(payload);
            }
            catch (Exception e)
            {
                DebugLogManager.GenerateErrorMessage<ReactBridge>($"OnUnityReady failed: {e.Message}");
            }
#else
            DebugLogManager.GenerateLogMessage<ReactBridge>($"(Editor) Unity → React OnUnityReady: {payload}");
#endif
        }

        /// <summary>Unity → React: 씬 로딩 완료 시 호출. JSON {"isLoaded":true} 전달.</summary>
        public static void SendSceneLoaded()
        {
            const string payload = "{\"isLoaded\":true}";
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                OnSceneLoaded(payload);
            }
            catch (Exception e)
            {
                DebugLogManager.GenerateErrorMessage<ReactBridge>($"OnSceneLoaded failed: {e.Message}");
            }
#else
            DebugLogManager.GenerateLogMessage<ReactBridge>($"(Editor) Unity → React OnSceneLoaded: {payload}");
#endif
        }

        /// <summary>Unity → React: 오브젝트 Setting 이후 호출. JSON {"isUpdated":true} 전달.</summary>
        public static void SendSceneUpdated()
        {
            const string payload = "{\"isUpdated\":true}";
#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                OnSceneUpdated(payload);
            }
            catch (Exception e)
            {
                DebugLogManager.GenerateErrorMessage<ReactBridge>($"OnSceneUpdated failed: {e.Message}");
            }
#else
            DebugLogManager.GenerateLogMessage<ReactBridge>($"(Editor) Unity → React OnSceneUpdated: {payload}");
#endif
        }
        #endregion
    }
}
