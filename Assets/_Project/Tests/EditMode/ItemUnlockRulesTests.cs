using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 조건 달성 기반 도구 해금의 계약. 여기서 지키는 것은 두 가지 함정이다 —
    /// <b>조건의 순환</b>(잠긴 도구로만 오르는 계측을 조건으로 걸면 영원히 못 연다)과
    /// <b>첫 판 파괴</b>(생존·경제·조합에 필요한 것을 잠그면 첫 판이 망가진다).
    /// </summary>
    public class ItemUnlockRulesTests
    {
        [Test]
        public void EveryCondition_UsesAMetricReachableWithTheStarterPool()
        {
            // 이 테스트가 이 기능의 가장 중요한 안전장치다. FreezeApplications/OilIgnited/
            // WaterFrozen 은 잠긴 도구가 있어야 오르므로 조건으로 쓰면 영원히 못 연다.
            foreach (ItemUnlockCondition condition in ItemUnlockRules.Conditions)
                Assert.Contains(
                    condition.Metric,
                    ItemUnlockRules.StarterReachableMetrics.ToList(),
                    $"{condition.Kind} 의 조건 축({condition.Metric})은 시작 풀만으로 올릴 수 없다 — " +
                    "그 도구가 없으면 조건도 못 채우는 순환이다.");
        }

        [Test]
        public void StarterReachableMetrics_ExcludeTheGatedElementMetrics()
        {
            // 순환 목록 자체가 느슨해지지 않게 반대 방향으로도 고정한다.
            var reachable = ItemUnlockRules.StarterReachableMetrics.ToList();
            CollectionAssert.DoesNotContain(reachable, BountyMetric.FreezeApplications);
            CollectionAssert.DoesNotContain(reachable, BountyMetric.OilIgnited);
            CollectionAssert.DoesNotContain(reachable, BountyMetric.WaterFrozen);
        }

        [Test]
        public void SurvivalAndEconomyItems_AreNeverGated()
        {
            // 잠그면 첫 판이 망가지는 것들. 물약=생존, 폭탄=상호작용 교육,
            // 통조림=배고픔 시계, 전리품=골드 경제, 약초/화약=조합 화면.
            ItemKind[] mustStayOpen =
            {
                ItemKind.Potion, ItemKind.Bomb, ItemKind.CannedFood,
                ItemKind.CoinPouch, ItemKind.Gemstone, ItemKind.Relic,
                ItemKind.Herb, ItemKind.BlastPowder
            };

            foreach (ItemKind kind in mustStayOpen)
                Assert.IsFalse(
                    ItemUnlockRules.RequiresUnlock(kind),
                    $"{kind} 를 잠그면 첫 판이 망가진다.");
        }

        [Test]
        public void EveryConditionHasAReadableRequirementSentence()
        {
            foreach (ItemUnlockCondition condition in ItemUnlockRules.Conditions)
            {
                Assert.IsNotEmpty(condition.Requirement, $"{condition.Kind} 조건 문장이 비었다.");
                Assert.Greater(condition.Target, 0);
            }
        }

        [Test]
        public void Resolve_SubstitutesUntilUnlocked_ThenReturnsTheRealThing()
        {
            var none = new List<ItemKind>();
            Assert.AreEqual(
                ItemKind.Bomb,
                ItemUnlockRules.Resolve(ItemKind.FrostBomb, none),
                "미해금 냉기 폭탄은 가장 가까운 형제(폭탄)로 치환된다.");

            var unlocked = new List<ItemKind> { ItemKind.FrostBomb };
            Assert.AreEqual(
                ItemKind.FrostBomb,
                ItemUnlockRules.Resolve(ItemKind.FrostBomb, unlocked));
        }

        [Test]
        public void Resolve_NeverSubstitutesUngatedKinds()
        {
            var none = new List<ItemKind>();
            foreach (ItemKind kind in new[]
                     {
                         ItemKind.Potion, ItemKind.Bomb, ItemKind.CannedFood,
                         ItemKind.Relic, ItemKind.Herb
                     })
                Assert.AreEqual(kind, ItemUnlockRules.Resolve(kind, none));
        }

        [Test]
        public void Fallbacks_AreNeverThemselvesGated()
        {
            // 치환 대상이 또 잠겨 있으면 미해금 아이템이 그대로 남는다.
            foreach (ItemUnlockCondition condition in ItemUnlockRules.Conditions)
            {
                ItemKind fallback = ItemUnlockRules.FallbackFor(condition.Kind);
                Assert.AreNotEqual(condition.Kind, fallback, $"{condition.Kind} 치환이 자기 자신이다.");
                Assert.IsFalse(
                    ItemUnlockRules.RequiresUnlock(fallback),
                    $"{condition.Kind} → {fallback} 치환 대상도 잠겨 있다.");
            }
        }

        // ── 판 종료 판정 ────────────────────────────────────────────────

        private static RunTelemetry TelemetryWith(BountyMetric metric, int value)
        {
            var telemetry = new RunTelemetry();
            switch (metric)
            {
                case BountyMetric.Kills: telemetry.kills = value; break;
                case BountyMetric.BurnApplications: telemetry.burnApplications = value; break;
                case BountyMetric.BarrelPushes: telemetry.barrelPushes = value; break;
                case BountyMetric.SecretRoomsFound: telemetry.secretRoomsFound = value; break;
                case BountyMetric.EnemyFalls: telemetry.enemyFalls = value; break;
                default: Assert.Fail($"테스트가 {metric} 를 채우는 방법을 모른다."); break;
            }
            return telemetry;
        }

        [Test]
        public void EvaluateUnlocks_OpensOnlyWhenTargetIsMet()
        {
            ItemUnlockCondition knife = ItemUnlockRules.Find(ItemKind.ThrowingKnife);
            var none = new List<ItemKind>();

            Assert.IsEmpty(
                ItemUnlockRules.EvaluateUnlocks(
                    TelemetryWith(knife.Metric, knife.Target - 1), none),
                "목표에 하나 모자라면 열리지 않는다.");

            List<ItemUnlockCondition> opened = ItemUnlockRules.EvaluateUnlocks(
                TelemetryWith(knife.Metric, knife.Target), none);
            CollectionAssert.Contains(opened.Select(c => c.Kind).ToList(), ItemKind.ThrowingKnife);
        }

        [Test]
        public void EvaluateUnlocks_DoesNotReopenWhatIsAlreadyUnlocked()
        {
            ItemUnlockCondition knife = ItemUnlockRules.Find(ItemKind.ThrowingKnife);
            RunTelemetry telemetry = TelemetryWith(knife.Metric, knife.Target);

            var unlocked = new List<ItemKind> { ItemKind.ThrowingKnife };
            Assert.IsEmpty(
                ItemUnlockRules.EvaluateUnlocks(telemetry, unlocked),
                "두 번 불려도 중복 해금이 생기면 안 된다.");
        }

        [Test]
        public void ClosestPending_PicksTheNearestGoal_AndNullWhenAllOpen()
        {
            ItemUnlockCondition knife = ItemUnlockRules.Find(ItemKind.ThrowingKnife);
            // 처치를 목표 직전까지 올리면 그것이 가장 가까운 목표여야 한다.
            RunTelemetry telemetry = TelemetryWith(knife.Metric, knife.Target - 1);

            ItemUnlockCondition closest =
                ItemUnlockRules.ClosestPending(telemetry, new List<ItemKind>());
            Assert.AreEqual(ItemKind.ThrowingKnife, closest.Kind);

            var all = ItemUnlockRules.Conditions.Select(c => c.Kind).ToList();
            Assert.IsNull(
                ItemUnlockRules.ClosestPending(telemetry, all),
                "전부 열렸으면 안내할 다음 목표가 없다.");
        }

        [Test]
        public void UnlockedCount_TracksProgressTowardTheCodexTotal()
        {
            Assert.AreEqual(0, ItemUnlockRules.UnlockedCount(new List<ItemKind>()));
            Assert.AreEqual(
                ItemUnlockRules.TotalCount,
                ItemUnlockRules.UnlockedCount(
                    ItemUnlockRules.Conditions.Select(c => c.Kind).ToList()));
        }

        // ── 저장 ────────────────────────────────────────────────────────

        [Test]
        public void MetaSave_UnlockItem_IsIdempotentAndSurvivesAsKinds()
        {
            var meta = new MetaSaveData();
            Assert.IsFalse(meta.IsItemUnlocked(ItemKind.FrostBomb));

            Assert.IsTrue(meta.UnlockItem(ItemKind.FrostBomb), "처음 해금은 true 여야 한다.");
            Assert.IsFalse(meta.UnlockItem(ItemKind.FrostBomb), "이미 열린 것은 false 여야 한다.");
            Assert.IsTrue(meta.IsItemUnlocked(ItemKind.FrostBomb));
            CollectionAssert.AreEqual(
                new[] { ItemKind.FrostBomb },
                meta.UnlockedItemKinds());
        }

        [Test]
        public void MetaSave_OldSaveWithoutTheField_ReadsAsNothingUnlocked()
        {
            // 옛 세이브에는 unlockedItems 가 없다 — null 로 들어와도 터지지 않아야 한다.
            var meta = new MetaSaveData { unlockedItems = null };

            Assert.IsFalse(meta.IsItemUnlocked(ItemKind.FrostBomb));
            Assert.IsEmpty(meta.UnlockedItemKinds());
            Assert.IsTrue(meta.UnlockItem(ItemKind.FrostBomb), "null 이어도 해금이 되어야 한다.");
        }
        // ── 생성기 게이트 ───────────────────────────────────────────────

        private static List<ItemKind> ItemKindsIn(DungeonLayout dungeon)
        {
            var kinds = new List<ItemKind>();
            foreach (DungeonFloorInfo floor in dungeon.Floors)
            foreach (ItemSpawn spawn in floor.Items)
                kinds.Add(spawn.Kind);
            return kinds;
        }

        private static DungeonLayout Build(DungeonMetaContext meta, out GridMap map, int seed = 1977)
        {
            map = new GridMap();
            return DungeonGenerator.Generate(
                map, 13, 13, 10, 4, seed,
                DungeonProgressDirection.Descend, -1, meta);
        }

        [Test]
        public void FullyUnlocked_ProducesTheSameDungeonAsNoGateAtAll()
        {
            // 게이트가 기존 밸런스를 바꾸지 않음을 고정한다 — 전부 해금한 상태는
            // 게이트를 아예 안 건 상태와 같은 던전이어야 한다.
            var all = ItemUnlockRules.Conditions.Select(c => c.Kind).ToList();

            DungeonLayout gated = Build(DungeonMetaContext.FromUnlocked(all), out _);
            DungeonLayout ungated = Build(DungeonMetaContext.Unrestricted, out _);

            CollectionAssert.AreEqual(ItemKindsIn(ungated), ItemKindsIn(gated));
        }

        [Test]
        public void NothingUnlocked_RemovesGatedToolsFromTheDungeon()
        {
            DungeonLayout locked = Build(
                DungeonMetaContext.FromUnlocked(new List<ItemKind>()), out _);

            foreach (ItemUnlockCondition condition in ItemUnlockRules.Conditions)
                CollectionAssert.DoesNotContain(
                    ItemKindsIn(locked),
                    condition.Kind,
                    $"해금 전인 {condition.Kind} 가 던전에 나왔다.");
        }

        [Test]
        public void UnlockingOneTool_MakesItAppearWhileOthersStayHidden()
        {
            var onlyFrost = new List<ItemKind> { ItemKind.FrostBomb };

            // 여러 seed 를 훑어야 한다 — 한 판에 특정 종류가 안 나오는 것은 정상이다.
            bool sawFrost = false;
            for (int seed = 1; seed <= 12 && !sawFrost; seed++)
            {
                DungeonLayout dungeon = Build(DungeonMetaContext.FromUnlocked(onlyFrost), out _, seed);
                List<ItemKind> kinds = ItemKindsIn(dungeon);
                sawFrost |= kinds.Contains(ItemKind.FrostBomb);
                CollectionAssert.DoesNotContain(
                    kinds, ItemKind.OilFlask, "해금하지 않은 기름 병이 나왔다.");
                CollectionAssert.DoesNotContain(
                    kinds, ItemKind.RecallScroll, "해금하지 않은 두루마리가 나왔다.");
            }

            Assert.IsTrue(sawFrost, "해금한 냉기 폭탄이 12개 seed 안에서 한 번도 안 나왔다.");
        }

        [Test]
        public void SameSeedAndSameUnlockState_ProducesTheSameDungeon()
        {
            var unlocked = new List<ItemKind> { ItemKind.ThrowingKnife };

            List<ItemKind> first = ItemKindsIn(
                Build(DungeonMetaContext.FromUnlocked(unlocked), out _, 23));
            List<ItemKind> second = ItemKindsIn(
                Build(DungeonMetaContext.FromUnlocked(unlocked), out _, 23));

            CollectionAssert.AreEqual(first, second);
        }

        [Test]
        public void GateDoesNotChangeTilesOrLayout_OnlyWhichItemsAppear()
        {
            // RNG 스트림을 보존한다는 약속의 핵심: 지형·계단·구멍은 해금 상태와 무관하다.
            DungeonLayout locked = Build(
                DungeonMetaContext.FromUnlocked(new List<ItemKind>()), out GridMap lockedMap);
            DungeonLayout open = Build(DungeonMetaContext.Unrestricted, out GridMap openMap);

            string Tiles(GridMap map) => string.Join(";", map.All()
                .Select(pair => $"{pair.Key}:{pair.Value.kind}")
                .OrderBy(entry => entry, System.StringComparer.Ordinal));

            Assert.AreEqual(Tiles(openMap), Tiles(lockedMap), "해금 상태가 지형을 바꿨다.");
            for (int i = 0; i < open.Floors.Count; i++)
            {
                Assert.AreEqual(open.Floors[i].Hole, locked.Floors[i].Hole);
                Assert.AreEqual(open.Floors[i].RestSite, locked.Floors[i].RestSite);
                CollectionAssert.AreEqual(
                    open.Floors[i].Items.Select(s => s.Position).ToList(),
                    locked.Floors[i].Items.Select(s => s.Position).ToList(),
                    "아이템 위치까지 같아야 한다 — 종류만 달라진다.");
            }
        }

    }
}
