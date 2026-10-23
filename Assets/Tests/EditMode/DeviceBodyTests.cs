using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Incheol.Tests
{
    public class DeviceBodyTests
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

        private GameObject NewBox(string _name, Vector3 _size, Transform _parent = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(box.GetComponent<Collider>());
            box.name = _name;
            box.transform.SetParent(_parent, false);
            box.transform.localScale = _size;
            createdList.Add(box);
            return box;
        }

        /// <summary>빈 루트 아래에 "Body" 상자가 있는 Pack / Module 형태.</summary>
        private Transform NewBodyRoot(Vector3 _size)
        {
            GameObject root = NewEmpty("Root");
            NewBox("Body", _size, root.transform);
            return root.transform;
        }

        #region TryGetRenderer
        [Test]
        public void TryGetRenderer_PrefersTheBodyChild()
        {
            GameObject root = NewBox("Root", Vector3.one * 5.0f);
            GameObject body = NewBox("Body", Vector3.one, root.transform);

            Assert.IsTrue(DeviceBody.TryGetRenderer(root.transform, out Renderer renderer));

            Assert.AreSame(body.GetComponent<Renderer>(), renderer);
        }

        [Test]
        public void TryGetRenderer_FallsBackToItself()
        {
            GameObject cell = NewBox("Cell", Vector3.one);

            Assert.IsTrue(DeviceBody.TryGetRenderer(cell.transform, out Renderer renderer));

            Assert.AreSame(cell.GetComponent<Renderer>(), renderer);
        }

        [Test]
        public void TryGetRenderer_BodyChildWithoutRenderer_FallsBackToItself()
        {
            GameObject root = NewBox("Root", Vector3.one);
            GameObject emptyBody = new GameObject(DeviceBody.BodyChildName);
            emptyBody.transform.SetParent(root.transform, false);

            Assert.IsTrue(DeviceBody.TryGetRenderer(root.transform, out Renderer renderer));

            Assert.AreSame(root.GetComponent<Renderer>(), renderer);
        }

        [Test]
        public void TryGetRenderer_IgnoresOtherChildren()
        {
            GameObject root = NewEmpty("Root");
            NewBox("Module", Vector3.one, root.transform);

            Assert.IsFalse(DeviceBody.TryGetRenderer(root.transform, out Renderer renderer));

            Assert.IsNull(renderer);
        }
        #endregion

        #region AttachKeepingOwnScale
        [Test]
        public void AttachKeepingOwnScale_KeepsTheWorldScale()
        {
            Transform parent = NewEmpty("Parent").transform;
            parent.localScale = new Vector3(4, 1, 2);
            Transform child = NewBox("Child", new Vector3(2, 2, 2)).transform;

            DeviceBody.AttachKeepingOwnScale(child, parent);

            Assert.AreSame(parent, child.parent);
            Assert.That(child.lossyScale, Is.EqualTo(new Vector3(2, 2, 2)).Using(VectorComparer));
        }

        [Test]
        public void AttachKeepingOwnScale_ZeroParentScaleAxis_DoesNotProduceNaN()
        {
            Transform parent = NewEmpty("Parent").transform;
            parent.localScale = new Vector3(0, 1, 1);
            Transform child = NewBox("Child", new Vector3(2, 2, 2)).transform;

            DeviceBody.AttachKeepingOwnScale(child, parent);

            Assert.IsFalse(float.IsNaN(child.localScale.x) || float.IsInfinity(child.localScale.x));
            Assert.AreEqual(2.0f, child.localScale.x, Tolerance, "나눌 수 없는 축은 값을 그대로 둔다.");
        }
        #endregion

        #region Resize
        [Test]
        public void Resize_BodyChild_KeepsTheLeftBottomBackCorner()
        {
            Transform root = NewBodyRoot(new Vector3(2, 1, 2));
            Bounds before = root.GetComponentInChildren<Renderer>().bounds;

            DeviceBody.Resize(root, new Vector3(4, 3, 5));

            Bounds after = root.GetComponentInChildren<Renderer>().bounds;
            Assert.That(after.size, Is.EqualTo(new Vector3(4, 3, 5)).Using(VectorComparer));
            Assert.AreEqual(before.min.x, after.min.x, Tolerance);
            Assert.AreEqual(before.min.y, after.min.y, Tolerance);
            Assert.AreEqual(before.max.z, after.max.z, Tolerance);
        }

        [Test]
        public void Resize_BodyChild_UsesWorldSizeUnderAScaledParent()
        {
            GameObject scaled = NewEmpty("Scaled");
            scaled.transform.localScale = new Vector3(2, 2, 2);
            GameObject root = NewEmpty("Root");
            root.transform.SetParent(scaled.transform, false);
            NewBox("Body", Vector3.one, root.transform);

            DeviceBody.Resize(root.transform, new Vector3(4, 4, 4));

            Assert.That(root.GetComponentInChildren<Renderer>().bounds.size, Is.EqualTo(new Vector3(4, 4, 4)).Using(VectorComparer), "크기는 부모 스케일을 감안한 월드 크기다.");
        }

        [Test]
        public void Resize_OwnRenderer_KeepsTheCenter()
        {
            Transform cell = NewBox("Cell", new Vector3(1, 1, 1)).transform;
            cell.position = new Vector3(3, 2, 1);

            DeviceBody.Resize(cell, new Vector3(2, 4, 6));

            Bounds after = cell.GetComponent<Renderer>().bounds;
            Assert.That(after.size, Is.EqualTo(new Vector3(2, 4, 6)).Using(VectorComparer));
            Assert.That(after.center, Is.EqualTo(new Vector3(3, 2, 1)).Using(VectorComparer));
        }

        [Test]
        public void Resize_NonPositiveAxis_ChangesNothing()
        {
            Transform root = NewBodyRoot(new Vector3(2, 1, 2));
            Vector3 before = root.GetComponentInChildren<Renderer>().bounds.size;

            DeviceBody.Resize(root, new Vector3(4, 0, 5));
            DeviceBody.Resize(root, new Vector3(-1, 1, 1));

            Assert.That(root.GetComponentInChildren<Renderer>().bounds.size, Is.EqualTo(before).Using(VectorComparer));
        }

        [Test]
        public void Resize_NullOrNoRenderer_DoesNothing()
        {
            Transform empty = NewEmpty("Empty").transform;

            Assert.DoesNotThrow(() => DeviceBody.Resize(null, Vector3.one));
            Assert.DoesNotThrow(() => DeviceBody.Resize(empty, Vector3.one));
            Assert.That(empty.localScale, Is.EqualTo(Vector3.one).Using(VectorComparer));
        }
        #endregion

        #region AlignCenter
        [Test]
        public void AlignCenter_MovesTheWholeObjectSoTheBodyCenterIsThere()
        {
            Transform root = NewBodyRoot(new Vector3(2, 1, 2));
            Vector3 target = new Vector3(5, 6, 7);

            DeviceBody.AlignCenter(root, target);

            Assert.That(root.GetComponentInChildren<Renderer>().bounds.center, Is.EqualTo(target).Using(VectorComparer));
        }

        [Test]
        public void AlignCenter_OwnRenderer_MovesIt()
        {
            Transform cell = NewBox("Cell", Vector3.one).transform;

            DeviceBody.AlignCenter(cell, new Vector3(-2, 0, 9));

            Assert.That(cell.GetComponent<Renderer>().bounds.center, Is.EqualTo(new Vector3(-2, 0, 9)).Using(VectorComparer));
        }

        [Test]
        public void AlignCenter_WithoutBody_DoesNotMove()
        {
            Transform empty = NewEmpty("Empty").transform;
            empty.position = new Vector3(1, 1, 1);

            DeviceBody.AlignCenter(empty, new Vector3(9, 9, 9));

            Assert.That(empty.position, Is.EqualTo(new Vector3(1, 1, 1)).Using(VectorComparer));
        }
        #endregion
    }
}
