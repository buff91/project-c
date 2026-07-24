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
            bool isAvailable)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            RouteLabel = routeLabel;
            Seed = seed;
            FloorCount = floorCount;
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
        public const string DefaultId = "forgotten-catacombs";

        public static readonly IReadOnlyList<DungeonDefinition> All = new[]
        {
            new DungeonDefinition(
                DefaultId,
                "무너진 환승역",
                "붕괴한 지하 10층을 내려가 최심층의 감시자를 쓰러뜨리고 출구를 연다.",
                "B1~B10 · B10 보스: 감시자 · 권장: 기사",
                seed: 1977,
                floorCount: 10,
                boss: new DungeonBossDefinition(
                    "grave-warden",
                    "감시자",
                    MonsterRoster.GraveWarden),
                isAvailable: true),
            new DungeonDefinition(
                "flooded-vault",
                "침수된 금고",
                "물과 빙결 반응이 중심인 다음 원정지.",
                "준비 중",
                seed: 2718,
                floorCount: 10,
                boss: null,
                isAvailable: false),
            new DungeonDefinition(
                "ember-keep",
                "잿불 성채",
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
