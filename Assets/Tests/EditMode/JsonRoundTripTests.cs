using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Tests
{
    public class JsonRoundTripTests : FactoryTestBase
    {
        private static readonly Vector3EqualityComparer VectorComparer = new Vector3EqualityComparer(Tolerance);

        [SetUp]
        public void SetUp()
        {
            CreateStandardPrefabs();
        }

        private static PackJsonBuilder NewBuilder(PackFactory _factory)
        {
            return new PackJsonBuilder(_factory, null);
        }

        /// <summary>Pack 2개. 첫 Pack에 ModuleAssembly 2개, 그중 첫 번째에 Module 1개 > ParallelAssembly 1개 > Cell 2개.</summary>
        private void BuildSampleStructure()
        {
            IDeviceNode pack1 = Make(DeviceType.pack, null, "1");
            Make(DeviceType.pack, null, "2");

            IDeviceNode moduleAssembly1 = Make(DeviceType.moduleassembly, pack1, "1");
            Make(DeviceType.moduleassembly, pack1, "2");
            IDeviceNode module = Make(DeviceType.module, moduleAssembly1, "1");
            IDeviceNode parallelAssembly = Make(DeviceType.parallelassembly, module, "1");
            Make(DeviceType.cell, parallelAssembly, "1");
            Make(DeviceType.cell, parallelAssembly, "2");
        }

        private static void AssertSameVector(Vector3 _expected, Vector3 _actual, string _what)
        {
            Assert.That(_actual, Is.EqualTo(_expected).Using(VectorComparer), _what);
        }

        private static void AssertSameNode(JsonDataController.NodeJson _expected, JsonDataController.NodeJson _actual, string _what)
        {
            Assert.AreEqual(_expected.name, _actual.name, $"{_what} name");
            AssertSameVector(_expected.size, _actual.size, $"{_what} size");
            Assert.AreEqual(_expected.hasBodyCenter, _actual.hasBodyCenter, $"{_what} hasBodyCenter");
            AssertSameVector(_expected.bodyCenter, _actual.bodyCenter, $"{_what} bodyCenter");
        }

        private static void AssertSameSave(JsonDataController.SaveJson _expected, JsonDataController.SaveJson _actual)
        {
            Assert.AreEqual(_expected.version, _actual.version, "version");
            Assert.AreEqual(_expected.packs.Count, _actual.packs.Count, "pack count");
            for (int p = 0; p < _expected.packs.Count; p++)
            {
                JsonDataController.PackJson e = _expected.packs[p];
                JsonDataController.PackJson a = _actual.packs[p];
                AssertSameNode(e, a, $"Pack {e.name}");
                Assert.AreEqual(e.moduleAssemblies.Count, a.moduleAssemblies.Count, $"Pack {e.name} moduleAssembly count");

                for (int ma = 0; ma < e.moduleAssemblies.Count; ma++)
                {
                    JsonDataController.ModuleAssemblyJson em = e.moduleAssemblies[ma];
                    JsonDataController.ModuleAssemblyJson am = a.moduleAssemblies[ma];
                    AssertSameNode(em, am, $"ModuleAssembly {em.name}");
                    Assert.AreEqual(em.modules.Count, am.modules.Count, $"ModuleAssembly {em.name} module count");

                    for (int m = 0; m < em.modules.Count; m++)
                    {
                        JsonDataController.ModuleJson emm = em.modules[m];
                        JsonDataController.ModuleJson amm = am.modules[m];
                        AssertSameNode(emm, amm, $"Module {emm.name}");
                        Assert.AreEqual(emm.parallelAssemblies.Count, amm.parallelAssemblies.Count, $"Module {emm.name} parallelAssembly count");

                        for (int pa = 0; pa < emm.parallelAssemblies.Count; pa++)
                        {
                            JsonDataController.ParallelAssemblyJson epa = emm.parallelAssemblies[pa];
                            JsonDataController.ParallelAssemblyJson apa = amm.parallelAssemblies[pa];
                            AssertSameNode(epa, apa, $"ParallelAssembly {epa.name}");
                            Assert.AreEqual(epa.cells.Count, apa.cells.Count, $"ParallelAssembly {epa.name} cell count");

                            for (int c = 0; c < epa.cells.Count; c++)
                            {
                                AssertSameNode(epa.cells[c], apa.cells[c], $"Cell {epa.cells[c].name}");
                            }
                        }
                    }
                }
            }
        }

        private Dictionary<DeviceType, GameObject> Prefabs()
        {
            return new Dictionary<DeviceType, GameObject>(prefabs);
        }

        [Test]
        public void Build_ListsEveryItemOfTheFactory()
        {
            BuildSampleStructure();

            JsonDataController.SaveJson data = JsonDataController.Build(factory);

            Assert.AreEqual(JsonDataController.CurrentVersion, data.version);
            Assert.AreEqual(2, data.packs.Count);
            Assert.AreEqual(2, data.packs[0].moduleAssemblies.Count);
            Assert.AreEqual(1, data.packs[0].moduleAssemblies[0].modules.Count);
            Assert.AreEqual(1, data.packs[0].moduleAssemblies[0].modules[0].parallelAssemblies.Count);
            Assert.AreEqual(2, data.packs[0].moduleAssemblies[0].modules[0].parallelAssemblies[0].cells.Count);
            Assert.IsTrue(data.packs[0].hasBodyCenter);
        }

        [Test]
        public void Build_WithoutFactory_ReturnsEmptyData()
        {
            JsonDataController.SaveJson data = JsonDataController.Build(null);

            Assert.AreEqual(0, data.packs.Count);
        }

        [Test]
        public void SaveThenLoad_RebuildsTheSameStructure()
        {
            BuildSampleStructure();
            JsonDataController.SaveJson saved = JsonDataController.Build(factory);
            JsonDataController.SaveJson loaded = JsonUtility.FromJson<JsonDataController.SaveJson>(JsonUtility.ToJson(saved, true));

            // 저장된 Json을 JsonUtility로 읽은 것만으로도 같은 내용이어야 한다.
            AssertSameSave(saved, loaded);

            // 그 Json으로 새 팩토리에 다시 만든 결과를 다시 저장하면 처음과 같아야 한다.
            PackFactory rebuilt = new PackFactory();
            try
            {
                PackJsonBuilder.Result result = NewBuilder(rebuilt).Build(loaded, Prefabs());

                Assert.AreEqual(0, result.skippedCount);
                Assert.AreEqual(2, result.packCount);
                Assert.AreEqual(2, result.moduleAssemblyCount);
                Assert.AreEqual(1, result.moduleCount);
                Assert.AreEqual(1, result.parallelAssemblyCount);
                Assert.AreEqual(2, result.cellCount);
                AssertSameSave(saved, JsonDataController.Build(rebuilt));
            }
            finally
            {
                Object.DestroyImmediate(rebuilt.Root.gameObject);
            }
        }

        [Test]
        public void Build_FromAnOtherVersion_BuildsNothing()
        {
            JsonDataController.SaveJson data = new JsonDataController.SaveJson { version = 1 };
            data.packs.Add(new JsonDataController.PackJson { name = "1", size = new Vector3(4, 1, 4) });

            PackJsonBuilder.Result result = NewBuilder(factory).Build(data, Prefabs());

            Assert.AreEqual(0, result.packCount);
            Assert.AreEqual(0, factory.GetNodes(DeviceType.pack).Count);
        }

        [Test]
        public void Build_FromJsonThatDoesNotFit_CountsSkippedItems()
        {
            // 1 x 1 x 1 ParallelAssembly 하나에는 0.5 x 1 x 0.5 Cell이 4개까지만 들어간다. 5개를 요청하면 1개가 건너뛰어진다.
            JsonDataController.SaveJson data = new JsonDataController.SaveJson { version = JsonDataController.CurrentVersion };
            JsonDataController.PackJson pack = new JsonDataController.PackJson { name = "1" };
            JsonDataController.ModuleAssemblyJson moduleAssembly = new JsonDataController.ModuleAssemblyJson { name = "1" };
            JsonDataController.ModuleJson module = new JsonDataController.ModuleJson { name = "1" };
            JsonDataController.ParallelAssemblyJson parallelAssembly = new JsonDataController.ParallelAssemblyJson { name = "1" };
            for (int i = 0; i < 5; i++)
            {
                parallelAssembly.cells.Add(new JsonDataController.CellJson { name = (i + 1).ToString() });
            }

            module.parallelAssemblies.Add(parallelAssembly);
            moduleAssembly.modules.Add(module);
            pack.moduleAssemblies.Add(moduleAssembly);
            data.packs.Add(pack);

            ExpectNoSpaceLogs();
            PackJsonBuilder.Result result = NewBuilder(factory).Build(data, Prefabs());

            Assert.AreEqual(1, result.skippedCount);
            Assert.AreEqual(4, result.cellCount);
        }
    }
}
