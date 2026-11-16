using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Tests
{
    public class PackFactoryRemoveTests : PlayModeFactoryTestBase
    {
        private IDeviceNode pack;
        private IDeviceNode moduleAssembly;
        private IDeviceNode module;
        private IDeviceNode parallelAssembly;

        [SetUp]
        public void SetUp()
        {
            // Pack 4x1x4 > ModuleAssembly 2x1x2 (4개) > Module 1x1x1 (4개) > ParallelAssembly 1x1x1 (1개) > Cell 0.5x1x0.5 (4개)
            CreateStandardPrefabs();
            pack = Make(DeviceType.Pack, null, "1");
            moduleAssembly = Make(DeviceType.ModuleAssembly, pack, "1");
            module = Make(DeviceType.Module, moduleAssembly, "1");
            parallelAssembly = Make(DeviceType.ParallelAssembly, module, "1");
        }

        [Test]
        public void Remove_Cell_RemovesFromParentAndRegistry()
        {
            IDeviceNode cell = Make(DeviceType.Cell, parallelAssembly, "1");

            Assert.IsTrue(factory.Remove(cell));

            Assert.AreEqual(0, parallelAssembly.Children.Count);
            Assert.AreEqual(0, factory.GetNodes(DeviceType.Cell).Count);
            Assert.IsNull(factory.Find(cell.Position));
        }

        [UnityTest]
        public IEnumerator Remove_Cell_DestroysTheSceneObject()
        {
            IDeviceNode cell = Make(DeviceType.Cell, parallelAssembly, "1");
            GameObject cellObject = cell.Position.gameObject;

            factory.Remove(cell);
            yield return null;

            Assert.IsTrue(cellObject == null, "프레임이 끝나면 Cell 오브젝트가 사라져야 한다.");
        }

        [Test]
        public void Remove_Cell_IsDetachedAndHiddenImmediately()
        {
            IDeviceNode cell = Make(DeviceType.Cell, parallelAssembly, "1");
            Transform cellTransform = cell.Position;

            factory.Remove(cell);

            Assert.IsNull(cellTransform.parent, "Destroy는 프레임 끝에 반영되므로 먼저 부모에서 떼어 낸다.");
            Assert.IsFalse(cellTransform.gameObject.activeSelf);
        }

        [Test]
        public void Remove_Cell_FreesItsSlotInTheSameFrame()
        {
            IDeviceNode[] cells = new IDeviceNode[4];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = Make(DeviceType.Cell, parallelAssembly, (i + 1).ToString());
            }

            // 가득 찬 ParallelAssembly에서 하나를 지우면 같은 프레임에 새 Cell이 그 칸에 들어간다.
            Assert.IsTrue(factory.Remove(cells[1]));
            IDeviceNode replacement = Make(DeviceType.Cell, parallelAssembly, "5");

            Assert.IsNotNull(replacement);
            Assert.AreEqual(4, parallelAssembly.Children.Count);
        }

        [Test]
        public void Remove_UnknownOrTwiceOrNull_ReturnsFalse()
        {
            IDeviceNode cell = Make(DeviceType.Cell, parallelAssembly, "1");
            PackDataModel stranger = new PackDataModel { packName = "x" };

            Assert.IsTrue(factory.Remove(cell));
            Assert.IsFalse(factory.Remove(cell));
            Assert.IsFalse(factory.Remove(stranger));
            Assert.IsFalse(factory.Remove(null));
        }

        [UnityTest]
        public IEnumerator Remove_Module_RemovesEverythingBelowIt()
        {
            IDeviceNode cell = Make(DeviceType.Cell, parallelAssembly, "1");
            GameObject moduleObject = module.Position.gameObject;
            GameObject cellObject = cell.Position.gameObject;

            Assert.IsTrue(factory.Remove(module));
            yield return null;

            Assert.AreEqual(0, moduleAssembly.Children.Count);
            Assert.AreEqual(0, factory.GetNodes(DeviceType.Module).Count);
            Assert.AreEqual(0, factory.GetNodes(DeviceType.ParallelAssembly).Count);
            Assert.AreEqual(0, factory.GetNodes(DeviceType.Cell).Count);
            Assert.IsTrue(moduleObject == null);
            Assert.IsTrue(cellObject == null);
        }

        [Test]
        public void Remove_ModuleAssembly_KeepsItsSiblings()
        {
            IDeviceNode sibling = Make(DeviceType.ModuleAssembly, pack, "2");

            Assert.IsTrue(factory.Remove(moduleAssembly));

            CollectionAssert.AreEqual(new[] { sibling }, pack.Children);
            Assert.AreEqual(1, factory.GetNodes(DeviceType.ModuleAssembly).Count);
            Assert.AreEqual(0, factory.GetNodes(DeviceType.Module).Count, "지운 ModuleAssembly 아래 항목은 모두 등록부에서 빠져야 한다.");
        }

        [Test]
        public void Remove_ModuleAssembly_FreesItsSlotInTheSameFrame()
        {
            IDeviceNode second = Make(DeviceType.ModuleAssembly, pack, "2");
            Make(DeviceType.ModuleAssembly, pack, "3");
            Make(DeviceType.ModuleAssembly, pack, "4");

            Assert.IsTrue(factory.Remove(second));
            IDeviceNode replacement = Make(DeviceType.ModuleAssembly, pack, "5");

            Assert.IsNotNull(replacement);
            Assert.AreEqual(4, pack.Children.Count);
        }

        [UnityTest]
        public IEnumerator Remove_Pack_RemovesEverythingAndTheObject()
        {
            Make(DeviceType.Cell, parallelAssembly, "1");
            GameObject packObject = pack.Position.gameObject;

            Assert.IsTrue(factory.Remove(pack));
            yield return null;

            foreach (DeviceType kind in System.Enum.GetValues(typeof(DeviceType)))
            {
                Assert.AreEqual(0, factory.GetNodes(kind).Count, $"{kind}이(가) 등록부에 남았다.");
            }

            Assert.IsTrue(packObject == null);
        }

        [Test]
        public void Remove_Pack_FreesItsPlaceInRoot()
        {
            IDeviceNode second = Make(DeviceType.Pack, null, "2");
            Bounds firstBounds = BodyBounds(pack);

            Assert.IsTrue(factory.Remove(pack));
            IDeviceNode replacement = Make(DeviceType.Pack, null, "3");

            Assert.IsNotNull(replacement);
            Assert.AreNotSame(second, replacement);
            Assert.AreEqual(2, factory.GetNodes(DeviceType.Pack).Count);
            Assert.AreEqual(firstBounds.size, BodyBounds(replacement).size);
        }

        [UnityTest]
        public IEnumerator Clear_DestroysRootAndResetsTheFactory()
        {
            Make(DeviceType.Cell, parallelAssembly, "1");
            GameObject rootObject = factory.Root.gameObject;

            factory.Clear();
            yield return null;

            Assert.IsTrue(rootObject == null);
            foreach (DeviceType kind in System.Enum.GetValues(typeof(DeviceType)))
            {
                Assert.AreEqual(0, factory.GetNodes(kind).Count);
            }

            Assert.IsEmpty(factory.LastFullMessage);
        }

        [Test]
        public void Clear_OnEmptyFactory_DoesNothing()
        {
            PackFactory empty = new PackFactory();

            Assert.DoesNotThrow(() => empty.Clear());
        }
    }
}
