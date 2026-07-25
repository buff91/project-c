using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 진행 방향은 던전별 데이터다 — 하강 던전과 상승 던전이 함께 존재한다.
    /// 이 테스트가 생성기 방향 지원의 계약이다(생성기가 아직 이 규칙을 읽지 않아도
    /// 규칙 자체는 고정된다).
    /// </summary>
    public class DungeonDirectionRulesTests
    {
        [Test]
        public void FloorIndex_SignFollowsDirection_AndFirstFloorIsAlwaysZero()
        {
            Assert.AreEqual(0, DungeonDirectionRules.FloorIndexFor(DungeonProgressDirection.Descend, 0));
            Assert.AreEqual(0, DungeonDirectionRules.FloorIndexFor(DungeonProgressDirection.Ascend, 0));

            Assert.AreEqual(-3, DungeonDirectionRules.FloorIndexFor(DungeonProgressDirection.Descend, 3));
            Assert.AreEqual(3, DungeonDirectionRules.FloorIndexFor(DungeonProgressDirection.Ascend, 3));
        }

        /// <summary>
        /// 공간(위/아래)과 진행(진출/귀환)의 분리. StairsUp/Down 은 공간 이름이라 고정이고,
        /// "다음 층으로 가는 계단"이 무엇인지만 방향을 탄다.
        /// </summary>
        [Test]
        public void OnwardAndBackStairs_SwapWithDirection()
        {
            Assert.AreEqual(
                TileKind.StairsDown,
                DungeonDirectionRules.OnwardStair(DungeonProgressDirection.Descend));
            Assert.AreEqual(
                TileKind.StairsUp,
                DungeonDirectionRules.BackStair(DungeonProgressDirection.Descend));

            Assert.AreEqual(
                TileKind.StairsUp,
                DungeonDirectionRules.OnwardStair(DungeonProgressDirection.Ascend));
            Assert.AreEqual(
                TileKind.StairsDown,
                DungeonDirectionRules.BackStair(DungeonProgressDirection.Ascend));
        }

        /// <summary>
        /// 고도가 축이 아닌 던전(<c>Inward</c>). 층은 여전히 쌓이지만 그건 렌더·컬링을 위한
        /// 엔진 사정이고, 플레이어에게 진행은 "얼마나 깊이 들어왔는가"다.
        /// </summary>
        [Test]
        public void Inward_DoesNotUseVerticalProgress_ButStillStacksFloors()
        {
            Assert.IsFalse(DungeonDirectionRules.UsesVerticalProgress(DungeonProgressDirection.Inward));
            Assert.IsTrue(DungeonDirectionRules.UsesVerticalProgress(DungeonProgressDirection.Descend));
            Assert.IsTrue(DungeonDirectionRules.UsesVerticalProgress(DungeonProgressDirection.Ascend));

            // 층 인덱스는 여전히 서로 달라야 한다 — 같으면 구역들이 공간에서 겹친다.
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int progress = 0; progress < 10; progress++)
            {
                int floorIndex =
                    DungeonDirectionRules.FloorIndexFor(DungeonProgressDirection.Inward, progress);
                Assert.IsTrue(seen.Add(floorIndex),
                    $"진행 {progress}의 층 인덱스가 중복됐다 — 구역이 공간에서 겹친다");
            }
        }

        /// <summary>진입 깊이 던전은 층 라벨을 쓰지 않는다 — B1/1F 는 화면에서 거짓이 된다.</summary>
        [TestCase(0, "1구역")]
        [TestCase(1, "2구역")]
        [TestCase(7, "8구역")]
        public void FloorLabel_InwardDungeon_UsesSectionNumbering(int progressIndex, string expected)
        {
            Assert.AreEqual(
                expected,
                DungeonDirectionRules.FloorLabelFor(
                    DungeonProgressDirection.Inward,
                    firstBuildingFloor: -1, // 무시돼야 한다
                    progressIndex));
        }

        /// <summary>낙하는 하강 던전에서만 진행을 앞당긴다. 진입 깊이 던전에선 그냥 지형 위험이다.</summary>
        [Test]
        public void Fall_IsUnrelatedToProgress_InInwardDungeons()
        {
            Assert.IsFalse(DungeonDirectionRules.FallAdvancesProgress(DungeonProgressDirection.Inward));
        }

        [Test]
        public void OnwardAndBackStairs_AreNeverTheSameTile()
        {
            foreach (DungeonProgressDirection direction in
                     new[]
                     {
                         DungeonProgressDirection.Descend,
                         DungeonProgressDirection.Ascend,
                         DungeonProgressDirection.Inward
                     })
            {
                Assert.AreNotEqual(
                    DungeonDirectionRules.OnwardStair(direction),
                    DungeonDirectionRules.BackStair(direction),
                    "진출과 귀환이 같은 타일이면 층 전환 링크가 자기 자신을 가리킨다");
            }
        }

        /// <summary>
        /// 중력은 방향을 타지 않는다. 낙하는 언제나 아래로 향하므로, 하강 던전에서는
        /// 전진 지름길이고 상승 던전에서는 역행(=후퇴·탈출 수단)이다 — GDD §5.3 주석.
        /// </summary>
        [Test]
        public void Fall_AdvancesOnlyInDescendingDungeons()
        {
            Assert.IsTrue(DungeonDirectionRules.FallAdvancesProgress(DungeonProgressDirection.Descend));
            Assert.IsFalse(DungeonDirectionRules.FallAdvancesProgress(DungeonProgressDirection.Ascend));
        }

        /// <summary>폐병원: B2 → B1 → 1F → … → 8F. 건물에는 0층이 없다.</summary>
        [TestCase(0, "B2")]
        [TestCase(1, "B1")]
        [TestCase(2, "1F")]
        [TestCase(3, "2F")]
        [TestCase(9, "8F")]
        public void FloorLabel_AscendingHospital_SkipsGroundZero(int progressIndex, string expected)
        {
            Assert.AreEqual(
                expected,
                DungeonDirectionRules.FloorLabelFor(
                    DungeonProgressDirection.Ascend,
                    firstBuildingFloor: -2,
                    progressIndex));
        }

        /// <summary>하강 던전은 기존 표기 그대로 B1 → B10.</summary>
        [TestCase(0, "B1")]
        [TestCase(1, "B2")]
        [TestCase(9, "B10")]
        public void FloorLabel_DescendingDungeon_KeepsBasementNumbering(int progressIndex, string expected)
        {
            Assert.AreEqual(
                expected,
                DungeonDirectionRules.FloorLabelFor(
                    DungeonProgressDirection.Descend,
                    firstBuildingFloor: -1,
                    progressIndex));
        }

        /// <summary>지상에서 시작해 올라가는 탑도 같은 규칙으로 나온다.</summary>
        [TestCase(0, "1F")]
        [TestCase(1, "2F")]
        [TestCase(5, "6F")]
        public void FloorLabel_GroundStartTower_CountsUpFromFirstFloor(int progressIndex, string expected)
        {
            Assert.AreEqual(
                expected,
                DungeonDirectionRules.FloorLabelFor(
                    DungeonProgressDirection.Ascend,
                    firstBuildingFloor: 1,
                    progressIndex));
        }

        /// <summary>카탈로그가 방향을 실제로 들고 있어야 생성기가 읽을 수 있다.</summary>
        [Test]
        public void Catalog_DeclaresDirectionPerDungeon()
        {
            DungeonDefinition hospital = DungeonCatalog.ById(DungeonCatalog.DefaultId);
            Assert.AreEqual(DungeonProgressDirection.Ascend, hospital.Direction);
            Assert.AreEqual(-2, hospital.FirstBuildingFloor, "폐병원은 B2에서 시작한다");

            DungeonDefinition vault = DungeonCatalog.ById("flooded-vault");
            Assert.AreEqual(DungeonProgressDirection.Descend, vault.Direction,
                "던전마다 방향이 다르다 — 전역 스위치가 아니다");
        }
    }
}
