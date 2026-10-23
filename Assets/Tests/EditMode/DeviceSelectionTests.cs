using Incheol.Modules;
using NUnit.Framework;
using UnityEngine;
using DeviceType = Incheol.Models.Define.DeviceType;

namespace Incheol.Tests
{
    public class DeviceSelectionTests
    {
        private GameObject target;

        [SetUp]
        public void SetUp()
        {
            target = new GameObject("Target");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(target);
        }

        [Test]
        public void Set_RaisesChangedOnce_WithNewValuesAlreadyApplied()
        {
            DeviceSelection selection = new DeviceSelection();
            PackDataModel pack = new PackDataModel { packName = "1" };
            int count = 0;
            bool hadSelectionInHandler = false;
            selection.Changed += () =>
            {
                count++;
                hadSelectionInHandler = selection.HasSelection && selection.Model == pack && selection.Kind == DeviceType.Pack;
            };

            selection.Set(pack, target.transform);

            Assert.AreEqual(1, count);
            Assert.IsTrue(hadSelectionInHandler, "이벤트를 받을 때는 이미 새 선택이 반영되어 있어야 한다.");
        }

        [Test]
        public void Clear_RaisesChanged_WithNoSelectionInHandler()
        {
            DeviceSelection selection = new DeviceSelection();
            selection.Set(new PackDataModel { packName = "1" }, target.transform);
            int count = 0;
            bool emptyInHandler = false;
            selection.Changed += () =>
            {
                count++;
                emptyInHandler = !selection.HasSelection && selection.Model == null && selection.Target == null;
            };

            selection.Clear();

            Assert.AreEqual(1, count);
            Assert.IsTrue(emptyInHandler);
        }

        [Test]
        public void Clear_WhenAlreadyEmpty_DoesNotRaiseChanged()
        {
            DeviceSelection selection = new DeviceSelection();
            int count = 0;
            selection.Changed += () => count++;

            selection.Clear();

            Assert.AreEqual(0, count, "바뀐 것이 없으면 갱신을 부르지 않는다.");
        }

        [Test]
        public void Set_WithoutSubscribers_DoesNotThrow()
        {
            DeviceSelection selection = new DeviceSelection();

            Assert.DoesNotThrow(() => selection.Set(new PackDataModel { packName = "1" }, target.transform));
        }
    }
}
