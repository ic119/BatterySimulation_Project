using Incheol.Modules;
using NUnit.Framework;
using UnityEngine;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Tests
{
    public class CreateParentResolverTests : FactoryTestBase
    {
        private GameObject packPrefab;
        private GameObject modulePrefab;
        private GameObject cellPrefab;
        private DeviceSelection selection;

        [SetUp]
        public void SetUp()
        {
            packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(1, 1, 1));
            cellPrefab = CreateCellPrefab("CellPrefab", new Vector3(0.5f, 0.5f, 0.5f));
            selection = new DeviceSelection();
        }

        private CreateParentResolver.Result Resolve(DeviceType _type, System.Func<ContainerDataModel, string> _fullCheck = null)
        {
            return CreateParentResolver.Resolve(_type, selection, factory, _fullCheck);
        }

        #region 선택 없음
        [Test]
        public void NoSelection_UsesTheLatestPackAndModule()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            ModuleDataModel module = factory.CreateModule(pack, "1", modulePrefab);

            CreateParentResolver.Result result = Resolve(DeviceType.Cell);

            Assert.IsTrue(result.success);
            Assert.AreSame(pack, result.parentPack);
            Assert.AreSame(module, result.parentModule);
            Assert.IsNull(result.parentContainer);
        }

        [Test]
        public void NullSelection_BehavesLikeNoSelection()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);

            CreateParentResolver.Result result = CreateParentResolver.Resolve(DeviceType.Module, null, factory);

            Assert.IsTrue(result.success);
            Assert.AreSame(pack, result.parentPack);
        }

        [Test]
        public void SelectionOfAnUnknownModel_IsIgnored()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            GameObject stranger = new GameObject("Stranger");
            selection.Set(new PackDataModel { packName = "x" }, stranger.transform);

            CreateParentResolver.Result result = Resolve(DeviceType.Module);
            Object.DestroyImmediate(stranger);

            Assert.IsTrue(result.success);
            Assert.AreSame(pack, result.parentPack);
        }
        #endregion

        #region Container 선택
        [Test]
        public void Container_Pack_UsesTheSelectedContainer()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            selection.Set(pack.parentContainer, pack.parentContainer.containerPosition);

            CreateParentResolver.Result result = Resolve(DeviceType.Pack, _container => null);

            Assert.IsTrue(result.success);
            Assert.AreSame(pack.parentContainer, result.parentContainer);
        }

        [Test]
        public void Container_Pack_WhenFull_FailsWithAlarm()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            selection.Set(pack.parentContainer, pack.parentContainer.containerPosition);

            CreateParentResolver.Result result = Resolve(DeviceType.Pack, _container => "가득 참");

            Assert.IsFalse(result.success);
            Assert.AreEqual("가득 참", result.message);
            Assert.IsTrue(result.showAlarm);
        }

        [Test]
        public void Container_Pack_WithoutFullCheck_Succeeds()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            selection.Set(pack.parentContainer, pack.parentContainer.containerPosition);

            Assert.IsTrue(Resolve(DeviceType.Pack).success);
        }

        [Test]
        public void Container_Module_UsesTheLastPackOfThatContainer()
        {
            factory.CreatePack("1", packPrefab);
            PackDataModel second = factory.CreatePack("2", packPrefab);
            selection.Set(second.parentContainer, second.parentContainer.containerPosition);

            CreateParentResolver.Result result = Resolve(DeviceType.Module);

            Assert.IsTrue(result.success);
            Assert.AreSame(second, result.parentPack);
            Assert.AreSame(second.parentContainer, result.parentContainer);
        }

        [Test]
        public void Container_Module_WithoutPack_MakesAStandaloneModule()
        {
            PackDataModel latest = factory.CreatePack("1", packPrefab);
            ContainerDataModel empty = factory.CreateContainer("empty");
            selection.Set(empty, empty.containerPosition);

            CreateParentResolver.Result result = Resolve(DeviceType.Module);

            Assert.IsTrue(result.success);
            Assert.IsNull(result.parentPack, "선택한 Container에 Pack이 없으면 최근 Pack이 아니라 단독 Module이어야 한다.");
            Assert.AreSame(empty, result.parentContainer);
            Assert.AreNotSame(latest, result.parentPack);
        }

        [Test]
        public void Container_Cell_FailsWithoutAlarm()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            selection.Set(pack.parentContainer, pack.parentContainer.containerPosition);

            CreateParentResolver.Result result = Resolve(DeviceType.Cell);

            Assert.IsFalse(result.success);
            Assert.IsFalse(result.showAlarm);
            Assert.IsNotEmpty(result.message);
        }
        #endregion

        #region Pack 선택
        [Test]
        public void Pack_Module_UsesTheSelectedPack()
        {
            factory.CreatePack("1", packPrefab);
            PackDataModel selected = factory.CreatePack("2", packPrefab);
            factory.CreatePack("3", packPrefab);
            selection.Set(selected, factory.GetPackTransform(selected));

            CreateParentResolver.Result result = Resolve(DeviceType.Module);

            Assert.IsTrue(result.success);
            Assert.AreSame(selected, result.parentPack);
        }

        [Test]
        public void Pack_Cell_UsesTheLastModuleOfThatPack()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            factory.CreateModule(pack, "1", modulePrefab);
            ModuleDataModel last = factory.CreateModule(pack, "2", modulePrefab);
            selection.Set(pack, factory.GetPackTransform(pack));

            CreateParentResolver.Result result = Resolve(DeviceType.Cell);

            Assert.IsTrue(result.success);
            Assert.AreSame(last, result.parentModule);
        }

        [Test]
        public void Pack_Cell_WithoutModule_Fails()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            selection.Set(pack, factory.GetPackTransform(pack));

            CreateParentResolver.Result result = Resolve(DeviceType.Cell);

            Assert.IsFalse(result.success);
            Assert.IsFalse(result.showAlarm);
        }

        [Test]
        public void Pack_Pack_Fails()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            selection.Set(pack, factory.GetPackTransform(pack));

            CreateParentResolver.Result result = Resolve(DeviceType.Pack);

            Assert.IsFalse(result.success);
            Assert.IsFalse(result.showAlarm);
        }
        #endregion

        #region Module 선택
        [Test]
        public void Module_Cell_UsesTheSelectedModule()
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            ModuleDataModel selected = factory.CreateModule(pack, "1", modulePrefab);
            factory.CreateModule(pack, "2", modulePrefab);
            selection.Set(selected, selected.modulePosition);

            CreateParentResolver.Result result = Resolve(DeviceType.Cell);

            Assert.IsTrue(result.success);
            Assert.AreSame(selected, result.parentModule);
        }

        [TestCase(DeviceType.Pack)]
        [TestCase(DeviceType.Module)]
        public void Module_PackOrModule_FailsAndNamesTheType(DeviceType _type)
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            ModuleDataModel module = factory.CreateModule(pack, "1", modulePrefab);
            selection.Set(module, module.modulePosition);

            CreateParentResolver.Result result = Resolve(_type);

            Assert.IsFalse(result.success);
            Assert.IsFalse(result.showAlarm);
            StringAssert.Contains(_type.ToString(), result.message);
        }
        #endregion

        #region Cell 선택
        [TestCase(DeviceType.Pack)]
        [TestCase(DeviceType.Module)]
        [TestCase(DeviceType.Cell)]
        public void Cell_Anything_Fails(DeviceType _type)
        {
            PackDataModel pack = factory.CreatePack("1", packPrefab);
            ModuleDataModel module = factory.CreateModule(pack, "1", modulePrefab);
            CellDataModel cell = factory.CreateCell(module, "1", cellPrefab);
            selection.Set(cell, cell.cellPosition);

            CreateParentResolver.Result result = Resolve(_type);

            Assert.IsFalse(result.success);
            Assert.IsFalse(result.showAlarm);
        }
        #endregion
    }
}
