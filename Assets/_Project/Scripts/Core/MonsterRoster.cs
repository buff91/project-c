using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 몬스터 명단과 깊이별 혼합 규칙. (M5 콘텐츠 — 밸런스 수치는 여기 한 곳에서)
    /// 새 몬스터 = archetype 추가 + 깊이 혼합표 갱신 + 스프라이트 슬롯이면 끝난다.
    /// </summary>
    public static class MonsterRoster
    {
        // 등반 가능 여부는 **실루엣과 일치**시킨다 — 인간형은 오르고 비등반 기계는 못 오른다.
        // 그래야 플레이어가 규칙을 배우지 않고도 "저건 못 따라오겠다"를 눈으로 판단한다.

        /// <summary>
        /// 점거군 돌격병(코드 ID Goblin): 기준 몬스터. 아프게 공격하지만 빈사가 되면 후퇴한다.
        /// 인간형이라 <b>사다리를 탄다</b> — 높은 곳으로 도망쳐도 이쪽은 따라온다.
        /// </summary>
        public static readonly MonsterArchetype Goblin =
            new MonsterArchetype("Goblin", maxHp: 5, attackPower: 2,
                aggroRange: 6, patrolRadius: 2, fleeThreshold: 0.3f, displayName: "점거군 돌격병",
                canClimb: true);

        /// <summary>
        /// 기업 진압 로봇(코드 ID Skeleton): 느리게 눈치채지만 단단하고 아프다. 도주하지 않는다.
        /// 기계라 <b>사다리를 못 탄다</b> — 캐치워크가 이 녀석에 대한 피난처가 된다.
        /// </summary>
        public static readonly MonsterArchetype Skeleton =
            new MonsterArchetype("Skeleton", maxHp: 8, attackPower: 2,
                aggroRange: 5, patrolRadius: 1, fleeThreshold: 0f, displayName: "기업 진압 로봇");

        /// <summary>
        /// 기업 추적 드론(코드 ID Slime): 약하고 흔한 사족 보안기. 넓게 배회하며 근접 시
        /// 손상된 군중제압제 주입턱을 쓴다. 저상 기계라 <b>사다리를 못 탄다</b>.
        /// </summary>
        public static readonly MonsterArchetype Slime =
            new MonsterArchetype("Slime", maxHp: 3, attackPower: 1,
                aggroRange: 4, patrolRadius: 3, fleeThreshold: 0f,
                displayName: "기업 추적 드론");

        /// <summary>
        /// 기업 보안 사수(코드 ID Slinger): 유일한 원거리 교전 몬스터. 멀리서 먼저 보고 사격하며,
        /// 붙으면 약하고 먼저 물러선다. 플레이어의 무피해 카이팅에 대한 반대 압력이자
        /// 엄폐·높이·돌진을 의미 있게 만드는 자리다. (수치는 실플레이 전 임시)
        /// </summary>
        public static readonly MonsterArchetype Slinger =
            new MonsterArchetype("Slinger", maxHp: 4, attackPower: 1,
                aggroRange: 7, patrolRadius: 2, fleeThreshold: 0.25f, displayName: "기업 보안 사수",
                rangedRange: 4, rangedPower: 2, keepAwayRange: 2,
                // 인간형이라 오른다. 게다가 고지대가 사거리 예산에 유리하므로
                // 사수가 캐치워크를 차지하러 올라오는 그림이 자연스럽다.
                canClimb: true);

        /// <summary>
        /// 침수된 금고 전용 합선 검사 드론. 직접 화력은 낮지만 명중점 주변과 이어진 웅덩이를 함께
        /// 통전시켜, 물 위의 안전한 장거리 교환을 깨뜨린다. 기계라 사다리는 못 탄다.
        /// </summary>
        public static readonly MonsterArchetype ArcDrone =
            new MonsterArchetype("ArcDrone", maxHp: 5, attackPower: 1,
                aggroRange: 7, patrolRadius: 2, fleeThreshold: 0f, displayName: "합선 검사 드론",
                rangedRange: 4, rangedPower: 2, keepAwayRange: 2,
                rangedEffect: MonsterRangedEffect.ConductiveShock);

        /// <summary>
        /// 첫 던전 보스: 추격 범위가 넓고 도주하지 않는 사이버사이코 집행관
        /// "감시자"(코드 ID GraveWarden).
        /// 표시명을 여기서 주는 이유는 일반 5종과 같다 — 주지 않으면 <see cref="MonsterArchetype.DisplayName"/>이
        /// 코드 ID 로 떨어져 화면에 "GraveWarden" 이 뜬다.
        /// </summary>
        public static readonly MonsterArchetype GraveWarden =
            new MonsterArchetype("GraveWarden", maxHp: 20, attackPower: 3,
                aggroRange: 8, patrolRadius: 1, fleeThreshold: 0f,
                displayName: "감시자",
                canClimb: true);

        /// <summary>깊이 비례로 스폰되는 일반 몬스터(보스 제외). 피해 소스 접두사 매칭 순서를 겸한다.</summary>
        public static readonly IReadOnlyList<MonsterArchetype> Regular =
            new[] { Goblin, Skeleton, Slime, Slinger, ArcDrone };

        /// <summary>
        /// 화면에 등장할 수 있는 전체 적군. 테마 검증과 피해 소스 표시가 같은 명단을 공유한다.
        /// 내부 ID는 호환용이고, 플레이어에게 보이는 정체성은 각 아키타입의 DisplayName이 소유한다.
        /// </summary>
        public static readonly IReadOnlyList<MonsterArchetype> All =
            new[] { Goblin, Skeleton, Slime, Slinger, ArcDrone, GraveWarden };

        /// <summary>
        /// 피해 소스 문자열("Goblin B2-1")의 접두사에 해당하는 몬스터 아키타입을 찾는다.
        /// 정산 문구·텔레메트리 그룹화가 공유하는 단일 매칭 규칙. 없으면 null.
        /// </summary>
        public static MonsterArchetype MatchSource(string source)
        {
            if (string.IsNullOrEmpty(source)) return null;
            foreach (MonsterArchetype archetype in All)
                if (source.StartsWith(archetype.Id, StringComparison.Ordinal)) return archetype;
            return null;
        }

        /// <summary>
        /// <b>(지역 × 깊이)</b>별 혼합 (depth 0 = 첫 층). 얕은 밴드는 추적 드론/돌격병부터 시작하고,
        /// 진행할수록 진압 로봇(Skeleton) 비중이 커지며 그 기준선을 지역이 정한다.
        /// 가중치는 <see cref="DungeonBandProfiles"/>가 소유한다(밴드 경계 SSOT는
        /// <see cref="DungeonDepthBandRules"/>). 롤은 한 번만 — 같은 seed·지역·depth는
        /// 항상 같은 결과. (GDD §5.7 난이도·깊이 연동)
        /// <para>
        /// <b>지역은 얼굴과 비중만 가른다.</b> 위 아키타입의 HP·공격력·행동 트리는 전 지역 공용이다.
        /// </para>
        /// </summary>
        public static MonsterArchetype PickForDepth(
            DungeonRegionProfile region, int depth, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            DungeonBandProfile profile = DungeonBandProfiles.ForDepth(region, depth);
            int roll = random.Next(0, profile.TotalWeight);
            if (roll < profile.SlimeWeight) return Slime;
            roll -= profile.SlimeWeight;
            if (roll < profile.GoblinWeight) return Goblin;
            roll -= profile.GoblinWeight;
            if (roll < profile.SkeletonWeight) return Skeleton;
            roll -= profile.SkeletonWeight;
            if (roll < profile.SlingerWeight) return Slinger;
            return ArcDrone;
        }
    }
}
