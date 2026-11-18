using Incheol.Models;
using Incheol.Utils;
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Incheol.Modules
{
    /// <summary>
    /// React(웹페이지) ↔ Unity(WebGL) 통신 브리지.
    /// - React → Unity: unityInstance.SendMessage("BatteryBridge", 메서드명, json) 으로 호출되는 public 메서드들.
    ///   수신한 JSON은 이벤트로 구독자에게 전달한다. (SendMessage는 메인 스레드에서 호출되므로 별도 디스패처 없이 바로 처리)
    /// - Unity → React: Send* 정적 메서드 → WebGLPlugin.jslib → window.dispatchReactUnityEvent / window.&lt;이름&gt;
    /// React가 SendMessage의 대상 오브젝트 이름으로 "BatteryBridge"를 쓰므로 GameObject 이름은 클래스명과 달라도 반드시 이 값을 유지한다.
    /// </summary>
    public class ReactBridge : MonoBehaviour
    {
        #region Variable
        private const string GameObjectName = "BatteryBridge";

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
        public event Action<string> OnPartSelectedReceived; // React화면에서 좌측 Tree구조에서 장비를 클릭했을 때, 해당 장비에 대한 데이터를 Unity로 전송
        public event Action<string> OnPartUpdatedReceived; // React화면에서 우측하단 Apply 버튼 클릭 시, Pack 전체에 대한 장비 데이터를 Unity로 전송

        /// <summary>가장 최근 OnPartSelected / OnPartUpdated로 받은 requestId. 아직 받은 적이 없으면 0.</summary>
        public static int LastPartRequestId { get; private set; }

        [Serializable]
        private class SceneUpdatedPayload
        {
            public bool isUpdated;
            public int requestId;
        }

        [Serializable]
        private class SceneLoadedPayload
        {
            public bool isLoaded;
            public int requestId;
        }

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
            OnPartSelectedReceived = null;
            OnPartUpdatedReceived = null;
        }
        #endregion

        #region Method (React → Unity)
        /// <summary>React → Unity: 부품 JSON({ requestId, part }) 수신 진입점. 파싱은 OnPartSelectedReceived 구독자(JsonDataLoader)가 맡는다.</summary>
        public void OnPartSelected(string _json)
        {
            CacheLastPartRequestId(_json);
            Receive(nameof(OnPartSelected), _json, OnPartSelectedReceived);
        }

        /// <summary>React → Unity: Apply 시 전달되는 Pack 전체 JSON({ requestId, part }) 수신 진입점. 파싱은 OnPartUpdatedReceived 구독자(JsonDataLoader)가 맡는다.</summary>
        public void OnPartUpdated(string _json)
        {
            CacheLastPartRequestId(_json);
            Receive(nameof(OnPartUpdated), _json, OnPartUpdatedReceived);
        }



        /// <summary>OnPartSelected / OnPartUpdated로 받은 JSON의 requestId를 저장해 둔다. SendSceneLoaded / SendSceneUpdated가 이 값을 그대로 돌려준다.</summary>
        private static void CacheLastPartRequestId(string _json)
        {
            if (string.IsNullOrEmpty(_json))
            {
                return;
            }

            try
            {
                PartHeaderRoot header = JsonUtility.FromJson<PartHeaderRoot>(_json);
                if (header != null)
                {
                    LastPartRequestId = header.requestId;
                }
            }
            catch (Exception e)
            {
                DebugLogManager.GenerateErrorMessage<ReactBridge>($"[ReactBridge] OnPartSelected requestId 읽기 실패: {e.Message}");
            }
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
        private static extern void OnUnityReady(string message);
        [DllImport("__Internal")]
        private static extern void OnSceneLoaded(string message);
        [DllImport("__Internal")]
        private static extern void OnSceneUpdated(string message);
#endif
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

        /// <summary>
        /// Unity → React: 씬 로딩 완료 시 호출. JSON {"isLoaded":true,"requestId":N} 전달.
        /// requestId는 가장 최근 OnPartSelected로 받은 값을 그대로 돌려준다.
        /// </summary>
        public static void SendSceneLoaded()
        {
            string payload = JsonUtility.ToJson(new SceneLoadedPayload { isLoaded = true, requestId = LastPartRequestId });
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

        /// <summary>
        /// Unity → React: 오브젝트 Setting 이후 호출. JSON {"isUpdated":true,"requestId":N} 전달.
        /// requestId는 가장 최근 OnPartSelected로 받은 값을 그대로 돌려준다.
        /// </summary>
        public static void SendSceneUpdated()
        {
            string payload = JsonUtility.ToJson(new SceneUpdatedPayload { isUpdated = true, requestId = LastPartRequestId });
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
