using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace Incheol.Tests
{
    public class JsonRoundTripTests : FactoryTestBase
    {
        private static readonly Vector3EqualityComparer VectorComparer = new Vector3EqualityComparer(Tolerance);

        private GameObject packPrefab;
        private GameObject modulePrefab;
        private GameObject cellPrefab;

        [SetUp]
        public void SetUp()
        {
            packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(4, 2, 4));
            modulePrefab = CreateBodyPrefab("ModulePrefab", new Vector3(2, 1, 2));
            cellPrefab = CreateCellPrefab("CellPrefab", new Vector3(1, 1, 1));
        }

        private static PackJsonBuilder NewBuilder(PackFactory _factory)
        {
            return new PackJsonBuilder(_factory, null, null, null, null);
        }

        /// <summary>Container 1개 (Pack 격자 2 x 1) 에 Pack 2개, 첫 Pack에 Module 2개(그중 하나에 Cell 2개), 단독 Module 1개를 만든다.</summary>
        private void BuildSampleStructure()
        {
            ContainerDataModel container = factory.CreateContainer("1");
            PackDataModel pack1 = factory.CreatePack("1", packPrefab, default, container, 2, 1);
            factory.CreatePack("2", packPrefab, default, container, 2, 1);

            ModuleDataModel module1 = factory.CreateModule(pack1, "1", modulePrefab);
            factory.CreateModule(pack1, "2", modulePrefab);
            factory.CreateCell(module1, "1", cellPrefab);
            factory.CreateCell(module1, "2", cellPrefab);

            factory.CreateModule(null, "3", modulePrefab, default, container);
        }

        private static void AssertSameVector(Vector3 _expected, Vector3 _actual, string _what)
        {
            Assert.That(_actual, Is.EqualTo(_expected).Using(VectorComparer), _what);
        }

        private static void AssertSameModule(JsonDataController.ModuleJson _expected, JsonDataController.ModuleJson _actual)
        {
            Assert.AreEqual(_expected.name, _actual.name);
            AssertSameVector(_expected.size, _actual.size, $"Module {_expected.name} size");
            Assert.AreEqual(_expected.hasBodyCenter, _actual.hasBodyCenter);
            AssertSameVector(_expected.bodyCenter, _actual.bodyCenter, $"Module {_expected.name} bodyCenter");
            Assert.AreEqual(_expected.cells.Count, _actual.cells.Count, $"Module {_expected.name} cell count");
            for (int i = 0; i < _expected.cells.Count; i++)
            {
                Assert.AreEqual(_expected.cells[i].name, _actual.cells[i].name);
                AssertSameVector(_expected.cells[i].size, _actual.cells[i].size, $"Cell {_expected.cells[i].name} size");
                AssertSameVector(_expected.cells[i].bodyCenter, _actual.cells[i].bodyCenter, $"Cell {_expected.cells[i].name} bodyCenter");
            }
        }

        private static void AssertSameSave(JsonDataController.SaveJson _expected, JsonDataController.SaveJson _actual)
        {
            Assert.AreEqual(_expected.containers.Count, _actual.containers.Count, "container count");
            for (int c = 0; c < _expected.containers.Count; c++)
            {
                JsonDataController.ContainerJson e = _expected.containers[c];
                JsonDataController.ContainerJson a = _actual.containers[c];
                Assert.AreEqual(e.name, a.name);
                Assert.AreEqual((e.packColumns, e.packRows), (a.packColumns, a.packRows), "pack grid");
                Assert.AreEqual(e.packs.Count, a.packs.Count, "pack count");
                Assert.AreEqual(e.standaloneModules.Count, a.standaloneModules.Count, "standalone module count");

                for (int p = 0; p < e.packs.Count; p++)
                {
                    Assert.AreEqual(e.packs[p].name, a.packs[p].name);
                    Assert.AreEqual((e.packs[p].hasSlot, e.packs[p].slotColumn, e.packs[p].slotRow), (a.packs[p].hasSlot, a.packs[p].slotColumn, a.packs[p].slotRow), $"Pack {e.packs[p].name} slot");
                    AssertSameVector(e.packs[p].size, a.packs[p].size, $"Pack {e.packs[p].name} size");
                    AssertSameVector(e.packs[p].bodyCenter, a.packs[p].bodyCenter, $"Pack {e.packs[p].name} bodyCenter");
                    Assert.AreEqual(e.packs[p].modules.Count, a.packs[p].modules.Count, $"Pack {e.packs[p].name} module count");
                    for (int m = 0; m < e.packs[p].modules.Count; m++)
                    {
                        AssertSameModule(e.packs[p].modules[m], a.packs[p].modules[m]);
                    }
                }

                for (int m = 0; m < e.standaloneModules.Count; m++)
                {
                    AssertSameModule(e.standaloneModules[m], a.standaloneModules[m]);
                }
            }
        }

        [Test]
        public void Build_ListsEveryItemOfTheFactory()
        {
            BuildSampleStructure();

            JsonDataController.SaveJson data = JsonDataController.Build(factory, container => new Vector2Int(2, 1));

            Assert.AreEqual(1, data.containers.Count);
            JsonDataController.ContainerJson container = data.containers[0];
            Assert.AreEqual((2, 1), (container.packColumns, container.packRows));
            Assert.AreEqual(2, container.packs.Count);
            Assert.AreEqual(2, container.packs[0].modules.Count);
            Assert.AreEqual(2, container.packs[0].modules[0].cells.Count);
            Assert.AreEqual(1, container.standaloneModules.Count);
            Assert.IsTrue(container.packs[0].hasSlot);
            Assert.IsTrue(container.packs[0].hasBodyCenter);
        }

        [Test]
        public void Build_WithoutFactory_ReturnsEmptyData()
        {
            JsonDataController.SaveJson data = JsonDataController.Build(null);

            Assert.AreEqual(0, data.containers.Count);
        }

        [Test]
        public void SaveThenLoad_RebuildsTheSameStructure()
        {
            BuildSampleStructure();
            JsonDataController.SaveJson saved = JsonDataController.Build(factory, container => new Vector2Int(2, 1));
            JsonDataController.SaveJson loaded = JsonUtility.FromJson<JsonDataController.SaveJson>(JsonUtility.ToJson(saved, true));

            // 저장된 Json을 JsonUtility로 읽은 것만으로도 같은 내용이어야 한다.
            AssertSameSave(saved, loaded);

            // 그 Json으로 새 팩토리에 다시 만든 결과를 다시 저장하면 처음과 같아야 한다.
            PackFactory rebuilt = new PackFactory();
            try
            {
                PackJsonBuilder.Result result = NewBuilder(rebuilt).Build(loaded, packPrefab, modulePrefab, cellPrefab);

                Assert.AreEqual(0, result.skippedCount);
                Assert.AreEqual(2, result.packCount);
                Assert.AreEqual(3, result.moduleCount);
                Assert.AreEqual(2, result.cellCount);
                AssertSameSave(saved, JsonDataController.Build(rebuilt, container => new Vector2Int(2, 1)));
            }
            finally
            {
                Object.DestroyImmediate(rebuilt.Root.gameObject);
            }
        }

        [Test]
        public void Load_LegacyFileWithoutContainers_BuildsOneContainerPerPackAndModule()
        {
            // 새 필드(hasBodyCenter, hasSlot, packColumns ...)와 containers가 없던 예전 형식.
            const string legacyJson = @"{
                ""packs"": [
                    {
                        ""name"": ""7"",
                        ""position"": { ""x"": 1, ""y"": 0, ""z"": 2 },
                        ""size"": { ""x"": 4, ""y"": 2, ""z"": 4 },
                        ""modules"": [
                            {
                                ""name"": ""3"",
                                ""position"": { ""x"": 0, ""y"": 0, ""z"": 0 },
                                ""size"": { ""x"": 2, ""y"": 1, ""z"": 2 },
                                ""cells"": [ { ""name"": ""2"", ""size"": { ""x"": 1, ""y"": 1, ""z"": 1 } } ]
                            }
                        ]
                    }
                ],
                ""standaloneModules"": [
                    { ""name"": ""4"", ""size"": { ""x"": 2, ""y"": 1, ""z"": 2 }, ""cells"": [] }
                ]
            }";

            JsonDataController.SaveJson data = JsonUtility.FromJson<JsonDataController.SaveJson>(legacyJson);
            Assert.AreEqual(0, data.containers.Count);
            Assert.IsFalse(data.packs[0].hasBodyCenter);
            Assert.IsFalse(data.packs[0].hasSlot);

            PackJsonBuilder.Result result = NewBuilder(factory).Build(data, packPrefab, modulePrefab, cellPrefab);

            Assert.AreEqual(0, result.skippedCount);
            Assert.AreEqual(2, factory.Containers.Count, "Pack과 단독 Module이 각각 새 Container에 담겨야 한다.");
            Assert.AreEqual(7, result.packCount);
            Assert.AreEqual(4, result.moduleCount);
            Assert.AreEqual(2, result.cellCount);

            // 몸체 중심이 없던 파일은 저장된 pivot 위치에 Pack을 둔다.
            AssertSameVector(new Vector3(1, 0, 2), factory.GetPackTransform(factory.Packs[0]).position, "legacy pack position");
            Assert.AreEqual(1, factory.Packs[0].modulesList.Count);
            Assert.AreEqual(1, factory.Packs[0].modulesList[0].cellsList.Count);
        }

        [Test]
        public void Build_FromJsonThatDoesNotFit_CountsSkippedItems()
        {
            // 2 x 1 x 2 Module 하나에는 1 x 1 x 1 Cell이 4개까지만 들어간다. 5개를 요청하면 1개가 건너뛰어진다.
            JsonDataController.SaveJson data = new JsonDataController.SaveJson();
            JsonDataController.ModuleJson module = new JsonDataController.ModuleJson { name = "1", size = new Vector3(2, 1, 2) };
            for (int i = 0; i < 5; i++)
            {
                module.cells.Add(new JsonDataController.CellJson { name = (i + 1).ToString(), size = Vector3.one });
            }

            data.standaloneModules.Add(module);

            ExpectNoSpaceLogs();
            PackJsonBuilder.Result result = NewBuilder(factory).Build(data, packPrefab, modulePrefab, cellPrefab);

            Assert.AreEqual(1, result.skippedCount);
            Assert.AreEqual(4, result.cellCount);
        }
    }
}
