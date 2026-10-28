using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Incheol.Tests
{
    public class RendererBoundsTests
    {
        private const float Tolerance = 0.001f;
        private static readonly Vector3EqualityComparer VectorComparer = new Vector3EqualityComparer(Tolerance);
        private readonly List<GameObject> createdList = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdList.Count; i++)
            {
                if (createdList[i] != null)
                {
                    Object.DestroyImmediate(createdList[i]);
                }
            }

            createdList.Clear();
        }

        private GameObject NewEmpty(string _name)
        {
            GameObject empty = new GameObject(_name);
            createdList.Add(empty);
            return empty;
        }

        private GameObject NewBox(string _name, Vector3 _position, Transform _parent = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.name = _name;
            box.transform.SetParent(_parent, false);
            box.transform.position = _position;
            createdList.Add(box);
            return box;
        }

        [Test]
        public void TryGetBounds_MergesAllChildRenderers()
        {
            Transform root = NewEmpty("Root").transform;
            NewBox("A", new Vector3(0, 0, 0), root);
            NewBox("B", new Vector3(4, 0, 0), root);

            Assert.IsTrue(RendererBounds.TryGetBounds(root, out Bounds bounds));

            Assert.That(bounds.min, Is.EqualTo(new Vector3(-0.5f, -0.5f, -0.5f)).Using(VectorComparer));
            Assert.That(bounds.max, Is.EqualTo(new Vector3(4.5f, 0.5f, 0.5f)).Using(VectorComparer));
        }

        [Test]
        public void TryGetBounds_NoRenderer_ReturnsFalse()
        {
            Transform root = NewEmpty("Root").transform;

            Assert.IsFalse(RendererBounds.TryGetBounds(root, out _));
            Assert.IsFalse(RendererBounds.TryGetBounds(null, out _));
        }

        [Test]
        public void TryGetBounds_IncludeInactive_DecidesWhetherHiddenObjectsCount()
        {
            Transform root = NewEmpty("Root").transform;
            NewBox("Visible", Vector3.zero, root);
            GameObject hidden = NewBox("Hidden", new Vector3(10, 0, 0), root);
            hidden.SetActive(false);

            RendererBounds.TryGetBounds(root, out Bounds withHidden, true);
            RendererBounds.TryGetBounds(root, out Bounds withoutHidden, false);

            Assert.AreEqual(11.0f, withHidden.size.x, Tolerance);
            Assert.AreEqual(1.0f, withoutHidden.size.x, Tolerance);
        }

        [Test]
        public void TryGetBounds_Filter_SkipsRejectedRenderers()
        {
            Transform root = NewEmpty("Root").transform;
            NewBox("Keep", Vector3.zero, root);
            GameObject skip = NewBox("Skip", new Vector3(10, 0, 0), root);

            RendererBounds.TryGetBounds(root, out Bounds bounds, true, _renderer => _renderer.gameObject != skip);

            Assert.AreEqual(1.0f, bounds.size.x, Tolerance);
        }

        [Test]
        public void TryGetBounds_FilterRejectsEverything_ReturnsFalse()
        {
            Transform root = NewEmpty("Root").transform;
            NewBox("A", Vector3.zero, root);

            Assert.IsFalse(RendererBounds.TryGetBounds(root, out _, true, _renderer => false));
        }
    }
}
