using Incheol.Modules;
using NUnit.Framework;
using UnityEngine;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Tests
{
    public class CreateParentResolverTests : FactoryTestBase
    {
        private DeviceSelection selection;

        // 한 줄 구조: Pack > ModuleAssembly > Module > ParallelAssembly > Cell
        private IDeviceNode pack;
        private IDeviceNode moduleAssembly;
        private IDeviceNode module;
        private IDeviceNode parallelAssembly;
        private IDeviceNode cell;

        [SetUp]
        public void SetUp()
        {
            CreateStandardPrefabs();
            selection = new DeviceSelection();
        }

        private void BuildChain()
        {
            pack = Make(DeviceType.pack, null, "1");
            moduleAssembly = Make(DeviceType.moduleassembly, pack, "1");
            module = Make(DeviceType.module, moduleAssembly, "1");
            parallelAssembly = Make(DeviceType.parallelassembly, module, "1");
            cell = Make(DeviceType.cell, parallelAssembly, "1");
        }

        private CreateParentResolver.Result Resolve(DeviceType _type)
        {
            return CreateParentResolver.Resolve(_type, selection, factory);
        }

        private void Select(IDeviceNode _node)
        {
            selection.Set(_node, _node.Position);
        }

        #region 선택 없음
        [Test]
        public void Pack_NeverHasAParent()
        {
            BuildChain();
            Select(cell);

            CreateParentResolver.Result result = Resolve(DeviceType.pack);

            Assert.IsTrue(result.success);
            Assert.IsNull(result.parent);
        }

        [Test]
        public void NoSelection_UsesTheLatestOfTheParentKind()
        {
            BuildChain();

            Assert.AreSame(pack, Resolve(DeviceType.moduleassembly).parent);
            Assert.AreSame(moduleAssembly, Resolve(DeviceType.module).parent);
            Assert.AreSame(module, Resolve(DeviceType.parallelassembly).parent);
            Assert.AreSame(parallelAssembly, Resolve(DeviceType.cell).parent);
        }

        [Test]
        public void NoSelection_WithoutAnyParent_Fails()
        {
            CreateParentResolver.Result result = Resolve(DeviceType.cell);

            Assert.IsFalse(result.success);
            Assert.IsNotEmpty(result.message);
        }

        [Test]
        public void NullSelection_BehavesLikeNoSelection()
        {
            BuildChain();

            CreateParentResolver.Result result = CreateParentResolver.Resolve(DeviceType.moduleassembly, null, factory);

            Assert.IsTrue(result.success);
            Assert.AreSame(pack, result.parent);
        }
        #endregion

        #region 선택한 항목이 바로 위 부모
        [Test]
        public void SelectedParentKind_IsTheParent()
        {
            BuildChain();
            IDeviceNode otherPack = Make(DeviceType.pack, null, "2");
            IDeviceNode otherModuleAssembly = Make(DeviceType.moduleassembly, otherPack, "1");
            Select(otherModuleAssembly);

            Assert.AreSame(otherModuleAssembly, Resolve(DeviceType.module).parent, "마지막으로 만든 것이 아니라 선택한 항목이 부모여야 한다.");
        }
        #endregion

        #region 선택한 항목이 조상
        [Test]
        public void SelectedAncestor_UsesTheLastChildDownTheChain()
        {
            BuildChain();
            Select(pack);

            Assert.AreSame(moduleAssembly, Resolve(DeviceType.module).parent);
            Assert.AreSame(module, Resolve(DeviceType.parallelassembly).parent);
            Assert.AreSame(parallelAssembly, Resolve(DeviceType.cell).parent);
        }

        [Test]
        public void SelectedAncestor_WithoutIntermediateChild_Fails()
        {
            pack = Make(DeviceType.pack, null, "1");
            Select(pack);

            CreateParentResolver.Result result = Resolve(DeviceType.cell);

            Assert.IsFalse(result.success);
            StringAssert.Contains("Pack", result.message);
            Assert.IsFalse(result.showAlarm);
        }
        #endregion

        #region 선택한 항목이 부모보다 아래
        [TestCase(DeviceType.cell, DeviceType.cell)]
        [TestCase(DeviceType.cell, DeviceType.module)]
        [TestCase(DeviceType.parallelassembly, DeviceType.module)]
        [TestCase(DeviceType.module, DeviceType.moduleassembly)]
        public void SelectedBelowTheParentKind_FailsAndNamesTheType(DeviceType _selected, DeviceType _create)
        {
            BuildChain();
            Select(FindOfKind(_selected));

            CreateParentResolver.Result result = Resolve(_create);

            Assert.IsFalse(result.success);
            StringAssert.Contains(_create.ToString(), result.message);
            StringAssert.Contains(_selected.ToString(), result.message);
        }

        private IDeviceNode FindOfKind(DeviceType _kind)
        {
            switch (_kind)
            {
                case DeviceType.pack: return pack;
                case DeviceType.moduleassembly: return moduleAssembly;
                case DeviceType.module: return module;
                case DeviceType.parallelassembly: return parallelAssembly;
                default: return cell;
            }
        }
        #endregion
    }
}
