using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>
    /// 층 단위 체크포인트 저장 데이터. (로그라이트 이어하기)
    /// 던전은 seed 로 재생성하므로 지형·적·아이템 배치는 저장하지 않는다 —
    /// 이어하기는 "현재 층을 층 입구에서 다시 시작"하는 의미다.
    /// </summary>
    [Serializable]
    public class RunSaveData
    {
        public string heroId;
        public string dungeonId;
        public int seed;
        public int roomSize;
        public int floorCount;
        public int elevationsPerFloor;
        public int stageCount = 1;
        public int stageIndex = 1;
        public int currentFloorIndex;
        public bool bossDefeated;
        public int hp;
        public int potions;
        public int bombs;
        public int frostBombs;
        public int oilFlasks;
        public int knives;
        public int scrolls;
        public int coinPouches;
        public int gemstones;
        public int relics;
        public int herbs;
        public int powders;
        public int frostShards;
        public int kills;
        public int deepestFloorIndex;

        /// <summary>이번 원정에 반입한 장비. 죽으면 잃으므로 런 상태로 들고 다닌다.</summary>
        public string carriedWeaponId = "";
        public string carriedGearId = "";
        public List<int> usedRestFloorIndices = new List<int>();
        public RunTelemetry telemetry;

        /// <summary>
        /// 백팩 ↔ 세이브 종류별 수량 매핑의 단일 출처. 저장(WriteItems)과 복원(AddItemsTo)이
        /// 같은 표를 공유하므로 아이템 하나가 이월에서 조용히 누락될 수 없다.
        /// (필드 자체는 Unity 직렬화 이름을 유지해야 하므로 이름/개수를 바꾸지 말 것)
        /// </summary>
        public void WriteItems(Inventory inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            potions = inventory.Count(ItemKind.Potion);
            bombs = inventory.Count(ItemKind.Bomb);
            frostBombs = inventory.Count(ItemKind.FrostBomb);
            oilFlasks = inventory.Count(ItemKind.OilFlask);
            knives = inventory.Count(ItemKind.ThrowingKnife);
            scrolls = inventory.Count(ItemKind.RecallScroll);
            coinPouches = inventory.Count(ItemKind.CoinPouch);
            gemstones = inventory.Count(ItemKind.Gemstone);
            relics = inventory.Count(ItemKind.Relic);
            herbs = inventory.Count(ItemKind.Herb);
            powders = inventory.Count(ItemKind.BlastPowder);
            frostShards = inventory.Count(ItemKind.FrostShard);
        }

        /// <summary>세이브에 담긴 종류별 수량을 인벤토리에 더한다(WriteItems 의 역). 0 은 건너뛴다.</summary>
        public void AddItemsTo(Inventory inventory)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (potions > 0) inventory.Add(ItemKind.Potion, potions);
            if (bombs > 0) inventory.Add(ItemKind.Bomb, bombs);
            if (frostBombs > 0) inventory.Add(ItemKind.FrostBomb, frostBombs);
            if (oilFlasks > 0) inventory.Add(ItemKind.OilFlask, oilFlasks);
            if (knives > 0) inventory.Add(ItemKind.ThrowingKnife, knives);
            if (scrolls > 0) inventory.Add(ItemKind.RecallScroll, scrolls);
            if (coinPouches > 0) inventory.Add(ItemKind.CoinPouch, coinPouches);
            if (gemstones > 0) inventory.Add(ItemKind.Gemstone, gemstones);
            if (relics > 0) inventory.Add(ItemKind.Relic, relics);
            if (herbs > 0) inventory.Add(ItemKind.Herb, herbs);
            if (powders > 0) inventory.Add(ItemKind.BlastPowder, powders);
            if (frostShards > 0) inventory.Add(ItemKind.FrostShard, frostShards);
        }
    }

    /// <summary>새 판과 이어하기가 시작할 던전 내부 깊이를 결정한다.</summary>
    public static class RunStartRules
    {
        /// <summary>새 판은 반드시 B1(0), 이어하기만 저장된 음수 floor index를 깊이로 변환한다.</summary>
        public static int ResolvePreviewDepth(RunSaveData continueData) =>
            continueData == null ? 0 : Math.Max(0, -continueData.currentFloorIndex);
    }
}
