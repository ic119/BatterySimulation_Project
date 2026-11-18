using NUnit.Framework;
using UnityEngine;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Tests
{
    public class PackRegistryTests
    {
        private PackRegistry registry;
        private readonly System.Collections.Generic.List<GameObject> createdList = new System.Collections.Generic.List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            registry = new PackRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < createdList.Count; i++)
            {
                if (createdList[i] != null)
                {
                    Object.DestroyImmediate(createdList[i]);
                }
            }

            createdList.Clear();
        }

        private PackDataModel NewPack(string _name)
        {
            GameObject packObject = new GameObject($"Pack_{_name}");
            createdList.Add(packObject);
            return new PackDataModel { packName = _name, packPosition = packObject.transform };
        }

        private ModuleDataModel NewModule(string _name)
        {
            GameObject moduleObject = new GameObject($"Module_{_name}");
            createdList.Add(moduleObject);
            return new ModuleDataModel { moduleName = _name, modulePosition = moduleObject.transform };
        }

        [Test]
        public void StartsEmpty()
        {
            foreach (DeviceType kind in System.Enum.GetValues(typeof(DeviceType)))
            {
                Assert.AreEqual(0, registry.GetNodes(kind).Count);
                Assert.IsNull(registry.GetLast(kind));
            }
        }

        [Test]
        public void Add_KeepsOrderPerKind_AndLastReturnsTheNewest()
        {
            PackDataModel a = NewPack("1");
            PackDataModel b = NewPack("2");
            ModuleDataModel module = NewModule("1");

            Assert.IsTrue(registry.Add(a));
            Assert.IsTrue(registry.Add(b));
            Assert.IsTrue(registry.Add(module));

            CollectionAssert.AreEqual(new IDeviceNode[] { a, b }, registry.GetNodes(DeviceType.pack));
            Assert.AreSame(b, registry.GetLast(DeviceType.pack));
            Assert.AreSame(module, registry.GetLast(DeviceType.module));
            Assert.IsNull(registry.GetLast(DeviceType.cell));
        }

        [Test]
        public void Add_SameNodeTwice_IsIgnored()
        {
            PackDataModel pack = NewPack("1");

            Assert.IsTrue(registry.Add(pack));
            Assert.IsFalse(registry.Add(pack));
            Assert.IsFalse(registry.Add(null));
            Assert.AreEqual(1, registry.GetNodes(DeviceType.pack).Count);
        }

        [Test]
        public void Find_ByTransform_ReturnsTheRegisteredModel()
        {
            PackDataModel pack = NewPack("1");
            ModuleDataModel module = NewModule("1");
            registry.Add(pack);
            registry.Add(module);

            Assert.AreSame(pack, registry.Find(pack.packPosition));
            Assert.AreSame(module, registry.Find(module.modulePosition));
        }

        [Test]
        public void Find_UnknownOrNull_ReturnsNull()
        {
            PackDataModel registered = NewPack("1");
            PackDataModel stranger = NewPack("2");
            registry.Add(registered);

            Assert.IsNull(registry.Find(stranger.packPosition));
            Assert.IsNull(registry.Find(null));
        }

        [Test]
        public void Remove_OnlyTouchesTheRegistry()
        {
            PackDataModel pack = NewPack("1");
            registry.Add(pack);

            Assert.IsTrue(registry.Remove(pack));

            Assert.IsFalse(registry.Contains(pack));
            Assert.IsNull(registry.Find(pack.packPosition));
            Assert.IsTrue(pack.packPosition != null, "씬 오브젝트는 지우지 않는다.");
        }

        [Test]
        public void Remove_Unregistered_ReturnsFalse()
        {
            Assert.IsFalse(registry.Remove(NewPack("1")));
            Assert.IsFalse(registry.Remove(null));
        }

        [Test]
        public void Clear_EmptiesEverything()
        {
            registry.Add(NewPack("1"));
            registry.Add(NewModule("1"));

            registry.Clear();

            Assert.AreEqual(0, registry.GetNodes(DeviceType.pack).Count);
            Assert.AreEqual(0, registry.GetNodes(DeviceType.module).Count);
        }
    }
}
