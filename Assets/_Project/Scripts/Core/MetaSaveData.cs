using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 판 사이에 유지되는 메타 창고 (extraction 규칙의 저장소).
    /// 생환 시: 전리품은 골드로 환산해 적립, 남은 소모품은 여기 보관된다.
    /// 허브에서 출정 백팩으로 고른 물품만 새 판에 반입하며, 죽으면 그 판 소지품은 소실된다.
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
        public int herbs;
        public int powders;
        public int frostShards;
        public int loadoutPotions;
        public int loadoutBombs;
        public int loadoutFrostBombs;
        public int loadoutOilFlasks;
        public int loadoutKnives;
        public int loadoutScrolls;
        public int loadoutHerbs;
        public int loadoutPowders;
        public int loadoutFrostShards;

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
                case ItemKind.Herb: return herbs;
                case ItemKind.BlastPowder: return powders;
                case ItemKind.FrostShard: return frostShards;
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
                case ItemKind.Herb: herbs += amount; break;
                case ItemKind.BlastPowder: powders += amount; break;
                case ItemKind.FrostShard: frostShards += amount; break;
            }
        }

        /// <summary>창고에서 요청 수량만 제거한다. 보유량을 넘는 요청은 실제 제거량만 반환한다.</summary>
        public int RemoveCount(ItemKind kind, int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int removed = Math.Min(GetCount(kind), amount);
            if (removed > 0) AddCount(kind, -removed);
            return removed;
        }

        public int GetLoadoutCount(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return loadoutPotions;
                case ItemKind.Bomb: return loadoutBombs;
                case ItemKind.FrostBomb: return loadoutFrostBombs;
                case ItemKind.OilFlask: return loadoutOilFlasks;
                case ItemKind.ThrowingKnife: return loadoutKnives;
                case ItemKind.RecallScroll: return loadoutScrolls;
                case ItemKind.Herb: return loadoutHerbs;
                case ItemKind.BlastPowder: return loadoutPowders;
                case ItemKind.FrostShard: return loadoutFrostShards;
                default: return 0;
            }
        }

        public void AddLoadoutCount(ItemKind kind, int amount)
        {
            switch (kind)
            {
                case ItemKind.Potion: loadoutPotions += amount; break;
                case ItemKind.Bomb: loadoutBombs += amount; break;
                case ItemKind.FrostBomb: loadoutFrostBombs += amount; break;
                case ItemKind.OilFlask: loadoutOilFlasks += amount; break;
                case ItemKind.ThrowingKnife: loadoutKnives += amount; break;
                case ItemKind.RecallScroll: loadoutScrolls += amount; break;
                case ItemKind.Herb: loadoutHerbs += amount; break;
                case ItemKind.BlastPowder: loadoutPowders += amount; break;
                case ItemKind.FrostShard: loadoutFrostShards += amount; break;
            }
        }

        public int RemoveLoadoutCount(ItemKind kind, int amount)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int removed = Math.Min(GetLoadoutCount(kind), amount);
            if (removed > 0) AddLoadoutCount(kind, -removed);
            return removed;
        }

        public void ClearLoadout()
        {
            loadoutPotions = loadoutBombs = loadoutFrostBombs = 0;
            loadoutOilFlasks = loadoutKnives = loadoutScrolls = 0;
            loadoutHerbs = loadoutPowders = loadoutFrostShards = 0;
        }

        public void ClearItems()
        {
            potions = bombs = frostBombs = oilFlasks = knives = scrolls = 0;
            herbs = powders = frostShards = 0;
            ClearLoadout();
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
