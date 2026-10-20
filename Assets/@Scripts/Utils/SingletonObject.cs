using UnityEngine;

namespace Incheol.Utils
{
    public class SingletonObject<T> : MonoBehaviour where T: MonoBehaviour
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
