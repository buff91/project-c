using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 판 사이에 유지되는 메타 창고 (extraction 규칙의 저장소).
    /// 생환 시: 전리품은 골드로 환산해 적립, 남은 소모품은 여기 보관된다.
    /// 새 판 시작 시 소모품은 전량 반입("가지고 들어간다") — 죽으면 그 판 소지품은 소실.
    /// </summary>
    [Serializable]
    public class MetaSaveData
    {
        public int gold;
        public string[] unlockedHeroes = { "knight" };
        public int potions;
        public int bombs;
        public int frostBombs;
        public int oilFlasks;
        public int knives;
        public int scrolls;

        public int GetCount(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return potions;
                case ItemKind.Bomb: return bombs;
                case ItemKind.FrostBomb: return frostBombs;
                case ItemKind.OilFlask: return oilFlasks;
                case ItemKind.ThrowingKnife: return knives;
                case ItemKind.RecallScroll: return scrolls;
                default: return 0; // 전리품은 보관하지 않는다 — 항상 골드로 환산
            }
        }

        public void AddCount(ItemKind kind, int amount)
        {
            switch (kind)
            {
                case ItemKind.Potion: potions += amount; break;
                case ItemKind.Bomb: bombs += amount; break;
                case ItemKind.FrostBomb: frostBombs += amount; break;
                case ItemKind.OilFlask: oilFlasks += amount; break;
                case ItemKind.ThrowingKnife: knives += amount; break;
                case ItemKind.RecallScroll: scrolls += amount; break;
            }
        }

        public void ClearItems()
        {
            potions = bombs = frostBombs = oilFlasks = knives = scrolls = 0;
        }

        /// <summary>골드가 충분하면 차감하고 true. 상점 구매/해금 공통 경로.</summary>
        public bool TrySpend(int cost)
        {
            if (cost < 0) throw new ArgumentOutOfRangeException(nameof(cost));
            if (gold < cost) return false;
            gold -= cost;
            return true;
        }

        public bool IsHeroUnlocked(string heroId)
        {
            if (unlockedHeroes == null) return false;
            foreach (string id in unlockedHeroes)
                if (id == heroId) return true;
            return false;
        }

        public void UnlockHero(string heroId)
        {
            if (IsHeroUnlocked(heroId)) return;
            var next = new string[(unlockedHeroes?.Length ?? 0) + 1];
            unlockedHeroes?.CopyTo(next, 0);
            next[next.Length - 1] = heroId;
            unlockedHeroes = next;
        }
    }
}
