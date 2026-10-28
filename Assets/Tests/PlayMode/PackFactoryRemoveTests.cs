using NUnit.Framework;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;

namespace Incheol.Tests
{
    public class PackFactoryRemoveTests : PlayModeFactoryTestBase
    {
        private GameObject packPrefab;
        private GameObject modulePrefab;
        private GameObject cellPrefab;

        [SetUp]
        public void SetUp()
        {
            // Pack 2 x 1 x 2 에 Module 1 x 1 x 1 이 4개, Module 2 x 1 x 2 에 Cell 1 x 1 x 1 이 4개 들어간다.
            packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(1, 1, 1));
            cellPrefab = CreateCellPrefab("CellPrefab", new Vector3(1, 1, 1));
        }

        private ModuleDataModel CreateModuleWithCells(PackDataModel _pack, string _name, int _cellCount)
        {
            GameObject bigModulePrefab = CreateBodyPrefab("BigModulePrefab", new Vector3(2, 1, 2));
            ModuleDataModel module = factory.CreateModule(_pack, _name, _pack == null ? bigModulePrefab : modulePrefab);
            Assert.IsNotNull(module);
            for (int i = 0; i < _cellCount; i++)
            {
                Assert.IsNotNull(factory.CreateCell(module, (i + 1).ToString(), cellPrefab));
            }

            return module;
        }

        #region Remove
        [Test]
        public void Remove_DispatchesByKind()
        {
            ModuleDataModel module = CreateModuleWithCells(null, "1", 2);
            CellDataModel cell = module.cellsList[0];

            Assert.IsTrue(factory.Remove(cell));
            Assert.AreEqual(1, module.cellsList.Count);

            Assert.IsTrue(factory.Remove(module));
            Assert.IsFalse(factory.Remove(null));
        }
        #endregion

        #region RemoveCell
        [UnityTest]
        public IEnumerator RemoveCell_RemovesFromModuleAndDestroysObject()
        {
            ModuleDataModel module = CreateModuleWithCells(null, "1", 2);
            CellDataModel cell = module.cellsList[0];
            GameObject cellObject = cell.cellPosition.gameObject;

            Assert.IsTrue(factory.RemoveCell(cell));

            Assert.AreEqual(1, module.cellsList.Count);
            Assert.IsFalse(module.cellsList.Contains(cell));
            yield return null;
            Assert.IsTrue(cellObject == null, "Cell 오브젝트가 프레임 뒤에 파괴되어야 한다.");
        }

        [Test]
        public void RemoveCell_IsDetachedAndHiddenImmediately()
        {
            ModuleDataModel module = CreateModuleWithCells(null, "1", 1);
            Transform cellTransform = module.cellsList[0].cellPosition;

            factory.RemoveCell(module.cellsList[0]);

            // Destroy는 프레임 끝에야 반영되므로, 같은 프레임의 Bounds 계산에 섞이지 않게 먼저 떼어 내고 꺼 둔다.
            Assert.IsNull(cellTransform.parent);
            Assert.IsFalse(cellTransform.gameObject.activeSelf);
            Assert.AreEqual(0, PackLayout.GetOccupiedBounds(module).Count);
        }

        [Test]
        public void RemoveCell_SecondTimeOrNull_ReturnsFalse()
        {
            ModuleDataModel module = CreateModuleWithCells(null, "1", 1);
            CellDataModel cell = module.cellsList[0];

            Assert.IsTrue(factory.RemoveCell(cell));
            Assert.IsFalse(factory.RemoveCell(cell));
            Assert.IsFalse(factory.RemoveCell(null));
            Assert.IsFalse(factory.RemoveCell(new CellDataModel { cellName = "x" }));
        }

        [Test]
        public void RemoveCell_FreesItsSlotInTheSameFrame()
        {
            ModuleDataModel module = CreateModuleWithCells(null, "1", 4);
            Vector3 removedCenter = RendererBounds(module.cellsList[2].cellPosition).center;

            factory.RemoveCell(module.cellsList[2]);
            CellDataModel replacement = factory.CreateCell(module, "5", cellPrefab);

            Assert.IsNotNull(replacement, "비워진 칸에 같은 프레임에도 다시 만들 수 있어야 한다.");
            Assert.AreEqual(removedCenter.x, RendererBounds(replacement.cellPosition).center.x, Tolerance);
            Assert.AreEqual(removedCenter.z, RendererBounds(replacement.cellPosition).center.z, Tolerance);
        }
        #endregion

        #region RemoveModule
        [UnityTest]
        public IEnumerator RemoveModule_InPack_RemovesCellsAndAllLists()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            ModuleDataModel module = CreateModuleWithCells(pack, "1", 0);
            ModuleDataModel other = factory.CreateModule(pack, "2", modulePrefab);
            GameObject moduleObject = module.modulePosition.gameObject;

