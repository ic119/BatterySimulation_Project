using Incheol.Modules;
using NUnit.Framework;
using UnityEngine;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Tests
{
    public class DeviceModelTests
    {
        private readonly System.Collections.Generic.List<GameObject> createdList = new System.Collections.Generic.List<GameObject>();

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

        private GameObject NewBox(string _name, Transform _parent = null)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = _name;
            box.transform.SetParent(_parent, false);
            createdList.Add(box);
            return box;
        }

        #region IDeviceNode
        [Test]
        public void Models_ExposeKindAndName()
        {
            IDeviceNode[] nodeArray =
            {
                new ContainerDataModel { containerName = "c" },
                new PackDataModel { packName = "p" },
                new ModuleDataModel { moduleName = "m" },
                new CellDataModel { cellName = "e" },
            };
            DeviceType[] kindArray = { DeviceType.Container, DeviceType.Pack, DeviceType.Module, DeviceType.Cell };
            string[] nameArray = { "c", "p", "m", "e" };

            for (int i = 0; i < nodeArray.Length; i++)
            {
                Assert.AreEqual(kindArray[i], nodeArray[i].Kind);
                Assert.AreEqual(nameArray[i], nodeArray[i].Name);
            }
        }

        [Test]
        public void Name_Setter_WritesTheUnderlyingField()
        {
            ContainerDataModel container = new ContainerDataModel();
            PackDataModel pack = new PackDataModel();
            ModuleDataModel module = new ModuleDataModel();
            CellDataModel cell = new CellDataModel();

            ((IDeviceNode)container).Name = "c";
            ((IDeviceNode)pack).Name = "p";
            ((IDeviceNode)module).Name = "m";
            ((IDeviceNode)cell).Name = "e";

            Assert.AreEqual("c", container.containerName);
            Assert.AreEqual("p", pack.packName);
            Assert.AreEqual("m", module.moduleName);
            Assert.AreEqual("e", cell.cellName);
        }
        #endregion

        #region DeviceSelection
        [Test]
        public void DeviceSelection_StartsEmpty()
        {
            DeviceSelection selection = new DeviceSelection();

            Assert.IsNull(selection.Model);
            Assert.IsNull(selection.Target);
            Assert.IsFalse(selection.HasSelection);
        }

        [Test]
        public void DeviceSelection_SetAndClear()
        {
            DeviceSelection selection = new DeviceSelection();
            ModuleDataModel module = new ModuleDataModel { moduleName = "1" };
            Transform target = NewBox("Module_1").transform;

            selection.Set(module, target);

            Assert.IsTrue(selection.HasSelection);
            Assert.AreSame(module, selection.Model);
            Assert.AreSame(target, selection.Target);
            Assert.AreEqual(DeviceType.Module, selection.Kind);

            selection.Clear();

            Assert.IsFalse(selection.HasSelection);
            Assert.IsNull(selection.Model);
            Assert.IsNull(selection.Target);
        }

        [Test]
        public void DeviceSelection_ModelWithoutTarget_IsNotASelection()
        {
            DeviceSelection selection = new DeviceSelection();

            selection.Set(new CellDataModel { cellName = "1" }, null);

            Assert.IsFalse(selection.HasSelection);
            Assert.AreEqual(DeviceType.Cell, selection.Kind);
        }
        #endregion

        #region DeviceVisibility
        [Test]
        public void SetVisible_False_HidesTargetAndChildrenOnly()
        {
            GameObject root = new GameObject("Root");
            createdList.Add(root);
            GameObject a = NewBox("A", root.transform);
            GameObject aChild = NewBox("AChild", a.transform);
            GameObject b = NewBox("B", root.transform);

            DeviceVisibility.SetVisible(a.transform, false);

            Assert.IsFalse(a.GetComponent<Renderer>().enabled);
            Assert.IsFalse(aChild.GetComponent<Renderer>().enabled);
            Assert.IsTrue(b.GetComponent<Renderer>().enabled);
        }

        [Test]
        public void SetVisible_True_ForPack_ShowsChildren()
        {
            GameObject pack = NewBox("Pack");
            GameObject module = NewBox("Module", pack.transform);
            pack.GetComponent<Renderer>().enabled = false;
            module.GetComponent<Renderer>().enabled = false;

            DeviceVisibility.SetVisible(pack.transform, true);

            Assert.IsTrue(pack.GetComponent<Renderer>().enabled);
            Assert.IsTrue(module.GetComponent<Renderer>().enabled);
        }

        [Test]
        public void SetVisible_True_ForCell_ShowsOnlyThatCellUnderTheRoot()
        {
            GameObject root = new GameObject("Root");
            createdList.Add(root);
            GameObject body = NewBox("Body", root.transform);
            GameObject cell1 = NewBox("Cell1", root.transform);
            GameObject cell2 = NewBox("Cell2", root.transform);
            body.GetComponent<Renderer>().enabled = true;
            cell1.GetComponent<Renderer>().enabled = true;
            cell2.GetComponent<Renderer>().enabled = true;

            DeviceVisibility.SetVisible(cell1.transform, true, DeviceType.Cell);

            Assert.IsFalse(body.GetComponent<Renderer>().enabled);
            Assert.IsTrue(cell1.GetComponent<Renderer>().enabled);
            Assert.IsFalse(cell2.GetComponent<Renderer>().enabled);
        }

        [Test]
        public void SetVisible_NullTarget_DoesNothing()
        {
            Assert.DoesNotThrow(() => DeviceVisibility.SetVisible(null, true));
            Assert.DoesNotThrow(() => DeviceVisibility.SetVisible(null, false, DeviceType.Cell));
        }
        #endregion
    }
}
