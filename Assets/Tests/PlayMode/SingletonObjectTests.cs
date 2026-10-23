using Incheol.Utils;
using NUnit.Framework;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Incheol.Tests
{
    public class SingletonObjectTests
    {
        private class TestSingleton : SingletonObject<TestSingleton>
        {
        }

        private class PersistentTestSingleton : SingletonObject<PersistentTestSingleton>
        {
            protected override bool PersistAcrossScenes => true;
        }

        private static readonly Regex CreatedLog = new Regex("씬에 없어 자동 생성했습니다");
        private static readonly Regex DuplicateLog = new Regex("이미 인스턴스가 있어 중복된");

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 정적 instance는 OnDestroy에서 비워지므로 다음 테스트에 남지 않게 만든 것을 모두 지우고 한 프레임을 보낸다.
            foreach (TestSingleton singleton in Object.FindObjectsByType<TestSingleton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.Destroy(singleton.gameObject);
            }

            foreach (PersistentTestSingleton singleton in Object.FindObjectsByType<PersistentTestSingleton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.Destroy(singleton.gameObject);
            }

            yield return null;
        }

        [Test]
        public void Instance_WhenMissing_CreatesExactlyOneAndLogs()
        {
            LogAssert.Expect(LogType.Log, CreatedLog);

            TestSingleton first = TestSingleton.Instance;
            TestSingleton second = TestSingleton.Instance;

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
            Assert.AreEqual(nameof(TestSingleton), first.name);
            Assert.AreEqual(1, Object.FindObjectsByType<TestSingleton>(FindObjectsSortMode.None).Length);
        }

        [Test]
        public void Instance_WhenPlacedInScene_ReturnsItWithoutCreatingAnother()
        {
            TestSingleton placed = new GameObject("Placed").AddComponent<TestSingleton>();

            TestSingleton instance = TestSingleton.Instance;

            Assert.AreSame(placed, instance);
            Assert.AreEqual(1, Object.FindObjectsByType<TestSingleton>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator Duplicate_IsDestroyedWithWarning_AndOriginalSurvives()
        {
            TestSingleton original = new GameObject("Original").AddComponent<TestSingleton>();

            LogAssert.Expect(LogType.Warning, DuplicateLog);
            GameObject duplicateObject = new GameObject("Duplicate");
            duplicateObject.AddComponent<TestSingleton>();

            yield return null;

            Assert.IsTrue(duplicateObject == null, "중복된 오브젝트는 파괴되어야 한다.");
            Assert.IsTrue(original != null);
            Assert.AreSame(original, TestSingleton.Instance);
        }

        [UnityTest]
        public IEnumerator Instance_AfterDestroy_CreatesANewOne()
        {
            LogAssert.Expect(LogType.Log, CreatedLog);
            TestSingleton old = TestSingleton.Instance;

            Object.Destroy(old.gameObject);
            yield return null;

            LogAssert.Expect(LogType.Log, CreatedLog);
            TestSingleton recreated = TestSingleton.Instance;

            Assert.IsNotNull(recreated);
            Assert.IsTrue(recreated != old);
        }

        [Test]
        public void PersistentSingleton_MovesToDontDestroyOnLoad()
        {
            LogAssert.Expect(LogType.Log, CreatedLog);

            PersistentTestSingleton persistent = PersistentTestSingleton.Instance;

            Assert.AreEqual("DontDestroyOnLoad", persistent.gameObject.scene.name);
            Assert.IsNull(persistent.transform.parent);
        }

        [Test]
        public void NonPersistentSingleton_StaysInTheActiveScene()
        {
            LogAssert.Expect(LogType.Log, CreatedLog);

            TestSingleton singleton = TestSingleton.Instance;

            Assert.AreNotEqual("DontDestroyOnLoad", singleton.gameObject.scene.name);
        }
    }
}
