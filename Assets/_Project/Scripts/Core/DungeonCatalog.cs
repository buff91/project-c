using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>허브에서 고르는 던전 목적지. 실제 플레이 가능한 항목과 예고 항목을 함께 제공한다.</summary>
    public sealed class DungeonDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public string RouteLabel { get; }
        public int Seed { get; }
        public int FloorCount { get; }

        /// <summary>
        /// 주 진행 방향. 던전별 정체성 축이다 — 하강이 주 목적인 던전과
        /// 상승이 주 목적인 던전이 함께 존재한다(GDD §10.1, §11).
        /// </summary>
        public DungeonProgressDirection Direction { get; }

        /// <summary>
        /// 던전이 시작하는 건물 층 번호(0 없음). 폐병원은 −2(B2), 지하 던전은 −1(B1).
        /// 표시 라벨만 정하며 좌표계에는 영향을 주지 않는다.
        /// </summary>
        public int FirstBuildingFloor { get; }

        public DungeonBossDefinition Boss { get; }
        public bool IsAvailable { get; }

        public DungeonDefinition(
            string id,
            string displayName,
            string description,
            string routeLabel,
            int seed,
            int floorCount,
            DungeonBossDefinition boss,
            bool isAvailable,
            DungeonProgressDirection direction = DungeonProgressDirection.Descend,
            int firstBuildingFloor = -1)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            RouteLabel = routeLabel;
            Seed = seed;
            FloorCount = floorCount;
            Direction = direction;
            FirstBuildingFloor = firstBuildingFloor;
            Boss = boss;
            IsAvailable = isAvailable;
        }
    }

    /// <summary>던전 최심층을 지키는 보스 데이터. 전투 수치는 MonsterRoster에서 공유한다.</summary>
    public sealed class DungeonBossDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public MonsterArchetype Archetype { get; }

        public DungeonBossDefinition(string id, string displayName, MonsterArchetype archetype)
        {
            Id = id;
            DisplayName = displayName;
            Archetype = archetype;
        }
    }

    /// <summary>던전 선택 화면과 절차 생성 seed가 공유하는 단일 목록.</summary>
    public static class DungeonCatalog
    {
        // 코드 ID 는 리스킨 프로파일 A 원칙에 따라 유지한다(세이브·체크포인트 호환).
        // 표시명만 폐병원으로 바꿨다 — GDD §10.1.
        // 생성기가 아래 Direction 을 실제로 읽는다 — 표시와 구조가 일치한다.
        // 방향은 던전별 데이터이며 전역 스위치가 아니다: 아래 셋이 서로 다른 방향으로 공존한다.
        public const string DefaultId = "forgotten-catacombs";

        public static readonly IReadOnlyList<DungeonDefinition> All = new[]
        {
            new DungeonDefinition(
                DefaultId,
                "폐병원",
                "지하 기계실에서 시작해 무너진 병동을 타고 올라간다. " +
                "최상층의 감시자를 쓰러뜨려야 옥상 출구가 열린다.",
                "B2 → 8F + 옥상 · 최상층 보스: 감시자 · 권장: 기사",
                seed: 1977,
                floorCount: 10,
                boss: new DungeonBossDefinition(
                    "grave-warden",
                    "감시자",
                    MonsterRoster.GraveWarden),
                isAvailable: true,
                direction: DungeonProgressDirection.Ascend,
                firstBuildingFloor: -2),
            new DungeonDefinition(
                "flooded-vault",
                "침수된 금고",
                "고도가 아니라 안으로 파고드는 구조. 물과 빙결 반응이 중심인 다음 원정지.",
                "준비 중",
                seed: 2718,
                floorCount: 10,
                boss: null,
                isAvailable: false,
                // 고도가 진행 축이 아니다 — 구역 번호로 표기하고 오르내림은 국소 지형이다.
                direction: DungeonProgressDirection.Inward,
                firstBuildingFloor: -1),
            new DungeonDefinition(
                "ember-keep",
                "잿불 성채",
                // 진행 방향 미정 — 기본값(하강)을 쓴다. 성채라면 상승이 어울리지만 확정 전이다.
                "불·기름 연쇄 반응이 중심인 고난도 원정지.",
                "준비 중",
                seed: 3141,
                floorCount: 10,
                boss: null,
                isAvailable: false)
        };

        public static DungeonDefinition ById(string id)
        {
            foreach (DungeonDefinition dungeon in All)
            {
                if (dungeon.Id == id) return dungeon;
            }
            return All[0];
        }
    }
}
