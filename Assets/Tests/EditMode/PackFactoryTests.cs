using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Incheol.Tests
{
    public class PackFactoryTests : FactoryTestBase
    {
        #region Pack Grid
        [Test]
        public void CreatePack_FillsGridSlotsRowByRow()
        {
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            factory.PackPadding = 1.0f;
            ContainerDataModel container = factory.CreateContainer("1");

            PackDataModel[] pack = new PackDataModel[4];
            for (int i = 0; i < pack.Length; i++)
            {
                pack[i] = factory.CreatePack((i + 1).ToString(), packPrefab, default, container, 2, 2);
            }

            // 첫 줄을 x 방향으로 채운 뒤 다음 줄로 넘어간다.
            Assert.AreEqual((0, 0), (pack[0].slotColumn, pack[0].slotRow));
            Assert.AreEqual((1, 0), (pack[1].slotColumn, pack[1].slotRow));
            Assert.AreEqual((0, 1), (pack[2].slotColumn, pack[2].slotRow));
            Assert.AreEqual((1, 1), (pack[3].slotColumn, pack[3].slotRow));

            Bounds b0 = PackBodyBounds(pack[0]);
            Bounds b1 = PackBodyBounds(pack[1]);
            Bounds b2 = PackBodyBounds(pack[2]);
            Bounds b3 = PackBodyBounds(pack[3]);

            // 같은 줄은 +x로 여백만큼 떨어지고, 다음 줄은 -z로 여백만큼 떨어진다.
            Assert.AreEqual(b0.max.x + 1.0f, b1.min.x, Tolerance);
            Assert.AreEqual(b0.max.z, b1.max.z, Tolerance);
            Assert.AreEqual(b0.min.x, b2.min.x, Tolerance);
            Assert.AreEqual(b0.min.z - 1.0f, b2.max.z, Tolerance);
            Assert.AreEqual(b1.min.x, b3.min.x, Tolerance);
            Assert.AreEqual(b2.max.z, b3.max.z, Tolerance);
        }

        [Test]
        public void CreatePack_WhenGridIsFull_DoesNotRecordSlot()
        {
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            ContainerDataModel container = factory.CreateContainer("1");

            PackDataModel first = factory.CreatePack("1", packPrefab, default, container, 1, 1);
            PackDataModel second = factory.CreatePack("2", packPrefab, default, container, 1, 1);

            Assert.AreEqual((0, 0), (first.slotColumn, first.slotRow));
            Assert.AreEqual((-1, -1), (second.slotColumn, second.slotRow));
        }

        [Test]
        public void CreatePack_WithRequestedSlot_UsesThatSlot()
        {
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            ContainerDataModel container = factory.CreateContainer("1");

            PackDataModel pack = factory.CreatePack("1", packPrefab, default, container, 3, 1, 2, 0);

            Assert.AreEqual((2, 0), (pack.slotColumn, pack.slotRow));
        }

        [Test]
        public void CreatePack_WithSize_ResizesBody()
        {
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));

            PackDataModel pack = factory.CreatePack("1", packPrefab, new Vector3(4, 2, 6));

            Vector3 size = PackBodyBounds(pack).size;
            Assert.AreEqual(4.0f, size.x, Tolerance);
            Assert.AreEqual(2.0f, size.y, Tolerance);
            Assert.AreEqual(6.0f, size.z, Tolerance);
        }
        #endregion

        #region Module Slots
        [Test]
        public void CreateModule_FillsPackUntilFull()
        {
            // Pack 내부 2 x 1 x 2 에 Module 1 x 1 x 1 은 4개까지 들어간다.
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            GameObject modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(1, 1, 1));
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            Bounds packBounds = PackBodyBounds(pack);

            for (int i = 0; i < 4; i++)
            {
                ModuleDataModel module = factory.CreateModule(pack, (i + 1).ToString(), modulePrefab);
                Assert.IsNotNull(module, $"{i + 1}번째 Module은 만들어져야 한다.");
                AssertInside(packBounds, RendererBounds(module.modulePosition), $"{i + 1}번째 Module이 Pack 밖에 놓였다.");
            }

            ExpectNoSpaceLogs();
            ModuleDataModel overflow = factory.CreateModule(pack, "5", modulePrefab);

            Assert.IsNull(overflow);
            Assert.IsNotEmpty(factory.LastFullMessage);
            Assert.AreEqual(4, pack.modulesList.Count);
        }

        [Test]
        public void CreateModule_ModulePaddingReducesCapacity()
        {
            // 여백 1이면 칸이 2 x 2 가 되어 Pack 하나에 Module은 1개뿐이다.
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            GameObject modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(1, 1, 1));
            factory.ModulePadding = 1.0f;
            PackDataModel pack = factory.CreatePack("1", packPrefab);

            Assert.IsNotNull(factory.CreateModule(pack, "1", modulePrefab));

            ExpectNoSpaceLogs();
            Assert.IsNull(factory.CreateModule(pack, "2", modulePrefab));
        }

        [Test]
        public void CreateModule_WithUnregisteredPack_ReturnsNull()
        {
            GameObject modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(1, 1, 1));
            PackDataModel stranger = new PackDataModel { packName = "x", modulesList = new System.Collections.Generic.List<ModuleDataModel>() };

            LogAssert.Expect(LogType.Error, new Regex("등록되지 않은 Pack"));
            Assert.IsNull(factory.CreateModule(stranger, "1", modulePrefab));
        }

        [Test]
        public void CreateModule_WithoutPack_GoesIntoContainer()
        {
            GameObject modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(1, 1, 1));

            ModuleDataModel module = factory.CreateModule(null, "1", modulePrefab);

            Assert.IsNotNull(module);
            Assert.IsNull(module.parentPack);
            Assert.IsNotNull(module.parentContainer);
            Assert.Contains(module, module.parentContainer.modulesList);
        }
        #endregion

        #region Cell Slots
        [Test]
        public void CreateCell_FillsModuleUntilFull_AndStaysInside()
        {
            GameObject modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(2, 1, 2));
            GameObject cellPrefab = CreateCellPrefab("CellPrefab", new Vector3(1, 1, 1));
            ModuleDataModel module = factory.CreateModule(null, "1", modulePrefab);

            for (int i = 0; i < 4; i++)
            {
                CellDataModel cell = factory.CreateCell(module, (i + 1).ToString(), cellPrefab);
                Assert.IsNotNull(cell, $"{i + 1}번째 Cell은 만들어져야 한다.");
                Assert.IsTrue(PackLayout.IsCellInsideModule(cell, out Vector3 overflow), $"{i + 1}번째 Cell이 Module 밖으로 {overflow} 벗어났다.");
            }

            ExpectNoSpaceLogs();
            Assert.IsNull(factory.CreateCell(module, "5", cellPrefab));
            Assert.AreEqual(4, module.cellsList.Count);
        }

        [Test]
        public void CreateCell_CellPaddingReducesCapacity()
        {
            // 여백 0.5면 칸이 1.5 x 1.5 라 2 x 2 Module에는 Cell이 1개뿐이다.
            GameObject modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(2, 1, 2));
            GameObject cellPrefab = CreateCellPrefab("CellPrefab", new Vector3(1, 1, 1));
            factory.CellPadding = 0.5f;
            ModuleDataModel module = factory.CreateModule(null, "1", modulePrefab);

            Assert.IsNotNull(factory.CreateCell(module, "1", cellPrefab));

            ExpectNoSpaceLogs();
            Assert.IsNull(factory.CreateCell(module, "2", cellPrefab));
        }

        [Test]
        public void CreateCell_WithoutModule_ReturnsNull()
        {
            GameObject cellPrefab = CreateCellPrefab("CellPrefab", new Vector3(1, 1, 1));

            LogAssert.Expect(LogType.Error, new Regex("Module이 있어야"));
            Assert.IsNull(factory.CreateCell(null, "1", cellPrefab));
            Assert.IsFalse(factory.CanCreateCell);
        }
        #endregion

        #region Find / Container
        [Test]
        public void FindPackModuleContainer_ByTransform()
        {
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            GameObject modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(1, 1, 1));
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            ModuleDataModel module = factory.CreateModule(pack, "1", modulePrefab);

            Assert.AreSame(pack, factory.FindPack(factory.GetPackTransform(pack)));
            Assert.AreSame(module, factory.FindModule(module.modulePosition));
            Assert.AreSame(pack.parentContainer, factory.FindContainer(pack.parentContainer.containerPosition));
            Assert.IsNull(factory.FindPack(module.modulePosition));
        }

        [Test]
        public void GetTransform_ReturnsTheSceneTransformOfEachKind()
        {
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            GameObject modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(1, 1, 1));
            GameObject cellPrefab = CreateCellPrefab("CellPrefab", new Vector3(0.5f, 0.5f, 0.5f));
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            ModuleDataModel module = factory.CreateModule(pack, "1", modulePrefab);
            CellDataModel cell = factory.CreateCell(module, "1", cellPrefab);

            Assert.AreSame(pack.parentContainer.containerPosition, factory.GetTransform(pack.parentContainer));
            Assert.AreSame(factory.GetPackTransform(pack), factory.GetTransform(pack));
            Assert.AreSame(module.modulePosition, factory.GetTransform(module));
            Assert.AreSame(cell.cellPosition, factory.GetTransform(cell));
            Assert.IsNull(factory.GetTransform(null));
        }

        [Test]
        public void CreatePack_WithoutContainer_CreatesOneContainerPerPack()
        {
            GameObject packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));

            PackDataModel a = factory.CreatePack("1", packPrefab);
            PackDataModel b = factory.CreatePack("2", packPrefab);

            Assert.AreEqual(2, factory.Containers.Count);
            Assert.AreNotSame(a.parentContainer, b.parentContainer);
            Assert.AreSame(b, factory.LastPack);
        }
        #endregion
    }
}
