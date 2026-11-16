using NUnit.Framework;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Tests
{
    public class PackFactoryTests : FactoryTestBase
    {
        [SetUp]
        public void SetUp()
        {
            CreateStandardPrefabs();
        }

        #region Pack
        [Test]
        public void CreatePack_PlacesPacksInOneRowWithPadding()
        {
            factory.SetPadding(DeviceType.Pack, 1.0f);

            IDeviceNode first = Make(DeviceType.Pack, null, "1");
            IDeviceNode second = Make(DeviceType.Pack, null, "2");
            IDeviceNode third = Make(DeviceType.Pack, null, "3");

            Bounds b0 = BodyBounds(first);
            Bounds b1 = BodyBounds(second);
            Bounds b2 = BodyBounds(third);

            // 같은 줄(+x 방향)에 여백만큼 떨어져 놓이고, z와 y는 그대로다.
            Assert.AreEqual(b0.max.x + 1.0f, b1.min.x, Tolerance);
            Assert.AreEqual(b1.max.x + 1.0f, b2.min.x, Tolerance);
            Assert.AreEqual(b0.max.z, b1.max.z, Tolerance);
            Assert.AreEqual(b0.min.y, b2.min.y, Tolerance);
        }

        [Test]
        public void CreatePack_WithSize_ResizesBody()
        {
            IDeviceNode pack = Make(DeviceType.Pack, null, "1", new Vector3(8, 2, 6));

            Vector3 size = BodyBounds(pack).size;
            Assert.AreEqual(8.0f, size.x, Tolerance);
            Assert.AreEqual(2.0f, size.y, Tolerance);
            Assert.AreEqual(6.0f, size.z, Tolerance);
        }

        [Test]
        public void CreatePack_WithAParent_ReturnsNull()
        {
            IDeviceNode pack = Make(DeviceType.Pack, null, "1");

            LogAssert.Expect(LogType.Error, new Regex("부모를 가질 수 없습니다"));
            Assert.IsNull(Make(DeviceType.Pack, pack, "2"));
        }
        #endregion

        #region Slots
        [Test]
        public void Create_FillsParentUntilFull_AndStaysInside()
        {
            // 4 x 1 x 4 Pack에 2 x 1 x 2 ModuleAssembly는 4개까지 들어간다.
            IDeviceNode pack = Make(DeviceType.Pack, null, "1");
            Bounds packBounds = BodyBounds(pack);

            for (int i = 0; i < 4; i++)
            {
                IDeviceNode moduleAssembly = Make(DeviceType.ModuleAssembly, pack, (i + 1).ToString());
                Assert.IsNotNull(moduleAssembly, $"{i + 1}번째 ModuleAssembly는 만들어져야 한다.");
                AssertInside(packBounds, RendererBounds(moduleAssembly.Position), $"{i + 1}번째 ModuleAssembly가 Pack 밖에 놓였다.");
            }

            ExpectNoSpaceLogs();
            Assert.IsNull(Make(DeviceType.ModuleAssembly, pack, "5"));
            Assert.IsNotEmpty(factory.LastFullMessage);
            Assert.AreEqual(4, pack.Children.Count);
        }

        [Test]
        public void Create_PaddingReducesCapacity()
        {
            // 여백 2면 칸이 4 x 4 가 되어 Pack 하나에 ModuleAssembly는 1개뿐이다.
            factory.SetPadding(DeviceType.ModuleAssembly, 2.0f);
            IDeviceNode pack = Make(DeviceType.Pack, null, "1");

            Assert.IsNotNull(Make(DeviceType.ModuleAssembly, pack, "1"));

            ExpectNoSpaceLogs();
            Assert.IsNull(Make(DeviceType.ModuleAssembly, pack, "2"));
        }

        [Test]
        public void CreateCell_FillsParallelAssemblyUntilFull()
        {
            IDeviceNode pack = Make(DeviceType.Pack, null, "1");
            IDeviceNode moduleAssembly = Make(DeviceType.ModuleAssembly, pack, "1");
            IDeviceNode module = Make(DeviceType.Module, moduleAssembly, "1");
            IDeviceNode parallelAssembly = Make(DeviceType.ParallelAssembly, module, "1");

            for (int i = 0; i < 4; i++)
            {
                IDeviceNode cell = Make(DeviceType.Cell, parallelAssembly, (i + 1).ToString());
                Assert.IsNotNull(cell, $"{i + 1}번째 Cell은 만들어져야 한다.");
                Assert.IsTrue(PackLayout.IsInsideParent(cell.Position, out Vector3 overflow), $"{i + 1}번째 Cell이 ParallelAssembly 밖으로 {overflow} 벗어났다.");
            }

            ExpectNoSpaceLogs();
            Assert.IsNull(Make(DeviceType.Cell, parallelAssembly, "5"));
            Assert.AreEqual(4, parallelAssembly.Children.Count);
        }
        #endregion

        #region Parent Rules
        [Test]
        public void Create_WithUnregisteredParent_ReturnsNull()
        {
            PackDataModel stranger = new PackDataModel { packName = "x" };

            LogAssert.Expect(LogType.Error, new Regex("생성된 Pack이"));
            Assert.IsNull(Make(DeviceType.ModuleAssembly, stranger, "1"));
        }

        [Test]
        public void Create_WithoutParent_ReturnsNull()
        {
            LogAssert.Expect(LogType.Error, new Regex("생성된 ParallelAssembly이"));
            Assert.IsNull(Make(DeviceType.Cell, null, "1"));
        }

        [Test]
        public void Create_WithWrongParentKind_ReturnsNull()
        {
            IDeviceNode pack = Make(DeviceType.Pack, null, "1");

            // Module의 부모는 ModuleAssembly여야 한다.
            LogAssert.Expect(LogType.Error, new Regex("생성된 ModuleAssembly이"));
            Assert.IsNull(Make(DeviceType.Module, pack, "1"));
        }

        [Test]
        public void Create_LinksParentAndChildren()
        {
            IDeviceNode pack = Make(DeviceType.Pack, null, "1");
            IDeviceNode moduleAssembly = Make(DeviceType.ModuleAssembly, pack, "1");
            IDeviceNode module = Make(DeviceType.Module, moduleAssembly, "1");
            IDeviceNode parallelAssembly = Make(DeviceType.ParallelAssembly, module, "1");
            IDeviceNode cell = Make(DeviceType.Cell, parallelAssembly, "1");

            Assert.IsNull(pack.Parent);
            Assert.AreSame(pack, moduleAssembly.Parent);
            Assert.AreSame(moduleAssembly, module.Parent);
            Assert.AreSame(module, parallelAssembly.Parent);
            Assert.AreSame(parallelAssembly, cell.Parent);
            CollectionAssert.AreEqual(new[] { moduleAssembly }, pack.Children);
            CollectionAssert.AreEqual(new[] { cell }, parallelAssembly.Children);
            Assert.AreEqual(0, cell.Children.Count);

            // 씬 오브젝트도 같은 계층이다.
            Assert.AreSame(moduleAssembly.Position, module.Position.parent);
            Assert.AreSame(parallelAssembly.Position, cell.Position.parent);
        }
        #endregion

        #region Find
        [Test]
        public void Find_ReturnsTheModelOfEachTransform()
        {
            IDeviceNode pack = Make(DeviceType.Pack, null, "1");
            IDeviceNode moduleAssembly = Make(DeviceType.ModuleAssembly, pack, "1");
            IDeviceNode module = Make(DeviceType.Module, moduleAssembly, "1");

            Assert.AreSame(pack, factory.Find(pack.Position));
            Assert.AreSame(moduleAssembly, factory.Find(moduleAssembly.Position));
            Assert.AreSame(module, factory.Find(module.Position));
            Assert.IsNull(factory.Find(null));
            Assert.AreSame(module.Position, factory.GetTransform(module));
            Assert.IsNull(factory.GetTransform(null));
        }

        [Test]
        public void GetLast_ReturnsTheNewestOfEachKind()
        {
            IDeviceNode first = Make(DeviceType.Pack, null, "1");
            IDeviceNode second = Make(DeviceType.Pack, null, "2");

            Assert.AreSame(second, factory.GetLast(DeviceType.Pack));
            Assert.AreNotSame(first, factory.GetLast(DeviceType.Pack));
            Assert.IsNull(factory.GetLast(DeviceType.Cell));
            Assert.AreEqual(2, factory.GetNodes(DeviceType.Pack).Count);
        }
        #endregion
    }
}
