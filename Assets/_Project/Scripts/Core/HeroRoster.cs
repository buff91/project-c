using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>시작 영웅 프리셋. 스탯·시작 아이템만 다르고 조작은 동일하다.</summary>
    public sealed class HeroArchetype
    {
        public string Id { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public int MaxHp { get; }
        public int Attack { get; }
        public int RangedDamage { get; }
        public int StartPotions { get; }
        public int StartBombs { get; }
        public int StartFrostBombs { get; }

        public HeroArchetype(
            string id,
            string displayName,
            string description,
            int maxHp,
            int attack,
            int rangedDamage,
            int startPotions = 0,
            int startBombs = 0,
            int startFrostBombs = 0)
        {
            Id = id;
            DisplayName = displayName;
            Description = description;
            MaxHp = maxHp;
            Attack = attack;
            RangedDamage = rangedDamage;
            StartPotions = startPotions;
            StartBombs = startBombs;
            StartFrostBombs = startFrostBombs;
        }
    }

    /// <summary>영웅 프리셋의 단일 출처. 밸런스 수치는 여기서만 고친다.</summary>
    public static class HeroRoster
    {
        public static readonly IReadOnlyList<HeroArchetype> All = new[]
        {
            new HeroArchetype(
                "knight", "기사", "단단하고 강한 근접전",
                maxHp: 10, attack: 3, rangedDamage: 1, startPotions: 1),
            // 밸런스: 3던전 체인 시뮬(전술 그리디 300판) — 기사 49%/사냥꾼 43%/연금 25% 완주.
            // 사냥꾼 HP 7→8, 연금 ATK 2→3 (근접이 해골 4방이면 체인 완주 불가).
            new HeroArchetype(
                "ranger", "사냥꾼", "원거리가 강한 대신 여림",
                maxHp: 8, attack: 2, rangedDamage: 2),
            new HeroArchetype(
                "alchemist", "연금술사", "폭탄과 냉기로 판을 짠다",
                maxHp: 8, attack: 3, rangedDamage: 1, startBombs: 2, startFrostBombs: 1)
        };

        /// <summary>id 미지정/미상은 첫 영웅(기사)으로 폴백한다.</summary>
        public static HeroArchetype ById(string id)
        {
            foreach (HeroArchetype hero in All)
            {
                if (hero.Id == id) return hero;
            }
            return All[0];
        }
    }
}
