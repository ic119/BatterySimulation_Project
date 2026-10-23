using NUnit.Framework;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Incheol.Tests
{
    public class JsonMigrationTests : FactoryTestBase
    {
        private static readonly Regex NewerVersionLog = new Regex("새로운 형식");
        private static readonly Regex UnreadableVersionLog = new Regex("읽을 수 없는 버전");

        // 버전 필드와 containers가 없던 가장 오래된 형식: 최상위에 Pack 하나와 단독 Module 하나.
        private const string LegacyJson = @"{
            ""packs"": [
                {
                    ""name"": ""7"",
                    ""position"": { ""x"": 1, ""y"": 0, ""z"": 2 },
                    ""size"": { ""x"": 4, ""y"": 2, ""z"": 4 },
                    ""modules"": []
                }
            ],
            ""standaloneModules"": [
                { ""name"": ""4"", ""size"": { ""x"": 2, ""y"": 1, ""z"": 2 }, ""cells"": [] }
            ]
        }";

        private static JsonDataController.SaveJson Parse(string _json)
        {
            return JsonUtility.FromJson<JsonDataController.SaveJson>(_json);
        }

        private static string WriteTempJson(string _json)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, _json);
            return path;
        }

        [Test]
        public void FromJson_WithoutVersionField_ReadsAsVersionZero()
        {
            Assert.AreEqual(0, Parse(LegacyJson).version);
        }

        [Test]
        public void Build_StampsTheCurrentVersion_AndItSurvivesJson()
        {
            factory.CreatePack("1", CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2)));

            JsonDataController.SaveJson data = JsonDataController.Build(factory);
            JsonDataController.SaveJson reloaded = Parse(JsonUtility.ToJson(data, true));

            Assert.AreEqual(JsonDataController.CurrentVersion, data.version);
            Assert.AreEqual(JsonDataController.CurrentVersion, reloaded.version);
        }

        [Test]
        public void TryMigrate_Legacy_MovesEachItemIntoItsOwnNumberedContainer()
        {
            JsonDataController.SaveJson data = Parse(LegacyJson);

            Assert.IsTrue(JsonDataMigration.TryMigrate(data));

            Assert.AreEqual(JsonDataController.CurrentVersion, data.version);
            Assert.AreEqual(0, data.packs.Count);
            Assert.AreEqual(0, data.standaloneModules.Count);
            Assert.AreEqual(2, data.containers.Count);
            Assert.AreEqual("1", data.containers[0].name);
            Assert.AreEqual("7", data.containers[0].packs[0].name);
            Assert.AreEqual("2", data.containers[1].name);
            Assert.AreEqual("4", data.containers[1].standaloneModules[0].name);
        }

        [Test]
        public void TryMigrate_ContinuesNumberingAfterExistingContainers()
        {
            // 중간 형식: 이미 containers가 있고 최상위에도 예전 Pack이 남아 있다.
            JsonDataController.SaveJson data = Parse(@"{
                ""containers"": [ { ""name"": ""5"" } ],
                ""packs"": [ { ""name"": ""1"", ""modules"": [] } ]
            }");

            Assert.IsTrue(JsonDataMigration.TryMigrate(data));

            Assert.AreEqual(2, data.containers.Count);
            Assert.AreEqual("5", data.containers[0].name);
            Assert.AreEqual("6", data.containers[1].name);
        }

        [Test]
        public void TryMigrate_IsIdempotent()
        {
            JsonDataController.SaveJson data = Parse(LegacyJson);
            JsonDataMigration.TryMigrate(data);
            string afterFirst = JsonUtility.ToJson(data);

            Assert.IsTrue(JsonDataMigration.TryMigrate(data));

            Assert.AreEqual(afterFirst, JsonUtility.ToJson(data));
        }

        [Test]
        public void TryMigrate_CurrentVersion_LeavesDataUntouched()
        {
            factory.CreatePack("1", CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2)));
            JsonDataController.SaveJson data = JsonDataController.Build(factory);
            string before = JsonUtility.ToJson(data);

            Assert.IsTrue(JsonDataMigration.TryMigrate(data));

            Assert.AreEqual(before, JsonUtility.ToJson(data));
        }

        [Test]
        public void TryMigrate_NewerVersion_IsRejected()
        {
            JsonDataController.SaveJson data = Parse(LegacyJson);
            data.version = JsonDataController.CurrentVersion + 1;
            string before = JsonUtility.ToJson(data);

            LogAssert.Expect(LogType.Error, NewerVersionLog);
            Assert.IsFalse(JsonDataMigration.TryMigrate(data));

            Assert.AreEqual(before, JsonUtility.ToJson(data), "읽을 수 없는 파일은 건드리지 않는다.");
        }

        [Test]
        public void TryMigrate_Null_IsRejected()
        {
            Assert.IsFalse(JsonDataMigration.TryMigrate(null));
        }

        [Test]
        public void Builder_WithNewerVersion_CreatesNothing()
        {
            JsonDataController.SaveJson data = Parse(LegacyJson);
            data.version = JsonDataController.CurrentVersion + 1;
            PackJsonBuilder builder = new PackJsonBuilder(factory, null, null, null, null);

            LogAssert.Expect(LogType.Error, NewerVersionLog);
            PackJsonBuilder.Result result = builder.Build(data, CreateBodyPrefab("PackPrefab", new Vector3(4, 2, 4)), CreateBodyPrefab("ModulePrefab", new Vector3(2, 1, 2)), CreateCellPrefab("CellPrefab", Vector3.one));

            Assert.AreEqual(0, factory.Containers.Count);
            Assert.AreEqual(0, result.packCount + result.moduleCount + result.cellCount + result.containerCount);
        }

        [Test]
        public void Load_LegacyFile_ReturnsMigratedData()
        {
            string path = WriteTempJson(LegacyJson);
            try
            {
                JsonDataController.SaveJson data = JsonDataController.Load(path);

                Assert.IsNotNull(data);
                Assert.AreEqual(JsonDataController.CurrentVersion, data.version);
                Assert.AreEqual(2, data.containers.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Load_NewerVersionFile_ReturnsNull()
        {
            string path = WriteTempJson($"{{ \"version\": {JsonDataController.CurrentVersion + 1}, \"containers\": [] }}");
            try
            {
                LogAssert.Expect(LogType.Error, NewerVersionLog);
                LogAssert.Expect(LogType.Error, UnreadableVersionLog);

                Assert.IsNull(JsonDataController.Load(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void Load_MissingFile_ReturnsNull()
        {
            LogAssert.Expect(LogType.Error, new Regex("Json 읽기 실패"));

            Assert.IsNull(JsonDataController.Load(Path.Combine(Path.GetTempPath(), "does_not_exist_" + System.Guid.NewGuid().ToString("N") + ".json")));
        }
    }
}
