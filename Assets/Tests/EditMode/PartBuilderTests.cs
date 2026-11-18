using Incheol.Models;
using Incheol.Modules;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Tests
{
    /// <summary>React 저장 모델(JSON)에서 부품의 크기·자리를 계산하고(DeviceLayout) 씬에 만드는(PartBuilder) 규칙.</summary>
    public class PartBuilderTests : FactoryTestBase
    {
        private const string CellJson = "{\"kind\":\"cell\",\"id\":\"CELLID\",\"name\":\"C\",\"stackingAxis\":\"Y\",\"geometry\":{\"shape\":\"Prismatic\",\"length\":173,\"thickness\":45,\"height\":125}}";

        private static string PaJson(string _id, int _cells, int _gap)
        {
            return "{\"kind\":\"parallelAssembly\",\"id\":\"pa-" + _id + "\",\"name\":\"PA\",\"stackingAxis\":\"Y\",\"topology\":\"SingleStack\",\"numParallelCells\":" + _cells + ",\"rows\":1,\"interCellGap\":" + _gap + ",\"cell\":" + CellJson.Replace("CELLID", "cell-" + _id) + "}";
        }

        private static string ModuleJson(string _id, int _series, int _gap, int _cells = 3)
        {
            return "{\"kind\":\"module\",\"id\":\"" + _id + "\",\"name\":\"M\",\"stackingAxis\":\"X\",\"topology\":\"SingleStack\",\"numSeriesAssemblies\":" + _series + ",\"interParallelAssemblyGap\":" + _gap + ",\"parallelAssembly\":" + PaJson(_id, _cells, 1) + "}";
        }

        private static string MaJson(int _levels, params string[] _modules)
        {
            return "{\"kind\":\"moduleAssembly\",\"id\":\"ma-01\",\"name\":\"MA\",\"stackingAxis\":\"Y\",\"interModuleGap\":5,\"numLevels\":" + _levels + ",\"modules\":[" + string.Join(",", _modules) + "]}";
        }

        private static string PackJson(params string[] _mas)
        {
            return "{\"kind\":\"pack\",\"id\":\"pack-01\",\"name\":\"P\",\"stackingAxis\":\"Y\",\"interModuleAssemblyGap\":5,\"moduleAssemblies\":[" + string.Join(",", _mas) + "]}";
        }

        private static PackDataModel Pack(string _json)
        {
            PackDataModel pack = new PackDataModel();
            Assert.IsTrue(pack.ApplyFrom(JsonUtility.FromJson<PackPartJson>(_json)));
            return pack;
        }

        private Dictionary<DeviceType, GameObject> Prefabs()
        {
            // 컨테이너 프리팹은 실제처럼 몸체 없는 빈 루트(프리팹이 없으면 빈 GameObject), Cell만 상자.
            return new Dictionary<DeviceType, GameObject> { { DeviceType.cell, CreateCellPrefab("CellPrefab", new Vector3(0.01f, 0.07f, 0.30f)) } };
        }

        [Test]
        public void Layout_ParallelAssembly_StacksCellsAlongXWithGap()
        {
            ParallelAssemblyDataModel pa = new ParallelAssemblyDataModel();
            Assert.IsTrue(pa.ApplyFrom(JsonUtility.FromJson<ParallelAssemblyPartJson>(PaJson("1", 3, 1))));

            LayoutNode layout = DeviceLayout.Compute(pa);

            Assert.AreEqual(3, layout.Children.Count);
            Assert.AreEqual(3 * 0.045f + 2 * 0.001f, layout.Size.x, Tolerance);
            Assert.AreEqual(0.125f, layout.Size.y, Tolerance);
            Assert.AreEqual(0.173f, layout.Size.z, Tolerance);
            Assert.AreEqual(0.046f, layout.Children[1].LocalMin.x, Tolerance);
            Assert.AreEqual(0.092f, layout.Children[2].LocalMin.x, Tolerance);
        }

        [Test]
        public void Layout_Module_StacksParallelAssembliesAlongZ()
        {
            PackDataModel pack = Pack(PackJson(MaJson(1, ModuleJson("m1", 4, 1))));

            LayoutNode module = DeviceLayout.Compute(pack.moduleAssembliesList[0].modulesList[0]);

            Assert.AreEqual(4, module.Children.Count);
            Assert.AreEqual(4 * 0.173f + 3 * 0.001f, module.Size.z, Tolerance);
            Assert.AreEqual(0.137f, module.Size.x, Tolerance);
            Assert.AreEqual(0.174f, module.Children[1].LocalMin.z, Tolerance);
        }

        [Test]
        public void Layout_ModuleAssembly_SplitsModulesEvenlyIntoLevelsAlongY()
        {
            PackDataModel pack = Pack(PackJson(MaJson(2, ModuleJson("m1", 1, 0), ModuleJson("m2", 1, 0), ModuleJson("m3", 1, 0), ModuleJson("m4", 1, 0))));

            LayoutNode ma = DeviceLayout.Compute(pack.moduleAssembliesList[0]);

            Assert.AreEqual(4, ma.Children.Count);
            Assert.AreEqual(0.0f, ma.Children[0].LocalMin.y, Tolerance);
            Assert.AreEqual(0.0f, ma.Children[1].LocalMin.y, Tolerance);
            Assert.AreEqual(0.125f + 0.005f, ma.Children[2].LocalMin.y, Tolerance);
            Assert.AreEqual(0.125f + 0.005f, ma.Children[3].LocalMin.y, Tolerance);
            // 한 층의 Module 둘은 x(MA 축 "Y")로 나란히, 간격 5mm.
            Assert.AreEqual(0.137f + 0.005f, ma.Children[1].LocalMin.x, Tolerance);
            Assert.AreEqual(2 * 0.125f + 0.005f, ma.Size.y, Tolerance);
        }

        [Test]
        public void Layout_CellWithoutGeometry_ReturnsNull()
        {
            CellDataModel cell = new CellDataModel();
            LogAssert.Expect(LogType.Error, new Regex("Cell 크기"));
            Assert.IsNull(DeviceLayout.Compute(cell));
        }

        [Test]
        public void Build_Pack_CreatesCopiesWithUniqueIds()
        {
            PackDataModel spec = Pack(PackJson(MaJson(1, ModuleJson("m1", 4, 1))));
            PartBuilder builder = new PartBuilder(factory, null);

            IDeviceNode root = builder.Build(spec, Prefabs());

            Assert.IsNotNull(root);
            Assert.AreEqual(1, factory.GetNodes(DeviceType.pack).Count);
            Assert.AreEqual(1, factory.GetNodes(DeviceType.moduleassembly).Count);
            Assert.AreEqual(1, factory.GetNodes(DeviceType.module).Count);
            Assert.AreEqual(4, factory.GetNodes(DeviceType.parallelassembly).Count);
            Assert.AreEqual(12, factory.GetNodes(DeviceType.cell).Count);
            Assert.IsNotNull(factory.FindByDeviceId("pa-m1"));
            Assert.IsNotNull(factory.FindByDeviceId("pa-m1#4"));
            Assert.IsNotNull(factory.FindByDeviceId("cell-m1#12"));
            Assert.AreNotSame(spec.moduleAssembliesList[0], factory.GetNodes(DeviceType.moduleassembly)[0], "받은 모델이 아니라 새로 만든 모델이 씬에 연결돼야 한다.");
        }

        [Test]
        public void Build_Pack_PlacesCellsWithMillimetersConverted()
        {
            PackDataModel spec = Pack(PackJson(MaJson(1, ModuleJson("m1", 2, 1))));
            new PartBuilder(factory, null).Build(spec, Prefabs());

            Bounds a = factory.FindByDeviceId("cell-m1").Position.GetComponent<Renderer>().bounds;
            Bounds b = factory.FindByDeviceId("cell-m1#2").Position.GetComponent<Renderer>().bounds;

            Assert.AreEqual(0.045f, a.size.x, Tolerance);
            Assert.AreEqual(0.173f, a.size.z, Tolerance);
            Assert.AreEqual(a.max.x + 0.001f, b.min.x, Tolerance, "interCellGap 1mm");
        }

        [Test]
        public void Build_NonPackSpec_CreatesStandaloneDownToCell()
        {
            PackDataModel spec = Pack(PackJson(MaJson(1, ModuleJson("m1", 2, 1))));
            ModuleDataModel module = spec.moduleAssembliesList[0].modulesList[0];

            IDeviceNode root = new PartBuilder(factory, null).Build(module, Prefabs());

            Assert.IsNotNull(root);
            Assert.AreEqual(DeviceType.module, root.Kind);
            Assert.IsNull(root.Parent);
            Assert.AreEqual(0, factory.GetNodes(DeviceType.pack).Count);
            Assert.AreEqual(2, factory.GetNodes(DeviceType.parallelassembly).Count);
            Assert.AreEqual(6, factory.GetNodes(DeviceType.cell).Count);
        }
    }
}