            Assert.IsTrue(factory.RemoveModule(module));

            Assert.IsFalse(pack.modulesList.Contains(module));
            Assert.IsFalse(factory.Modules.Contains(module));
            Assert.IsTrue(factory.Modules.Contains(other), "다른 Module은 남아야 한다.");
            yield return null;
            Assert.IsTrue(moduleObject == null);
        }

        [UnityTest]
        public IEnumerator RemoveModule_DestroysItsCellObjects()
        {
            ModuleDataModel module = CreateModuleWithCells(null, "1", 2);
            GameObject cellObject = module.cellsList[0].cellPosition.gameObject;

            factory.RemoveModule(module);

            yield return null;
            Assert.IsTrue(cellObject == null, "Module 아래의 Cell 오브젝트도 함께 파괴되어야 한다.");
        }

        [Test]
        public void RemoveModule_Standalone_RemovesFromItsContainer()
        {
            ModuleDataModel module = CreateModuleWithCells(null, "1", 0);
            ContainerDataModel container = module.parentContainer;

            Assert.IsTrue(factory.RemoveModule(module));

            Assert.AreEqual(0, container.modulesList.Count);
            Assert.AreEqual(0, factory.Modules.Count);
        }

        [Test]
        public void RemoveModule_LastOne_DisablesCellCreation()
        {
            ModuleDataModel module = CreateModuleWithCells(null, "1", 0);
            Assert.IsTrue(factory.CanCreateCell);

            factory.RemoveModule(module);

            Assert.IsFalse(factory.CanCreateCell);
        }

        [Test]
        public void RemoveModule_UnknownOrTwice_ReturnsFalse()
        {
            ModuleDataModel module = CreateModuleWithCells(null, "1", 0);

            Assert.IsTrue(factory.RemoveModule(module));
            Assert.IsFalse(factory.RemoveModule(module));
            Assert.IsFalse(factory.RemoveModule(null));
        }

        [Test]
        public void RemoveModule_FreesItsSlotInTheSameFrame()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            for (int i = 0; i < 4; i++)
            {
                Assert.IsNotNull(factory.CreateModule(pack, (i + 1).ToString(), modulePrefab));
            }

            Vector3 removedCenter = RendererBounds(pack.modulesList[1].modulePosition).center;

            factory.RemoveModule(pack.modulesList[1]);
            ModuleDataModel replacement = factory.CreateModule(pack, "5", modulePrefab);

            Assert.IsNotNull(replacement, "비워진 칸에 같은 프레임에도 다시 만들 수 있어야 한다.");
            Vector3 center = RendererBounds(replacement.modulePosition).center;
            Assert.AreEqual(removedCenter.x, center.x, Tolerance);
            Assert.AreEqual(removedCenter.z, center.z, Tolerance);
        }
        #endregion

        #region RemovePack
        [UnityTest]
        public IEnumerator RemovePack_RemovesModulesCellsAndObjects()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            ModuleDataModel module = factory.CreateModule(pack, "1", modulePrefab);
            GameObject packObject = factory.GetPackTransform(pack).gameObject;
            GameObject moduleObject = module.modulePosition.gameObject;
            ContainerDataModel container = pack.parentContainer;

            Assert.IsTrue(factory.RemovePack(pack));

            Assert.AreEqual(0, factory.Packs.Count);
            Assert.AreEqual(0, factory.Modules.Count, "Pack 안의 Module도 팩토리에서 빠져야 한다.");
            Assert.AreEqual(0, container.packsList.Count);
            Assert.IsNull(factory.GetPackTransform(pack));
            yield return null;
            Assert.IsTrue(packObject == null);
            Assert.IsTrue(moduleObject == null, "Pack 아래의 Module 오브젝트도 함께 파괴되어야 한다.");
        }

        [Test]
        public void RemovePack_UnknownOrTwice_ReturnsFalse()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);

            Assert.IsTrue(factory.RemovePack(pack));
            Assert.IsFalse(factory.RemovePack(pack));
            Assert.IsFalse(factory.RemovePack(null));
            Assert.IsFalse(factory.RemovePack(new PackDataModel { packName = "x", modulesList = new System.Collections.Generic.List<ModuleDataModel>() }));
        }

        [Test]
        public void RemovePack_KeepsTheOtherPacksSlots()
        {
            ContainerDataModel container = factory.CreateContainer("1");
            PackDataModel a = factory.CreatePack("1", packPrefab, default, container, 3, 1);
            PackDataModel b = factory.CreatePack("2", packPrefab, default, container, 3, 1);
            PackDataModel c = factory.CreatePack("3", packPrefab, default, container, 3, 1);
            Bounds cBefore = PackBodyBounds(c);

            factory.RemovePack(b);

            Assert.AreEqual((0, 2), (a.slotColumn, c.slotColumn));
            Assert.AreEqual(cBefore.center.x, PackBodyBounds(c).center.x, Tolerance, "남은 Pack은 움직이지 않아야 한다.");
        }

        [Test]
        public void RemovePack_FreesItsGridSlot_NextPackTakesItInTheSameFrame()
        {
            factory.PackPadding = 1.0f;
            ContainerDataModel container = factory.CreateContainer("1");
            PackDataModel a = factory.CreatePack("1", packPrefab, default, container, 2, 1);
            factory.CreatePack("2", packPrefab, default, container, 2, 1);
            Bounds removedBounds = PackBodyBounds(a);

            factory.RemovePack(a);
            PackDataModel replacement = factory.CreatePack("3", packPrefab, default, container, 2, 1);

            Assert.AreEqual((0, 0), (replacement.slotColumn, replacement.slotRow), "비워진 첫 칸을 다시 채워야 한다.");
            Assert.AreEqual(removedBounds.min.x, PackBodyBounds(replacement).min.x, Tolerance);
            Assert.AreEqual(removedBounds.max.z, PackBodyBounds(replacement).max.z, Tolerance);
        }
        #endregion

        #region RemoveContainer / Clear
        [UnityTest]
        public IEnumerator RemoveContainer_RemovesPacksStandaloneModulesAndItself()
        {
            ContainerDataModel container = factory.CreateContainer("1");
            PackDataModel packA = factory.CreatePack("1", packPrefab, default, container, 2, 1);
            PackDataModel packB = factory.CreatePack("2", packPrefab, default, container, 2, 1);
            factory.CreateModule(packA, "1", modulePrefab);
            ModuleDataModel standalone = factory.CreateModule(null, "2", modulePrefab, default, container);
            GameObject containerObject = container.containerPosition.gameObject;
            GameObject packBObject = factory.GetPackTransform(packB).gameObject;
            GameObject standaloneObject = standalone.modulePosition.gameObject;

            // 다른 Container는 영향을 받지 않아야 한다.
            PackDataModel otherPack = factory.CreatePack("9", packPrefab);

            Assert.IsTrue(factory.RemoveContainer(container));

            Assert.AreEqual(1, factory.Containers.Count);
            Assert.AreEqual(1, factory.Packs.Count);
            Assert.AreSame(otherPack, factory.Packs[0]);
            Assert.AreEqual(0, factory.Modules.Count);
            yield return null;
            Assert.IsTrue(containerObject == null);
            Assert.IsTrue(packBObject == null);
            Assert.IsTrue(standaloneObject == null);
            Assert.IsNotNull(factory.GetPackTransform(otherPack));
        }

        [Test]
        public void RemoveContainer_UnknownOrTwice_ReturnsFalse()
        {
            ContainerDataModel container = factory.CreateContainer("1");

            Assert.IsTrue(factory.RemoveContainer(container));
            Assert.IsFalse(factory.RemoveContainer(container));
            Assert.IsFalse(factory.RemoveContainer(null));
        }

        [Test]
        public void RemoveContainer_FindContainerNoLongerFindsIt()
        {
            ContainerDataModel container = factory.CreateContainer("1");
            Transform containerTransform = container.containerPosition;

            factory.RemoveContainer(container);

            Assert.IsNull(factory.FindContainer(containerTransform));
        }

        [UnityTest]
        public IEnumerator Clear_DestroysRootAndResetsTheFactory()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            factory.CreateModule(pack, "1", modulePrefab);
            GameObject rootObject = factory.Root.gameObject;

            factory.Clear();

            Assert.AreEqual(0, factory.Containers.Count);
            Assert.AreEqual(0, factory.Packs.Count);
            Assert.AreEqual(0, factory.Modules.Count);
            Assert.IsFalse(factory.CanCreateCell);
            Assert.IsNull(factory.LastPack);
            Assert.IsNull(factory.LastModule);
            Assert.AreEqual(string.Empty, factory.LastFullMessage);
            yield return null;
            Assert.IsTrue(rootObject == null, "Root 오브젝트가 파괴되어야 한다.");

            // 비운 뒤에도 새로 만들 수 있고, 새 Root가 쓰인다.
            PackDataModel again = factory.CreatePack("1", packPrefab);
            Assert.IsNotNull(again);
            Assert.IsTrue(factory.Root.gameObject != rootObject);
            Assert.AreEqual(1, factory.Root.childCount);
        }

        [Test]
        public void Clear_OnEmptyFactory_DoesNothing()
        {
            Assert.DoesNotThrow(() => factory.Clear());
            Assert.AreEqual(0, factory.Containers.Count);
        }
        #endregion
    }
}
