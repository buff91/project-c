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
                ItemUnlockRules.EvaluateUnlocks(none, Best(knife.Kind, knife.Target - 1), Zero),
                "목표에 하나 모자라면 열리지 않는다.");

            List<ItemUnlockCondition> opened =
                ItemUnlockRules.EvaluateUnlocks(none, Best(knife.Kind, knife.Target), Zero);
            CollectionAssert.Contains(opened.Select(c => c.Kind).ToList(), ItemKind.ThrowingKnife);

            // 모자란 진행은 버려지지 않는다 — 기록 하나가 그 판을 살린다.
            List<ItemUnlockCondition> byRecords = ItemUnlockRules.EvaluateUnlocks(
                none, Best(knife.Kind, knife.Target - 1), Best(knife.Kind, 1));
            CollectionAssert.Contains(byRecords.Select(c => c.Kind).ToList(), ItemKind.ThrowingKnife);
        }

        [Test]
        public void EvaluateUnlocks_DoesNotReopenWhatIsAlreadyUnlocked()
        {
            ItemUnlockCondition knife = ItemUnlockRules.Find(ItemKind.ThrowingKnife);

            var unlocked = new List<ItemKind> { ItemKind.ThrowingKnife };
            Assert.IsEmpty(
                ItemUnlockRules.EvaluateUnlocks(unlocked, Best(knife.Kind, knife.Target), Zero),
                "두 번 불려도 중복 해금이 생기면 안 된다.");
        }

        /// <summary>한 조건에만 값을 주는 조회 함수. 나머지는 0이다.</summary>
        private static System.Func<ItemKind, int> Best(ItemKind kind, int value) =>
            k => k == kind ? value : 0;

        private static System.Func<ItemKind, int> Zero => _ => 0;

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

        private static string Tiles(GridMap map) => string.Join(";", map.All()
            .Select(pair => $"{pair.Key}:{pair.Value.kind}")
            .OrderBy(entry => entry, System.StringComparer.Ordinal));

        private static List<ItemKind> ItemKindsIn(DungeonLayout dungeon)
        {
            var kinds = new List<ItemKind>();
            foreach (DungeonFloorInfo floor in dungeon.Floors)
            foreach (ItemSpawn spawn in floor.Items)
                kinds.Add(spawn.Kind);
            return kinds;
        }

        /// <summary>모든 동료를 구출한 상태 — NPC 갇힌 방이 생기지 않는다.</summary>
        private static List<string> AllRescued() =>
            ShelterNpcRoster.All.Select(npc => npc.Id).ToList();

        private static DungeonLayout Build(DungeonMetaContext meta, out GridMap map, int seed = 1977)
        {
            map = new GridMap();
            return DungeonGenerator.Generate(
                map, 13, 13, 10, 4, seed,
                DungeonProgressDirection.Descend, -1, meta);
        }

        [Test]
        public void FullyProgressed_ProducesTheSameDungeonAsNoGateAtAll()
        {
            // 게이트가 기존 밸런스를 바꾸지 않음을 고정한다 — 전부 해금하고 전부 구출한 상태는
            // 게이트를 아예 안 건 상태와 같은 던전이어야 한다.
            // (구출까지 넣어야 한다: 미구출 NPC 는 갇힌 방을 만들어 지형을 바꾼다.)
            var all = ItemUnlockRules.Conditions.Select(c => c.Kind).ToList();

            DungeonLayout gated = Build(
                DungeonMetaContext.FromUnlocked(all, AllRescued()), out GridMap gatedMap);
            DungeonLayout ungated = Build(DungeonMetaContext.Unrestricted, out GridMap ungatedMap);

            CollectionAssert.AreEqual(ItemKindsIn(ungated), ItemKindsIn(gated));
            Assert.AreEqual(Tiles(ungatedMap), Tiles(gatedMap), "지형까지 같아야 한다.");
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
        public void ItemGateDoesNotChangeTilesOrLayout_OnlyWhichItemsAppear()
        {
            // RNG 스트림을 보존한다는 약속의 핵심: 지형·계단·구멍은 **도구 해금 상태**와 무관하다.
            // 구출 상태는 고정해 둔다 — 갇힌 방은 실제로 지형을 바꾸는 것이 맞다.
            List<string> rescued = AllRescued();
            var all = ItemUnlockRules.Conditions.Select(c => c.Kind).ToList();

            DungeonLayout locked = Build(
                DungeonMetaContext.FromUnlocked(new List<ItemKind>(), rescued), out GridMap lockedMap);
            DungeonLayout open = Build(
                DungeonMetaContext.FromUnlocked(all, rescued), out GridMap openMap);

            Assert.AreEqual(Tiles(openMap), Tiles(lockedMap), "도구 해금 상태가 지형을 바꿨다.");
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

        // ── 최고 기록 (기록실 안내의 근거) ──────────────────────────────

        [Test]
        public void BestUnlockProgress_OnlyGoesUp()
        {
            // 조건이 한 판 기준이라 지난 판 값을 쓰면 나쁜 판 뒤에 0 으로 돌아가
            // 안내가 쓸모없어진다. 최고 기록은 단조 증가해야 한다.
            var meta = new MetaSaveData();
            Assert.AreEqual(0, meta.BestUnlockProgress(ItemKind.FrostBomb));

            meta.RecordUnlockProgress(ItemKind.FrostBomb, 8);
            Assert.AreEqual(8, meta.BestUnlockProgress(ItemKind.FrostBomb));

            meta.RecordUnlockProgress(ItemKind.FrostBomb, 3);
            Assert.AreEqual(8, meta.BestUnlockProgress(ItemKind.FrostBomb),
                "더 낮은 값이 최고 기록을 깎으면 안 된다.");

            meta.RecordUnlockProgress(ItemKind.FrostBomb, 11);
            Assert.AreEqual(11, meta.BestUnlockProgress(ItemKind.FrostBomb));
        }

        [Test]
        public void BestUnlockProgress_IgnoresNonPositiveAndSurvivesNullList()
        {
            var meta = new MetaSaveData { unlockProgress = null };

            Assert.AreEqual(0, meta.BestUnlockProgress(ItemKind.OilFlask), "옛 세이브는 null 이다.");
            meta.RecordUnlockProgress(ItemKind.OilFlask, 0);
            Assert.AreEqual(0, meta.BestUnlockProgress(ItemKind.OilFlask), "0 은 기록하지 않는다.");

            meta.RecordUnlockProgress(ItemKind.OilFlask, 2);
            Assert.AreEqual(2, meta.BestUnlockProgress(ItemKind.OilFlask));
        }

        [Test]
        public void BestUnlockProgress_TracksEachConditionSeparately()
        {
            var meta = new MetaSaveData();
            meta.RecordUnlockProgress(ItemKind.FrostBomb, 5);
            meta.RecordUnlockProgress(ItemKind.RecallScroll, 1);

            Assert.AreEqual(5, meta.BestUnlockProgress(ItemKind.FrostBomb));
            Assert.AreEqual(1, meta.BestUnlockProgress(ItemKind.RecallScroll));
            Assert.AreEqual(0, meta.BestUnlockProgress(ItemKind.ThrowingKnife));
        }

        // ── 갇힌 방 / NPC 구출 (B단계) ──────────────────────────────────

        [Test]
        public void EveryPendingNpc_AlwaysGetsARescueRoom_AcrossManySeeds()
        {
            // **확률이 아니라 보장**이다. 확률로 두면 운이 나쁜 플레이어의 시설이 영원히
            // 열리지 않고 되돌릴 방법이 없다. 여러 seed 로 예외가 없음을 확인한다.
            for (int seed = 1; seed <= 20; seed++)
            {
                DungeonLayout dungeon = Build(
                    DungeonMetaContext.FromUnlocked(new List<ItemKind>(), new List<string>()),
                    out _, seed);

                foreach (ShelterNpcDefinition npc in ShelterNpcRoster.All)
                {
                    DungeonFloorInfo floor = dungeon.Floors
                        .Single(f => f.ProgressIndex == npc.ProgressIndex);
                    Assert.AreEqual(
                        npc.Id, floor.RescueNpcId,
                        $"seed {seed}: 진행 {npc.ProgressIndex}층에 {npc.DisplayName} 가 없다.");
                    Assert.IsTrue(
                        floor.RescueNpc.HasValue,
                        $"seed {seed}: {npc.DisplayName} 의 자리가 정해지지 않았다.");
                }
            }
        }

        [Test]
        public void FreshSave_PutsMoreThanOneRescueRoom_InTheSameDungeon()
        {
            // 이 사실이 프레젠테이션 계층의 계약이다. 첫 판에는 미구출 NPC 가 전부 —
            // 연락책(2)과 대장장이(5) — 같은 던전에 갇힌 방을 얻는다.
            // 월드 표현이 동료를 **스칼라 한 벌**로 들면 뒤에 만들어진 쪽이 앞을 덮어써서
            // 앞 동료가 참조를 잃은 채 씬에 남는다: 회전 때 다시 투영되지 않아 방과 따로 놀고,
            // FOV 로 가려지지 않아 벽 너머로 보이며, 구출 판정에도 안 걸려 **시설이 영원히
            // 안 열린다**. "등장은 확률이 아니라 보장"이 여기서 조용히 깨졌었다.
            for (int seed = 1; seed <= 20; seed++)
            {
                DungeonLayout dungeon = Build(
                    DungeonMetaContext.FromUnlocked(new List<ItemKind>(), new List<string>()),
                    out _, seed);

                int rescueRooms = dungeon.Floors.Count(f => f.RescueNpc.HasValue);
                Assert.AreEqual(
                    ShelterNpcRoster.All.Count, rescueRooms,
                    $"seed {seed}: 미구출 동료 수와 갇힌 방 수가 다르다");
                Assert.Greater(
                    rescueRooms, 1,
                    "동료가 둘 이상이어야 이 계약이 의미가 있다 — 로스터가 줄었다면 " +
                    "월드 표현이 목록을 유지할 이유도 다시 검토한다");
            }
        }

        [Test]
        public void RescueRoomsNeverOverlapSecretRooms()
        {
            // 숨은 방은 벽처럼 위장해 못 찾을 수 있다 — 거기에 NPC 를 두면 진행이 막힌다.
            for (int seed = 1; seed <= 20; seed++)
            {
                DungeonLayout dungeon = Build(
                    DungeonMetaContext.FromUnlocked(new List<ItemKind>(), new List<string>()),
                    out _, seed);

                foreach (DungeonFloorInfo floor in dungeon.Floors)
                {
                    if (string.IsNullOrEmpty(floor.RescueNpcId)) continue;
                    Assert.IsFalse(
                        floor.HasSecretRoom,
                        $"seed {seed}: 진행 {floor.ProgressIndex}층이 갇힌 방과 숨은 방을 겹쳤다.");
                }
            }
        }

        [Test]
        public void RescueNpc_IsWalkableFromTheFloorEntry()
        {
            DungeonLayout dungeon = Build(
                DungeonMetaContext.FromUnlocked(new List<ItemKind>(), new List<string>()),
                out GridMap map);
            OpenAllDoorsForRescue(map, dungeon);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
            {
                if (!floor.RescueNpc.HasValue) continue;
                Assert.Greater(
                    GridPathfinder.FindPath(map, floor.Entry, floor.RescueNpc.Value).Count, 0,
                    $"진행 {floor.ProgressIndex}층의 동료에게 걸어갈 수 없다.");
            }
        }

        private static void OpenAllDoorsForRescue(GridMap map, DungeonLayout dungeon)
        {
            foreach (DungeonFloorInfo floor in dungeon.Floors)
            foreach (GridPos door in floor.Doors)
                map.Set(door, TileKind.DoorOpen);
        }

        [Test]
        public void RescuedNpcs_GetNoMoreRescueRooms()
        {
            // 같은 사람을 두 번 구출하는 방을 만들지 않는다.
            DungeonLayout dungeon = Build(
                DungeonMetaContext.FromUnlocked(new List<ItemKind>(), AllRescued()), out _);

            foreach (DungeonFloorInfo floor in dungeon.Floors)
                Assert.IsNull(
                    floor.RescueNpcId,
                    $"진행 {floor.ProgressIndex}층에 이미 구출한 동료의 방이 남아 있다.");
        }

        [Test]
        public void Facilities_OpenOnlyAfterTheirNpcIsRescued()
        {
            var none = new List<string>();
            Assert.IsFalse(ShelterNpcRoster.IsFacilityOpen(ShelterFacility.Forge, none));
            Assert.IsFalse(ShelterNpcRoster.IsFacilityOpen(ShelterFacility.BountyBoard, none));

            ShelterNpcDefinition smith = ShelterNpcRoster.ForFacility(ShelterFacility.Forge);
            var withSmith = new List<string> { smith.Id };
            Assert.IsTrue(ShelterNpcRoster.IsFacilityOpen(ShelterFacility.Forge, withSmith));
            Assert.IsFalse(
                ShelterNpcRoster.IsFacilityOpen(ShelterFacility.BountyBoard, withSmith),
                "한 명을 구출해도 다른 시설은 열리지 않는다.");
        }

        [Test]
        public void MetaSave_RescueNpc_IsIdempotentAndSurvivesNullArray()
        {
            var meta = new MetaSaveData { rescuedNpcs = null };
            Assert.IsFalse(meta.IsNpcRescued("smith"), "옛 세이브는 null 이다.");
            Assert.IsFalse(meta.IsFacilityOpen(ShelterFacility.Forge));

            Assert.IsTrue(meta.RescueNpc("smith"));
            Assert.IsFalse(meta.RescueNpc("smith"), "이미 합류한 동료는 false 여야 한다.");
            Assert.IsTrue(meta.IsFacilityOpen(ShelterFacility.Forge));
        }

        [Test]
        public void EachNpcOpensADistinctFacility()
        {
            // 두 NPC 가 같은 시설을 열면 한쪽 구출이 의미가 없어진다.
            var facilities = ShelterNpcRoster.All.Select(npc => npc.Facility).ToList();
            CollectionAssert.AllItemsAreUnique(facilities);

            // 층도 서로 달라야 한다 — 한 층에 두 명이면 방이 하나뿐이라 한 명만 구출된다.
            var floors = ShelterNpcRoster.All.Select(npc => npc.ProgressIndex).ToList();
            CollectionAssert.AllItemsAreUnique(floors);
        }

    }
}
