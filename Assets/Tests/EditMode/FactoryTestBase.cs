using NUnit.Framework;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Incheol.Tests
{
    /// <summary>
    /// PackFactory를 쓰는 테스트의 공통 준비. Edit Mode에서는 Object.Destroy를 쓸 수 없으므로(PackFactory.Remove* / Clear는 Destroy를 쓴다)
    /// 테스트에서는 지우기를 호출하지 않고, TearDown에서 DestroyImmediate로 만든 오브젝트를 모두 정리한다.
    /// </summary>
    public abstract class FactoryTestBase
    {
        protected const float Tolerance = 0.001f;

        protected PackFactory factory;
        private readonly List<GameObject> createdList = new List<GameObject>();
        private readonly HashSet<GameObject> rootsBeforeTest = new HashSet<GameObject>();

        [SetUp]
        public void BaseSetUp()
        {
            rootsBeforeTest.Clear();
            GameObject[] rootArray = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < rootArray.Length; i++)
            {
                rootsBeforeTest.Add(rootArray[i]);
            }

            factory = new PackFactory();
        }

        [TearDown]
        public void BaseTearDown()
        {
            if (factory != null)
            {
                Object.DestroyImmediate(factory.Root.gameObject);
            }

            // 놓을 곳이 없어 만들다 만 Module / Cell은 부모에서 떼어 낸 채 Destroy(Edit Mode에서는 지워지지 않음)되므로 씬 루트에 남는다. 테스트가 만든 것은 모두 치운다.
            GameObject[] rootArray = SceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < rootArray.Length; i++)
            {
                if (!rootsBeforeTest.Contains(rootArray[i]))
                {
                    Object.DestroyImmediate(rootArray[i]);
                }
            }

            for (int i = 0; i < createdList.Count; i++)
            {
                if (createdList[i] != null)
                {
                    Object.DestroyImmediate(createdList[i]);
                }
            }

            createdList.Clear();
        }

        /// <summary>실제 PackPrefab / ModulePrefab과 같은 구조: 빈 루트 아래에 _size 크기의 자식 "Body" 상자.</summary>
        protected GameObject CreateBodyPrefab(string _name, Vector3 _size)
        {
            GameObject root = new GameObject(_name);
            GameObject body = CreateBox("Body", _size);
            body.transform.SetParent(root.transform, false);
            createdList.Add(root);
            return root;
        }

        /// <summary>실제 CellPrefab과 같은 구조: 루트 자신이 _size 크기의 상자(Renderer)이다.</summary>
        protected GameObject CreateCellPrefab(string _name, Vector3 _size)
        {
            GameObject cell = CreateBox(_name, _size);
            createdList.Add(cell);
            return cell;
        }

        private static GameObject CreateBox(string _name, Vector3 _size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = _name;
            box.transform.localScale = _size;
            Object.DestroyImmediate(box.GetComponent<Collider>());
            return box;
        }

        /// <summary>
        /// 놓을 곳이 없어 Module / Cell 생성이 실패할 때 나오는 로그를 예상한다. 팩토리가 만들다 만 오브젝트를 Object.Destroy로 지우는데
        /// Edit Mode에서는 이것이 에러 로그를 남기므로(Play Mode에서는 정상) 함께 예상한다.
        /// </summary>
        protected static void ExpectNoSpaceLogs()
        {
            LogAssert.Expect(LogType.Error, new Regex("공간이 없습니다"));
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
        }

        protected Bounds PackBodyBounds(PackDataModel _pack)
        {
            Assert.IsTrue(PackLayout.TryGetPackBody(_pack, factory.GetPackTransform(_pack), out Renderer body), "Pack 몸체 Renderer를 찾지 못했다.");
            return body.bounds;
        }

        protected static Bounds RendererBounds(Transform _transform)
        {
            Renderer[] rendererArray = _transform.GetComponentsInChildren<Renderer>();
            Bounds bounds = rendererArray[0].bounds;
            for (int i = 1; i < rendererArray.Length; i++)
            {
                bounds.Encapsulate(rendererArray[i].bounds);
            }

            return bounds;
        }

        protected static void AssertInside(Bounds _outer, Bounds _inner, string _message)
        {
            bool inside =
                _inner.min.x >= _outer.min.x - Tolerance && _inner.max.x <= _outer.max.x + Tolerance &&
                _inner.min.y >= _outer.min.y - Tolerance && _inner.max.y <= _outer.max.y + Tolerance &&
                _inner.min.z >= _outer.min.z - Tolerance && _inner.max.z <= _outer.max.z + Tolerance;
            Assert.IsTrue(inside, $"{_message} (바깥 {_outer}, 안쪽 {_inner})");
        }
    }
}
