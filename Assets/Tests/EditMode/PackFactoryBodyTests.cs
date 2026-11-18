using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Tests
{
    /// <summary>
    /// 만들 때 크기를 지정하거나 저장된 몸체 중심을 주었을 때 몸체가 어떻게 놓이는지(PackFactory를 거친 결과).
    /// 크기를 바꾸는 규칙: 자식 Body를 가진 장치는 Body의 좌측 바닥 뒤쪽 모서리(min.x, min.y, max.z)를 고정한 채 +x, +y, -z 방향으로 늘고,
    /// Cell은 본인이 몸체라 중심을 유지한 채 바뀐다.
    /// </summary>
    public class PackFactoryBodyTests : FactoryTestBase
    {
        private static readonly Vector3EqualityComparer VectorComparer = new Vector3EqualityComparer(Tolerance);

        [SetUp]
        public void SetUp()
        {
            CreateStandardPrefabs();
        }

        [Test]
        public void CreatePack_WithSize_KeepsTheLeftBottomBackCorner()
        {
            IDeviceNode pack = Make(DeviceType.pack, null, "1", new Vector3(8, 2, 6));

            Bounds bounds = BodyBounds(pack);
            Assert.That(bounds.size, Is.EqualTo(new Vector3(8, 2, 6)).Using(VectorComparer));
            Assert.AreEqual(-2.0f, bounds.min.x, Tolerance, "min.x는 그대로여야 한다.");
            Assert.AreEqual(-0.5f, bounds.min.y, Tolerance, "min.y는 그대로여야 한다.");
            Assert.AreEqual(2.0f, bounds.max.z, Tolerance, "max.z는 그대로여야 한다(크기는 -z 방향으로 늘어난다).");
        }

        [Test]
        public void CreatePack_WithNonPositiveSizeAxis_KeepsThePrefabSize()
        {
            IDeviceNode pack = Make(DeviceType.pack, null, "1", new Vector3(4, 0, 6));

            Assert.That(BodyBounds(pack).size, Is.EqualTo(new Vector3(4, 1, 4)).Using(VectorComparer), "한 축이라도 0 이하면 크기를 지정하지 않은 것으로 본다.");
        }

        [Test]
        public void CreatePack_WithBodyCenter_PutsTheBodyThere()
        {
            Vector3 center = new Vector3(10, 3, -7);

            IDeviceNode pack = factory.Create(DeviceType.pack, null, "1", prefabs[DeviceType.pack], default, center);

            Assert.That(BodyBounds(pack).center, Is.EqualTo(center).Using(VectorComparer));
        }

        [Test]
        public void CreateModuleAssembly_WithSizeAndBodyCenter_PutsTheSizedBodyThere()
        {
            IDeviceNode pack = Make(DeviceType.pack, null, "1", new Vector3(20, 4, 20));
            // 20 x 4 x 20 Pack 몸체는 x -2~18, y -0.5~3.5, z -18~2 이므로, 3 x 2 x 4 몸체가 이 중심에서 안에 들어간다.
            Vector3 center = new Vector3(1, 1, -3);

            IDeviceNode moduleAssembly = factory.Create(DeviceType.moduleassembly, pack, "1", prefabs[DeviceType.moduleassembly], new Vector3(3, 2, 4), center);

            Bounds bounds = RendererBounds(moduleAssembly.Position);
            Assert.That(bounds.size, Is.EqualTo(new Vector3(3, 2, 4)).Using(VectorComparer));
            Assert.That(bounds.center, Is.EqualTo(center).Using(VectorComparer));
        }

        [Test]
        public void CreateCell_WithSizeAndBodyCenter_ResizesTheCellItself()
        {
            IDeviceNode pack = Make(DeviceType.pack, null, "1", new Vector3(20, 4, 20));
            IDeviceNode moduleAssembly = Make(DeviceType.moduleassembly, pack, "1", new Vector3(10, 4, 10));
            IDeviceNode module = Make(DeviceType.module, moduleAssembly, "1", new Vector3(8, 4, 8));
            IDeviceNode parallelAssembly = Make(DeviceType.parallelassembly, module, "1", new Vector3(6, 4, 6));
            Vector3 center = new Vector3(0.5f, 0.5f, 0.5f);

            IDeviceNode cell = factory.Create(DeviceType.cell, parallelAssembly, "1", prefabs[DeviceType.cell], new Vector3(1, 1, 1), center);

            Bounds bounds = cell.Position.GetComponent<Renderer>().bounds;
            Assert.That(bounds.size, Is.EqualTo(new Vector3(1, 1, 1)).Using(VectorComparer));
            Assert.That(bounds.center, Is.EqualTo(center).Using(VectorComparer));
        }

        [Test]
        public void CreatePack_UnderAScaledRoot_KeepsItsOwnWorldScale()
        {
            // 부모(Root)의 크기가 곱해지지 않고 프리팹 본래 크기가 월드 크기가 된다.
            factory.Root.localScale = new Vector3(2, 3, 4);

            IDeviceNode pack = Make(DeviceType.pack, null, "1");

            Assert.That(BodyBounds(pack).size, Is.EqualTo(new Vector3(4, 1, 4)).Using(VectorComparer));
        }
    }
}
