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
        public bool IsAvailable { get; }

        public DungeonDefinition(
            string id,
            string displayName,
            string description,
            string routeLabel,
            int seed,
            bool isAvailable)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            RouteLabel = routeLabel;
            Seed = seed;
            IsAvailable = isAvailable;
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
                "잊힌 지하묘지",
                "제한된 시야 속에서 세 구역을 내려가 최심층에 도달한다.",
                "3개 구역 · 권장: 기사",
                seed: 1977,
                isAvailable: true),
            new DungeonDefinition(
                "flooded-vault",
                "침수된 금고",
                "물과 빙결 반응이 중심인 다음 원정지.",
                "준비 중",
                seed: 2718,
                isAvailable: false),
            new DungeonDefinition(
                "ember-keep",
                "잿불 성채",
                "불·기름 연쇄 반응이 중심인 고난도 원정지.",
                "준비 중",
                seed: 3141,
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
