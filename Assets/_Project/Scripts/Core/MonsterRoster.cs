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
        /// <summary>약탈자(코드 ID Goblin): 기준 몬스터. 아프게 공격하지만 겁이 많아 빈사가 되면 도망친다.</summary>
        public static readonly MonsterArchetype Goblin =
            new MonsterArchetype("Goblin", maxHp: 5, attackPower: 2,
                aggroRange: 6, patrolRadius: 2, fleeThreshold: 0.3f, displayName: "약탈자");

        /// <summary>낡은 경비 드론(코드 ID Skeleton): 느리게 눈치채지만 단단하고 아프다. 도주하지 않는다.</summary>
        public static readonly MonsterArchetype Skeleton =
            new MonsterArchetype("Skeleton", maxHp: 8, attackPower: 2,
                aggroRange: 5, patrolRadius: 1, fleeThreshold: 0f, displayName: "낡은 경비 드론");

        /// <summary>누출 오염 슬러지(코드 ID Slime): 약하고 흔하다. 넓게 배회하며 겁 없이 달려든다.</summary>
        public static readonly MonsterArchetype Slime =
            new MonsterArchetype("Slime", maxHp: 3, attackPower: 1,
                aggroRange: 4, patrolRadius: 3, fleeThreshold: 0f, displayName: "누출 오염 슬러지");

        /// <summary>첫 던전 보스: 추격 범위가 넓고 도주하지 않는 감시자(코드 ID GraveWarden).</summary>
        public static readonly MonsterArchetype GraveWarden =
            new MonsterArchetype("GraveWarden", maxHp: 20, attackPower: 3,
                aggroRange: 8, patrolRadius: 1, fleeThreshold: 0f);

        /// <summary>깊이 비례로 스폰되는 일반 몬스터(보스 제외). 피해 소스 접두사 매칭 순서를 겸한다.</summary>
        public static readonly IReadOnlyList<MonsterArchetype> Regular = new[] { Goblin, Skeleton, Slime };

        /// <summary>
        /// 피해 소스 문자열("Goblin B2-1")의 접두사에 해당하는 일반 몬스터 아키타입을 찾는다.
        /// 정산 문구·텔레메트리 그룹화가 공유하는 단일 매칭 규칙. 없으면 null.
        /// </summary>
        public static MonsterArchetype MatchSource(string source)
        {
            if (string.IsNullOrEmpty(source)) return null;
            foreach (MonsterArchetype archetype in Regular)
                if (source.StartsWith(archetype.Id, StringComparison.Ordinal)) return archetype;
            return null;
        }

        /// <summary>
        /// 깊이 구간(밴드)별 혼합 (depth 0 = 최상층 B1). 얕은 밴드는 슬러지/약탈자만,
        /// 깊어질수록 경비 드론(Skeleton) 비중이 커진다. 가중치는 <see cref="DungeonBandProfiles"/>가
        /// 소유한다(밴드 경계 SSOT는 <see cref="DungeonDepthBandRules"/>). 롤은 한 번만 —
        /// 같은 seed·depth는 항상 같은 결과. (GDD §5.7 난이도·깊이 연동)
        /// </summary>
        public static MonsterArchetype PickForDepth(int depth, Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));

            DungeonBandProfile profile = DungeonBandProfiles.ForDepth(depth);
            int roll = random.Next(0, profile.TotalWeight);
            if (roll < profile.SlimeWeight) return Slime;
            if (roll < profile.SlimeWeight + profile.GoblinWeight) return Goblin;
            return Skeleton;
        }
    }
}
