using Incheol.Modules;
using NUnit.Framework;
using UnityEngine;

namespace Incheol.Tests
{
    /// <summary>
    /// Pack을 Container의 격자 칸에 놓는 규칙. 한 칸의 크기는 Pack 크기 + 여백이고, 이미 놓인 Pack이 있으면 그 Pack의 면에서 여백만큼 떨어뜨린다.
    /// 아래 값은 Pack 본체 2 x 1 x 2, 여백 1(= 칸 간격 3)을 기준으로 한다. PackFactory의 줄별 동작을 고정하는 특성 테스트다.
    /// </summary>
    public class PackGridPlacementTests : FactoryTestBase
    {
        private GameObject packPrefab;

        [SetUp]
        public void SetUp()
        {
            packPrefab = CreateBodyPrefab("PackPrefab", new Vector3(2, 1, 2));
            factory.PackPadding = 1.0f;
        }

        private PackDataModel CreatePackAt(ContainerDataModel _container, string _name, int _columns, int _rows, int _slotColumn = -1, int _slotRow = -1)
        {
            return factory.CreatePack(_name, packPrefab, default, _container, _columns, _rows, _slotColumn, _slotRow);
        }

        [Test]
        public void RequestedSlotAhead_ThenEarlierSlot_PlacesBeforeIt()
        {
            // 3열 중 2열(맨 뒤)을 먼저 채우면 원점에서 칸 두 개만큼 떨어진 곳에 놓이고, 이어 0열을 요청하면 그 앞쪽 칸에 놓인다.
            ContainerDataModel container = factory.CreateContainer("1");
            PackDataModel last = CreatePackAt(container, "1", 3, 1, 2, 0);
            PackDataModel first = CreatePackAt(container, "2", 3, 1, 0, 0);

            Bounds lastBounds = PackBodyBounds(last);
            Bounds firstBounds = PackBodyBounds(first);
            Assert.AreEqual(5.0f, lastBounds.min.x, Tolerance, "첫 Pack은 원점 기준 칸 크기(3) * 열(2)만큼 떨어져 놓인다.");
            Assert.AreEqual(-1.0f, firstBounds.min.x, Tolerance);
            Assert.AreEqual(lastBounds.min.x, firstBounds.min.x + 2.0f + 1.0f + 3.0f, Tolerance, "0열과 2열 사이에 빈 1열(칸 3)과 여백 1이 있다.");
        }

        [Test]
        public void RequestedRowAhead_ThenEarlierRow_PlacesBeforeIt()
        {
            ContainerDataModel container = factory.CreateContainer("1");
            PackDataModel back = CreatePackAt(container, "1", 1, 3, 0, 2);
            PackDataModel front = CreatePackAt(container, "2", 1, 3, 0, 0);

            Bounds backBounds = PackBodyBounds(back);
            Bounds frontBounds = PackBodyBounds(front);
            Assert.AreEqual(-5.0f, backBounds.max.z, Tolerance, "줄은 -z 방향으로 늘어난다.");
            Assert.AreEqual(1.0f, frontBounds.max.z, Tolerance);
        }

        [Test]
        public void GapBetweenColumns_KeepsTheEmptyColumnWidth()
        {
            // 0열과 2열이 놓인 상태에서 1열을 채우면 둘 사이에 정확히 들어간다.
            ContainerDataModel container = factory.CreateContainer("1");
            PackDataModel a = CreatePackAt(container, "1", 3, 1, 0, 0);
            PackDataModel c = CreatePackAt(container, "3", 3, 1, 2, 0);
            PackDataModel b = CreatePackAt(container, "2", 3, 1, 1, 0);

            Bounds aBounds = PackBodyBounds(a);
            Bounds bBounds = PackBodyBounds(b);
            Bounds cBounds = PackBodyBounds(c);
            Assert.AreEqual(aBounds.max.x + 1.0f, bBounds.min.x, Tolerance);
            Assert.AreEqual(bBounds.max.x + 1.0f, cBounds.min.x, Tolerance);
        }

        [Test]
        public void RequestedSlotAlreadyTaken_FallsBackToTheFirstFreeSlot()
        {
            ContainerDataModel container = factory.CreateContainer("1");
            CreatePackAt(container, "1", 2, 1, 1, 0);

            PackDataModel second = CreatePackAt(container, "2", 2, 1, 1, 0);

            Assert.AreEqual((0, 0), (second.slotColumn, second.slotRow));
        }

