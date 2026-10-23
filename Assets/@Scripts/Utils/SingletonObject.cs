using UnityEngine;

namespace Incheol.Utils
{
    /// <summary>
    /// 씬에 두지 않아도 처음 Instance를 부를 때 스스로 만들어지는(lazy) 싱글톤의 기반 클래스. 
    /// 이 프로젝트의 매니저들은 BootstrapScene에 미리 두지 않고 이 방식으로 만든다.
    /// T는 반드시 자기 자신(class Foo : SingletonObject&lt;Foo&gt;)이어야 한다. 제약으로 컴파일 때 막으므로 Awake의 `this as T`가 조용히 null이 되는 일은 없다.
    /// </summary>
    public class SingletonObject<T> : MonoBehaviour where T : SingletonObject<T>
    {
        private static T instance;

        /// <summary>
        /// 애플리케이션 종료(에디터에서는 Play 모드 종료 포함) 중에는 true로 바뀐다.
        /// 다른 오브젝트의 OnDestroy/OnDisable에서 SomeSingleton.Instance != null 같은 식으로 체크하다가
        /// 종료 직전에 새 인스턴스를 만들어버리는 문제를 막기 위해, 이 상태에서는 Instance가 null을 반환한다.
        /// </summary>
        private static bool applicationIsQuitting;

        public static T Instance
        {
            get
            {
                if (applicationIsQuitting)
                {
                    return null;
                }

                if (instance == null)
                {
                    instance = GameObject.FindAnyObjectByType<T>();

                    if (instance == null)
                    {
                        GameObject go = new GameObject();
                        instance = go.AddComponent<T>();
                        instance.name = typeof(T).Name;

                        // 이 프로젝트에서는 의도된 동작이라 경고가 아니라 기록만 남긴다. 어떤 매니저가 언제 만들어졌는지(생성 순서)를 추적하는 용도다.
                        DebugLogManager.GenerateLogMessage<T>($"씬에 없어 자동 생성했습니다. (씬 전환 유지 : {instance.PersistAcrossScenes})");
                    }
                }
                return instance;
            }
        }

        /// <summary>
        /// true로 재정의하면 씬이 전환되어도 파괴되지 않고 유지된다. (DontDestroyOnLoad)
        /// </summary>
        protected virtual bool PersistAcrossScenes => false;

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                // 이미 살아 있는 인스턴스가 있는데 같은 매니저가 또 생기는 것은 씬에 중복 배치했거나 씬을 다시 불러온 경우라 알아챌 수 있게 경고한다.
                DebugLogManager.GenerateWarningMessage<T>($"이미 인스턴스가 있어 중복된 오브젝트({gameObject.name})를 파괴합니다.");
                Destroy(gameObject);
                return;
            }

            instance = this as T;

            if (PersistAcrossScenes)
            {
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
            }
        }

        protected virtual void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
        }
    }
}
