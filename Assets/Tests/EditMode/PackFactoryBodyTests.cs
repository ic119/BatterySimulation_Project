using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Incheol.Tests
{
    /// <summary>
    /// 만들 때 크기를 지정하거나 저장된 몸체 중심을 주었을 때 몸체가 어떻게 놓이는지(PackFactory를 거친 결과).
    /// 크기를 바꾸는 규칙: Pack / Module은 자식 Body의 좌측 바닥 뒤쪽 모서리(min.x, min.y, max.z)를 고정한 채 +x, +y, -z 방향으로 늘고,
    /// Cell은 본인이 몸체라 중심을 유지한 채 바뀐다.
    /// </summary>
    public class PackFactoryBodyTests : FactoryTestBase
    {
        private static readonly Vector3EqualityComparer VectorComparer = new Vector3EqualityComparer(Tolerance);

        [Test]
        public void CreatePack_WithSize_KeepsTheLeftBottomBackCorner()
        {
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));

            PackDataModel pack = factory.CreatePack("1", packPrefab, new Vector3(4, 2, 6));

            Bounds bounds = PackBodyBounds(pack);
            Assert.That(bounds.size, Is.EqualTo(new Vector3(4, 2, 6)).Using(VectorComparer));
            Assert.AreEqual(-1.0f, bounds.min.x, Tolerance, "min.x는 그대로여야 한다.");
            Assert.AreEqual(-0.5f, bounds.min.y, Tolerance, "min.y는 그대로여야 한다.");
            Assert.AreEqual(1.0f, bounds.max.z, Tolerance, "max.z는 그대로여야 한다(크기는 -z 방향으로 늘어난다).");
        }

        [Test]
        public void CreatePack_WithNonPositiveSizeAxis_KeepsThePrefabSize()
        {
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));

            PackDataModel pack = factory.CreatePack("1", packPrefab, new Vector3(4, 0, 6));

            Assert.That(PackBodyBounds(pack).size, Is.EqualTo(new Vector3(2, 1, 2)).Using(VectorComparer), "한 축이라도 0 이하면 크기를 지정하지 않은 것으로 본다.");
        }

        [Test]
        public void CreatePack_WithBodyCenter_PutsTheBodyThere()
        {
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            Vector3 center = new Vector3(10, 3, -7);

            PackDataModel pack = factory.CreatePack("1", packPrefab, default, null, 0, 0, -1, -1, center);

            Assert.That(PackBodyBounds(pack).center, Is.EqualTo(center).Using(VectorComparer));
        }

        [Test]
        public void CreateModule_WithSizeAndBodyCenter_PutsTheSizedBodyThere()
        {
            GameObject modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(1, 1, 1));
            Vector3 center = new Vector3(4, 2, 5);

            ModuleDataModel module = factory.CreateModule(null, "1", modulePrefab, new Vector3(3, 2, 4), null, center);

            Bounds bounds = RendererBounds(module.modulePosition);
            Assert.That(bounds.size, Is.EqualTo(new Vector3(3, 2, 4)).Using(VectorComparer));
            Assert.That(bounds.center, Is.EqualTo(center).Using(VectorComparer));
        }

        [Test]
        public void CreateCell_WithSizeAndBodyCenter_ResizesTheCellItself()
        {
            GameObject modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(4, 4, 4));
            GameObject cellPrefab = CreateCellPrefab("CellPrefab", new Vector3(0.5f, 0.5f, 0.5f));
            ModuleDataModel module = factory.CreateModule(null, "1", modulePrefab);
            Vector3 center = new Vector3(0.5f, 0.5f, 0.5f);

            CellDataModel cell = factory.CreateCell(module, "1", cellPrefab, new Vector3(1, 1, 1), center);

            Bounds bounds = cell.cellPosition.GetComponent<Renderer>().bounds;
            Assert.That(bounds.size, Is.EqualTo(new Vector3(1, 1, 1)).Using(VectorComparer));
            Assert.That(bounds.center, Is.EqualTo(center).Using(VectorComparer));
        }

        [Test]
        public void CreatePack_UnderAScaledContainer_KeepsItsOwnWorldScale()
        {
            // 부모(Container)의 크기가 곱해지지 않고 프리팹 본래 크기가 월드 크기가 된다.
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            ContainerDataModel container = factory.CreateContainer("1");
            container.containerPosition.localScale = new Vector3(2, 3, 4);

            PackDataModel pack = factory.CreatePack("1", packPrefab, default, container);

            Assert.That(PackBodyBounds(pack).size, Is.EqualTo(new Vector3(2, 1, 2)).Using(VectorComparer));
        }
    }
}