        [Test]
        public void RequestedSlotOutsideTheGrid_FallsBackToTheFirstFreeSlot()
        {
            ContainerDataModel container = factory.CreateContainer("1");

            PackDataModel pack = CreatePackAt(container, "1", 2, 1, 5, 5);

            Assert.AreEqual((0, 0), (pack.slotColumn, pack.slotRow));
        }

        [Test]
        public void PackWithoutRecordedSlot_IsPlacedIntoTheSlotMatchingItsPosition()
        {
            // 격자를 정하기 전에 만든 Pack(칸 기록 없음)은 현재 위치를 칸 크기로 나눠 어느 칸인지 구하고, 그 칸은 찬 것으로 본다.
            ContainerDataModel container = factory.CreateContainer("1");
            PackDataModel legacy = CreatePackAt(container, "1", 0, 0);
            Assert.AreEqual(-1, legacy.slotColumn);

            PackDataModel next = CreatePackAt(container, "2", 2, 1);

            Assert.AreEqual((0, 0), (legacy.slotColumn, legacy.slotRow), "칸 기록이 없던 Pack의 칸이 위치로 역산되어 기록된다.");
            Assert.AreEqual((1, 0), (next.slotColumn, next.slotRow));
        }

        [Test]
        public void ContainerSpacing_ReplacesThePaddingOnThatAxis()
        {
            ContainerDataModel container = factory.CreateContainer("1");
            SubDeviceSpacingData spacing = container.containerPosition.gameObject.AddComponent<SubDeviceSpacingData>();
            spacing.Set(0, 0.5f);
            spacing.Set(2, 4.0f);

            PackDataModel a = CreatePackAt(container, "1", 2, 2);
            PackDataModel b = CreatePackAt(container, "2", 2, 2);
            PackDataModel c = CreatePackAt(container, "3", 2, 2);

            Bounds aBounds = PackBodyBounds(a);
            Assert.AreEqual(aBounds.max.x + 0.5f, PackBodyBounds(b).min.x, Tolerance, "x 간격은 Container에 기억된 0.5를 쓴다.");
            Assert.AreEqual(aBounds.min.z - 4.0f, PackBodyBounds(c).max.z, Tolerance, "z 간격은 Container에 기억된 4.0을 쓴다.");
        }

        [Test]
        public void ZeroPadding_PacksTouchEachOther()
        {
            factory.PackPadding = 0.0f;
            ContainerDataModel container = factory.CreateContainer("1");

            PackDataModel a = CreatePackAt(container, "1", 2, 1);
            PackDataModel b = CreatePackAt(container, "2", 2, 1);

            Assert.AreEqual(PackBodyBounds(a).max.x, PackBodyBounds(b).min.x, Tolerance);
        }

        [Test]
        public void PackWithoutRenderer_GetsNoSlot()
        {
            ContainerDataModel container = factory.CreateContainer("1");

            PackDataModel pack = factory.CreatePack("1", null, default, container, 2, 1);

            Assert.AreEqual((-1, -1), (pack.slotColumn, pack.slotRow));
        }

        [Test]
        public void PackWithoutGrid_IsPlacedNextToTheExistingOnes_WithoutOverlap()
        {
            ContainerDataModel container = factory.CreateContainer("1");

            PackDataModel a = CreatePackAt(container, "1", 0, 0);
            PackDataModel b = CreatePackAt(container, "2", 0, 0);

            Assert.IsFalse(PackLayout.Overlaps(PackBodyBounds(a), PackBodyBounds(b)));
            Assert.AreEqual((-1, -1), (b.slotColumn, b.slotRow));
        }

        [Test]
        public void PackPlacement_DoesNotMoveThePacksAlreadyThere()
        {
            ContainerDataModel container = factory.CreateContainer("1");
            PackDataModel a = CreatePackAt(container, "1", 3, 1);
            Bounds before = PackBodyBounds(a);

            CreatePackAt(container, "2", 3, 1);
            CreatePackAt(container, "3", 3, 1);

            Assert.AreEqual(before.center.x, PackBodyBounds(a).center.x, Tolerance);
            Assert.AreEqual(before.center.z, PackBodyBounds(a).center.z, Tolerance);
        }
    }
}
