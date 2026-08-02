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
        /// 던전이 시작하는 건물 층 번호(0 없음). 아케이드 타워는 −2(B2), 지하 던전은 −1(B1).
        /// 표시 라벨만 정하며 좌표계에는 영향을 주지 않는다.
        /// </summary>
        public int FirstBuildingFloor { get; }

        /// <summary>
        /// 이 원정지의 콘텐츠 정체성 프로파일. 적 혼합·밀도·반응 무대 확률이 여기서 갈린다
        /// (<see cref="DungeonBandProfiles"/>). <b>던전마다 하나씩이 아니라 공유 가능한 프로파일</b>이라
        /// 같은 결의 원정지가 늘어나도 표가 커지지 않는다.
        /// </summary>
        public DungeonRegionProfile Region { get; }

        /// <summary>
        /// 절차 생성한 한 층 안에서 +1/+2 이동 높이를 사용하는가.
        /// false여도 elevation stride와 층간 계단·Hole·낙하 규칙은 그대로 유지된다.
        /// 던전별 레이아웃 정책이며 공유 가능한 <see cref="Region"/>의 속성이 아니다.
        /// </summary>
        public bool UsesLocalElevation { get; }

        public DungeonBossDefinition Boss { get; }
        public bool IsAvailable { get; }

        /// <summary>
        /// 던전에 처음 발을 들일 때 한 번 띄우는 카드의 제목/본문. 비어 있으면 띄우지 않는다.
        /// <para>
        /// <b>왜 데이터인가.</b> "왜 하필 여기서 시작하는가"는 던전마다 다른 서사다 —
        /// 아케이드 타워는 쉘터 배수 터널이 지하 기계실로 이어지기 때문이고, 다른 원정지는 다른 이유다.
        /// 규칙 계층에 문구를 두면 던전을 늘릴 때마다 분기가 는다.
        /// </para>
        /// </summary>
        public string EntryTitle { get; }

        public string EntryDetail { get; }

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
            int firstBuildingFloor = -1,
            string entryTitle = null,
            string entryDetail = null,
            DungeonRegionProfile region = DungeonRegionProfile.Facility,
            bool usesLocalElevation = true)
        {
            Region = region;
            UsesLocalElevation = usesLocalElevation;
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
            EntryTitle = entryTitle;
            EntryDetail = entryDetail;
        }

        /// <summary>입장 카드를 띄울 문구가 있는가.</summary>
        public bool HasEntryCue =>
            !string.IsNullOrWhiteSpace(EntryTitle) && !string.IsNullOrWhiteSpace(EntryDetail);
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
        // 표시명만 폐 아케이드 복합타워로 바꿨다 — GDD §10.1 v0.3.3(사이버펑크 승격).
        // 생성기가 아래 Direction 을 실제로 읽는다 — 표시와 구조가 일치한다.
        // 방향은 던전별 데이터이며 전역 스위치가 아니다: 아래 셋이 서로 다른 방향으로 공존한다.
        public const string DefaultId = "forgotten-catacombs";

        public static readonly IReadOnlyList<DungeonDefinition> All = new[]
        {
            new DungeonDefinition(
                DefaultId,
                "폐 아케이드 복합타워",
                "쉘터의 배수 터널이 타워 지하 기계실로 이어진다 — 정문 셔터는 오래전에 내려갔다. " +
                "지하 2층에서 시작해 죽은 네온 상가를 타고 올라가고, " +
                "최상층의 감시자를 쓰러뜨려야 옥상 출구가 열린다.",
                "B2 → 8F + 옥상 · 최상층 보스: 감시자 · 추락 대비 장비 권장",
                seed: 1977,
                floorCount: 10,
                boss: new DungeonBossDefinition(
                    "grave-warden",
                    MonsterRoster.GraveWarden.DisplayName,
                    MonsterRoster.GraveWarden),
                isAvailable: true,
                direction: DungeonProgressDirection.Ascend,
                firstBuildingFloor: -2,
                // 왜 정문이 아니라 지하 2층인가 — 시작 지점을 변명이 아니라 제약으로 만든다.
                // 이 한 줄이 "위로 올라가는 것이 유일한 길"과 "옥상이 목표"를 동시에 설명한다.
                entryTitle: "지하 2층 · 기계실",
                entryDetail: "쉘터의 배수 터널이 여기로 이어진다. 타워 정문 셔터는 오래전에 내려갔고, " +
                    "지상으로 나가려면 죽은 상가를 타고 올라가야 한다.",
                // 첫 던전은 층간 구조를 수직성의 주 단위로 삼고, 각 층의 이동 바닥은 하나로 읽힌다.
                usesLocalElevation: false),
            new DungeonDefinition(
                "flooded-vault",
                "침수된 금고",
                "무너진 방수문 너머로 안쪽 구역을 파고든다. 침수된 바닥을 얼려 길을 만들고, " +
                "물 위의 합선 드론이 일으키는 감전 연쇄를 역이용해야 한다.",
                "구역 1 → 10 · 물·빙결·감전 반응 · 냉기 장비 권장",
                seed: 2718,
                floorCount: 10,
                boss: null,
                isAvailable: true,
                // 고도가 진행 축이 아니다 — 구역 번호로 표기하고 오르내림은 국소 지형이다.
                direction: DungeonProgressDirection.Inward,
                firstBuildingFloor: -1,
                // 물 웅덩이가 도처에 있어야 "왜 여기가 다른가"가 첫 층에서 읽힌다.
                region: DungeonRegionProfile.Flooded,
                entryTitle: "구역 1 · 외곽 방수문",
                entryDetail: "배수 장치가 멎어 금고 안쪽이 잠겼다. 물을 얼려 발판을 만들고, " +
                    "합선 드론의 전류가 번지기 전에 깊은 구역으로 진입하라."),
            new DungeonDefinition(
                "ember-keep",
                "잿불 성채",
                // 진행 방향 미정 — 기본값(하강)을 쓴다. 성채라면 상승이 어울리지만 확정 전이다.
                "불·기름 연쇄 반응이 중심인 고난도 원정지.",
                "준비 중",
                seed: 3141,
                floorCount: 10,
                boss: null,
                isAvailable: false,
                // 물이 드물어야 불 연쇄가 선다 — 웅덩이 확률이 Facility 의 절반 이하다.
                region: DungeonRegionProfile.Ember)
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
