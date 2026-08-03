namespace ProjectC.Core
{
    /// <summary>
    /// 지역별 일반 아이템 드롭 편성. 모든 지역이 같은 23칸 롤을 소비하므로
    /// 지역을 바꾸더라도 생성기의 RNG 스트림은 흔들리지 않는다.
    /// </summary>
    public static class DungeonLootRules
    {
        public const int RollCount = 23;

        public static ItemKind Resolve(
            DungeonRegionProfile region,
            int progressIndex,
            int roll)
        {
            if (roll < 0 || roll >= RollCount)
                throw new System.ArgumentOutOfRangeException(nameof(roll));

            return region == DungeonRegionProfile.Flooded
                ? ResolveFlooded(progressIndex, roll)
                : ResolveFacility(progressIndex, roll);
        }

        private static ItemKind ResolveFacility(int progressIndex, int roll)
        {
            if (roll < 3) return ItemKind.Potion;
            if (roll < 6) return ItemKind.Bomb;
            if (roll < 7) return ItemKind.FrostBomb;
            if (roll < 8) return ItemKind.OilFlask;
            if (roll < 9) return ItemKind.ThrowingKnife;
            if (roll < 10) return ItemKind.RecallScroll;
            if (roll < 15) return ItemKind.CannedFood;
            if (roll < 17) return ItemKind.CoinPouch;
            if (roll < 18) return ItemKind.Gemstone;
            if (roll < 19)
                return progressIndex >= 2 ? ItemKind.Relic : ItemKind.CoinPouch;
            if (roll < 21) return ItemKind.Herb;
            if (roll < 22) return ItemKind.BlastPowder;
            return ItemKind.FrostShard;
        }

        private static ItemKind ResolveFlooded(int progressIndex, int roll)
        {
            // 침수 지역(/23): 응급 키트3 · 폭탄2 · 냉기3 · 투척 볼트1 · 귀환 비컨1 ·
            // 통조림5 · 스크랩2 · 코어 파편1 · 시제품 코어1 · 지혈 패치1 · 화약1 · 냉각 코일2.
            // 기름은 물 위에서 지역 반응을 흐리므로 제외하고 냉기 도구로 자리를 넘긴다.
            if (roll < 3) return ItemKind.Potion;
            if (roll < 5) return ItemKind.Bomb;
            if (roll < 8) return ItemKind.FrostBomb;
            if (roll < 9) return ItemKind.ThrowingKnife;
            if (roll < 10) return ItemKind.RecallScroll;
            if (roll < 15) return ItemKind.CannedFood;
            if (roll < 17) return ItemKind.CoinPouch;
            if (roll < 18) return ItemKind.Gemstone;
            if (roll < 19)
                return progressIndex >= 2 ? ItemKind.Relic : ItemKind.CoinPouch;
            if (roll < 20) return ItemKind.Herb;
            if (roll < 21) return ItemKind.BlastPowder;
            return ItemKind.FrostShard;
        }
    }
}
