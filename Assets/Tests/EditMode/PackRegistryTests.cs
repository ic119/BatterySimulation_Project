using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Incheol.Tests
{
    public class PackRegistryTests
    {
        private PackRegistry registry;
        private readonly List<GameObject> createdList = new List<GameObject>();

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

        private Transform NewTransform(string _name)
        {
            GameObject gameObject = new GameObject(_name);
            createdList.Add(gameObject);
            return gameObject.transform;
        }

        private ContainerDataModel NewContainer(string _name)
        {
            return new ContainerDataModel
            {
                containerName = _name,
                packsList = new List<PackDataModel>(),
                modulesList = new List<ModuleDataModel>(),
                containerPosition = NewTransform("Container_" + _name),
            };
        }

        private static PackDataModel NewPack(string _name)
        {
            return new PackDataModel { packName = _name, modulesList = new List<ModuleDataModel>() };
        }

        private ModuleDataModel NewModule(string _name)
        {
            return new ModuleDataModel { moduleName = _name, cellsList = new List<CellDataModel>(), modulePosition = NewTransform("Module_" + _name) };
        }

        [Test]
        public void StartsEmpty()
        {
            Assert.AreEqual(0, registry.Containers.Count);
            Assert.AreEqual(0, registry.Packs.Count);
            Assert.AreEqual(0, registry.Modules.Count);
            Assert.IsNull(registry.LastPack);
            Assert.IsNull(registry.LastModule);
        }

        [Test]
        public void Add_KeepsOrder_AndLastReturnsTheNewest()
        {
            PackDataModel packA = NewPack("A");
            PackDataModel packB = NewPack("B");
            ModuleDataModel moduleA = NewModule("A");
            ModuleDataModel moduleB = NewModule("B");

            registry.AddPack(packA, NewTransform("PackA"));
            registry.AddPack(packB, NewTransform("PackB"));
            registry.AddModule(moduleA);
            registry.AddModule(moduleB);

            Assert.AreEqual(2, registry.Packs.Count);
            Assert.AreSame(packA, registry.Packs[0]);
            Assert.AreSame(packB, registry.LastPack);
            Assert.AreSame(moduleB, registry.LastModule);
            Assert.IsTrue(registry.ContainsModule(moduleA));
        }

        [Test]
        public void Find_ByTransform_ReturnsTheRegisteredModel()
        {
            ContainerDataModel container = NewContainer("1");
            PackDataModel pack = NewPack("1");
            Transform packTransform = NewTransform("Pack_1");
            ModuleDataModel module = NewModule("1");
            registry.AddContainer(container);
            registry.AddPack(pack, packTransform);
            registry.AddModule(module);

            Assert.AreSame(container, registry.FindContainer(container.containerPosition));
            Assert.AreSame(pack, registry.FindPack(packTransform));
            Assert.AreSame(module, registry.FindModule(module.modulePosition));
            Assert.AreSame(packTransform, registry.GetPackTransform(pack));
        }

        [Test]
        public void Find_UnknownOrNull_ReturnsNull()
        {
            Transform stranger = NewTransform("Stranger");
            registry.AddPack(NewPack("1"), NewTransform("Pack_1"));

            Assert.IsNull(registry.FindContainer(stranger));
            Assert.IsNull(registry.FindPack(stranger));
            Assert.IsNull(registry.FindModule(stranger));
            Assert.IsNull(registry.FindContainer(null));
            Assert.IsNull(registry.FindPack(null));
            Assert.IsNull(registry.FindModule(null));
            Assert.IsNull(registry.GetPackTransform(null));
            Assert.IsNull(registry.GetPackTransform(NewPack("unregistered")));
        }

        [Test]
        public void Remove_OnlyTouchesTheRegistry()
        {
            ContainerDataModel container = NewContainer("1");
            PackDataModel pack = NewPack("1");
            ModuleDataModel module = NewModule("1");
            container.packsList.Add(pack);
            container.modulesList.Add(module);
            registry.AddContainer(container);
            registry.AddPack(pack, NewTransform("Pack_1"));
            registry.AddModule(module);

            Assert.IsTrue(registry.RemovePack(pack));
            Assert.IsTrue(registry.RemoveModule(module));
            Assert.IsTrue(registry.RemoveContainer(container));

            Assert.AreEqual(0, registry.Packs.Count);
            Assert.AreEqual(0, registry.Modules.Count);
            Assert.AreEqual(0, registry.Containers.Count);
            Assert.AreEqual(1, container.packsList.Count, "부모의 목록은 PackRemover가 정리하므로 등록부는 건드리지 않는다.");
            Assert.AreEqual(1, container.modulesList.Count);
            Assert.IsTrue(container.containerPosition != null, "씬 오브젝트도 건드리지 않는다.");
        }

        [Test]
        public void Remove_Unregistered_ReturnsFalse()
        {
            Assert.IsFalse(registry.RemovePack(NewPack("x")));
            Assert.IsFalse(registry.RemoveModule(NewModule("x")));
            Assert.IsFalse(registry.RemoveContainer(NewContainer("x")));
            Assert.IsFalse(registry.ContainsContainer(NewContainer("y")));
        }

        [Test]
        public void TryUnregisterPackTransform_ReturnsItOnce()
        {
            PackDataModel pack = NewPack("1");
            Transform packTransform = NewTransform("Pack_1");
            registry.AddPack(pack, packTransform);

            Assert.IsTrue(registry.TryUnregisterPackTransform(pack, out Transform first));
            Assert.AreSame(packTransform, first);
            Assert.IsFalse(registry.TryUnregisterPackTransform(pack, out Transform second));
            Assert.IsNull(second);
            Assert.IsFalse(registry.TryUnregisterPackTransform(null, out _));
            Assert.IsNull(registry.GetPackTransform(pack));
        }

        [Test]
        public void Clear_EmptiesEverything()
        {
            PackDataModel pack = NewPack("1");
            registry.AddContainer(NewContainer("1"));
            registry.AddPack(pack, NewTransform("Pack_1"));
            registry.AddModule(NewModule("1"));

            registry.Clear();

            Assert.AreEqual(0, registry.Containers.Count);
            Assert.AreEqual(0, registry.Packs.Count);
            Assert.AreEqual(0, registry.Modules.Count);
            Assert.IsNull(registry.GetPackTransform(pack));
        }
    }
}
