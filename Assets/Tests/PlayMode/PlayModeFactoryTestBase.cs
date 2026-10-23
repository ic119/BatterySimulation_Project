using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools;

namespace Incheol.Tests
{
    /// <summary>
    /// 지우기(Remove* / Clear)를 시험하는 Play Mode 테스트의 공통 준비.
    /// PackFactory는 지울 때 Object.Destroy를 쓰는데 이것은 Play Mode에서만 쓸 수 있고, 실제로 사라지는 것은 프레임이 끝난 뒤이므로
    /// 테스트는 yield return null 로 한 프레임을 보낸 뒤 확인한다.
    /// </summary>
    public abstract class PlayModeFactoryTestBase
    {
        protected const float Tolerance = 0.001f;

        protected PackFactory factory;
        private readonly List<GameObject> prefabList = new List<GameObject>();

        [SetUp]
        public void BaseSetUp()
        {
            factory = new PackFactory();
        }

        [UnityTearDown]
        public IEnumerator BaseTearDown()
        {
            if (factory != null)
            {
                Object.Destroy(factory.Root.gameObject);
            }

            for (int i = 0; i < prefabList.Count; i++)
            {
                if (prefabList[i] != null)
                {
                    Object.Destroy(prefabList[i]);
                }
            }

            prefabList.Clear();
            yield return null;
        }

        /// <summary>실제 PackPrefab / ModulePrefab과 같은 구조: 빈 루트 아래에 _size 크기의 자식 "Body" 상자.</summary>
        protected GameObject CreateBodyPrefab(string _name, Vector3 _size)
        {
            GameObject root = new GameObject(_name);
            GameObject body = CreateBox("Body", _size);
            body.transform.SetParent(root.transform, false);
            prefabList.Add(root);
            return root;
        }

        /// <summary>실제 CellPrefab과 같은 구조: 루트 자신이 _size 크기의 상자(Renderer)이다.</summary>
        protected GameObject CreateCellPrefab(string _name, Vector3 _size)
        {
            GameObject cell = CreateBox(_name, _size);
            prefabList.Add(cell);
            return cell;
        }

        private static GameObject CreateBox(string _name, Vector3 _size)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = _name;
            box.transform.localScale = _size;
            Object.Destroy(box.GetComponent<Collider>());
            return box;
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

        protected Bounds PackBodyBounds(PackDataModel _pack)
        {
            Assert.IsTrue(PackLayout.TryGetPackBody(_pack, factory.GetPackTransform(_pack), out Renderer body), "Pack 몸체 Renderer를 찾지 못했다.");
            return body.bounds;
        }
    }
}
