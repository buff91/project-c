using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class VerticalThrowRulesTests
    {
        private readonly DungeonHeightModel _height = new DungeonHeightModel(4);
        private readonly GridPos _fromUpper = new GridPos(0, 0, 0);
        private readonly GridPos _opening = new GridPos(2, 0, 0);
        private readonly GridPos _landing = new GridPos(2, 0, -4);
        private readonly GridPos _targetLower = new GridPos(4, 0, -4);

        [TestCase(ItemKind.Bomb)]
        [TestCase(ItemKind.FrostBomb)]
        [TestCase(ItemKind.OilFlask)]
        public void CanThrow_AllowsBlastItemsThroughAdjacentOpening(ItemKind kind)
        {
            GridMap map = CreateTwoFloorCorridor();

            Assert.IsTrue(VerticalThrowRules.CanThrow(
                map, _height, _fromUpper, _targetLower, kind, maxRange: 5));
            Assert.IsTrue(VerticalThrowRules.TryResolve(
                map, _height, _fromUpper, _targetLower, kind, maxRange: 5,
                out VerticalThrowPath path));
            Assert.AreEqual(_opening, path.Opening);
            Assert.AreEqual(_landing, path.Landing);
            Assert.AreEqual(5, path.Cost, "2칸 + 개구부 1 + 2칸");
        }

        [Test]
        public void CanThrow_RejectsEveryItemExceptBombFrostAndOil()
        {
            GridMap map = CreateTwoFloorCorridor();
            var supported = new HashSet<ItemKind>
            {
                ItemKind.Bomb,
                ItemKind.FrostBomb,
                ItemKind.OilFlask
            };

            foreach (ItemKind kind in Enum.GetValues(typeof(ItemKind)))
            {
                Assert.AreEqual(supported.Contains(kind), VerticalThrowRules.Supports(kind), kind.ToString());
                Assert.AreEqual(supported.Contains(kind), VerticalThrowRules.CanThrow(
                    map, _height, _fromUpper, _targetLower, kind, maxRange: 5), kind.ToString());
            }
        }

        [Test]
        public void CanThrow_CountsBothPlanarSegmentsAndPortalStepAgainstRange()
        {
            GridMap map = CreateTwoFloorCorridor();

            Assert.IsTrue(VerticalThrowRules.CanThrow(
                map, _height, _fromUpper, _targetLower, ItemKind.Bomb, maxRange: 5));
            Assert.IsFalse(VerticalThrowRules.CanThrow(
                map, _height, _fromUpper, _targetLower, ItemKind.Bomb, maxRange: 4));
        }

        [Test]
        public void CanThrow_IsSymmetricBetweenDownwardAndUpwardRoutes()
        {
            GridMap map = CreateTwoFloorCorridor();

            bool downward = VerticalThrowRules.CanThrow(
                map, _height, _fromUpper, _targetLower, ItemKind.FrostBomb, maxRange: 5);
            bool upward = VerticalThrowRules.CanThrow(
                map, _height, _targetLower, _fromUpper, ItemKind.FrostBomb, maxRange: 5);

            Assert.IsTrue(downward);
            Assert.AreEqual(downward, upward);
        }

        [Test]
        public void CanThrow_RejectsNonAdjacentDungeonFloor()
        {
            GridMap map = CreateTwoFloorCorridor(lowerElevation: -8);
            var target = new GridPos(4, 0, -8);

            Assert.IsFalse(VerticalThrowRules.CanThrow(
                map, _height, _fromUpper, target, ItemKind.Bomb, maxRange: 20));
        }

        [Test]
        public void CanThrow_RequiresRealHoleAndFirstSolidLanding()
        {
            GridMap noHole = CreateTwoFloorCorridor();
            noHole.Set(_opening, TileKind.StairsDown);
            Assert.IsFalse(VerticalThrowRules.CanThrow(
                noHole, _height, _fromUpper, _targetLower, ItemKind.Bomb, maxRange: 5));

            GridMap noLanding = CreateTwoFloorCorridor(includeLanding: false);
            Assert.IsFalse(VerticalThrowRules.CanThrow(
                noLanding, _height, _fromUpper, _targetLower, ItemKind.Bomb, maxRange: 5));

            GridMap blockedShaft = CreateTwoFloorCorridor();
            blockedShaft.Set(new GridPos(2, 0, -2), TileKind.WeakFloor);
            Assert.IsFalse(VerticalThrowRules.CanThrow(
                blockedShaft, _height, _fromUpper, _targetLower, ItemKind.Bomb, maxRange: 5),
                "첫 solid landing 위에 온전한 타일이 끼면 실제 열린 shaft가 아니다");
        }

        [Test]
        public void CanThrow_RequiresSolidTarget()
        {
            GridMap map = CreateTwoFloorCorridor();
            map.Set(_targetLower, TileKind.Hole);

            Assert.IsFalse(VerticalThrowRules.CanThrow(
                map, _height, _fromUpper, _targetLower, ItemKind.OilFlask, maxRange: 5));
        }

        [Test]
        public void CanThrow_UsesSightRulesOnBothPlanarSegments()
        {
            GridMap upperBlocked = CreateTwoFloorCorridor();
            upperBlocked.Set(new GridPos(1, 0, 0), TileKind.Wall);
            Assert.IsFalse(VerticalThrowRules.CanThrow(
                upperBlocked, _height, _fromUpper, _targetLower, ItemKind.Bomb, maxRange: 5),
                "from→opening 시야선");

            GridMap lowerBlocked = CreateTwoFloorCorridor();
            lowerBlocked.Set(new GridPos(3, 0, -4), TileKind.Wall);
            Assert.IsFalse(VerticalThrowRules.CanThrow(
                lowerBlocked, _height, _fromUpper, _targetLower, ItemKind.Bomb, maxRange: 5),
                "landing→target 시야선");
        }

        [Test]
        public void ForEachThrowTarget_EnumeratesExactlyWhatCanThrowConfirms()
        {
            GridMap map = CreateTwoFloorCorridor();
            var targets = new List<GridPos>();

            VerticalThrowRules.ForEachThrowTarget(
                map, _height, _fromUpper, ItemKind.Bomb, maxRange: 4, targets.Add);

            CollectionAssert.AreEqual(
                new[]
                {
                    new GridPos(1, 0, -4),
                    new GridPos(2, 0, -4),
                    new GridPos(3, 0, -4)
                },
                targets);
            Assert.AreEqual(targets.Count, targets.Distinct().Count());
            Assert.IsTrue(targets.All(target => VerticalThrowRules.CanThrow(
                map, _height, _fromUpper, target, ItemKind.Bomb, maxRange: 4)));

            foreach (KeyValuePair<GridPos, TileData> pair in map.All())
            {
                bool confirmed = VerticalThrowRules.CanThrow(
                    map, _height, _fromUpper, pair.Key, ItemKind.Bomb, maxRange: 4);
                Assert.AreEqual(confirmed, targets.Contains(pair.Key), pair.Key.ToString());
            }
        }

        [Test]
        public void NearEndpointPredicate_FiltersTheSameRouteForPreviewAndConfirmation()
        {
            GridMap map = CreateTwoFloorCorridor();
            var seenEndpoints = new List<GridPos>();
            bool AllowOnlyOpening(GridPos endpoint)
            {
                seenEndpoints.Add(endpoint);
                return endpoint == _opening;
            }

            var preview = new List<GridPos>();
            var previewPaths = new List<VerticalThrowPath>();
            VerticalThrowRules.ForEachThrowTarget(
                map, _height, _fromUpper, ItemKind.Bomb, maxRange: 4,
                AllowOnlyOpening,
                (target, path) =>
                {
                    preview.Add(target);
                    previewPaths.Add(path);
                });

            Assert.IsNotEmpty(preview);
            Assert.IsTrue(seenEndpoints.All(endpoint => endpoint == _opening),
                "아래 투척은 현재 층의 Hole만 predicate에 전달한다");
            Assert.IsTrue(previewPaths.All(path => path.Opening == _opening && path.Landing == _landing));
            Assert.IsTrue(preview.All(target => VerticalThrowRules.CanThrow(
                map, _height, _fromUpper, target, ItemKind.Bomb, maxRange: 4,
                endpoint => endpoint == _opening)));
            Assert.IsFalse(VerticalThrowRules.CanThrow(
                map, _height, _fromUpper, _targetLower, ItemKind.Bomb, maxRange: 5,
                _ => false));
        }

        [Test]
        public void NearEndpointPredicate_ReceivesLandingWhenThrowingUpward()
        {
            GridMap map = CreateTwoFloorCorridor();
            GridPos seen = default;

            Assert.IsTrue(VerticalThrowRules.TryResolve(
                map, _height, _targetLower, _fromUpper, ItemKind.OilFlask, maxRange: 5,
                endpoint =>
                {
                    seen = endpoint;
                    return true;
                },
                out VerticalThrowPath path));

            Assert.AreEqual(_landing, seen);
            Assert.AreEqual(_opening, path.Opening);
            Assert.AreEqual(_landing, path.Landing);
        }

        [Test]
        public void BombRules_PreservesSameElevationBehavior()
        {
            GridMap map = CreateTwoFloorCorridor();

            Assert.IsTrue(BombRules.CanThrow(
                map, _fromUpper, new GridPos(1, 0, 0), maxRange: 1));
            Assert.IsFalse(BombRules.CanThrow(
                map, _fromUpper, _targetLower, maxRange: 20),
                "기존 BombRules는 다른 elevation을 계속 거부한다");
        }

        private GridMap CreateTwoFloorCorridor(
            int lowerElevation = -4,
            bool includeLanding = true)
        {
            var map = new GridMap();
            for (int x = 0; x <= 4; x++)
            {
                map.Set(new GridPos(x, 0, 0), TileKind.Floor);
                if (includeLanding || x != _landing.x)
                    map.Set(new GridPos(x, 0, lowerElevation), TileKind.Floor);
            }
            map.Set(_opening, TileKind.Hole);
            return map;
        }
    }
}
