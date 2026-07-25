using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 기록 — 죽음이 먹이는 축. 두 가지를 동시에 지켜야 해서 테스트가 양방향이다:
    /// ① 실패한 판도 <b>반드시</b> 전진한다(0을 주면 원래 문제로 돌아간다),
    /// ② 그런데 반복 파밍은 이득이 아니어야 한다(도달 층에만 비례하면 1~3층 왕복이 최적이 된다).
    /// </summary>
    public class RunRecordRulesTests
    {
        [Test]
        public void EveryRun_LeavesSomething_EvenTheWorstOne()
        {
            // 첫 층에서 아무것도 못 하고 죽은 판. 이것도 0이면 "실패한 판도 전진"이 거짓말이 된다.
            int worst = RunRecordRules.Award(
                reachedFloors: RunRecordRules.ReachedFloors(0),
                bestFloorsEver: 99,
                secretRoomsFound: 0);

            Assert.Greater(worst, 0, "가장 나쁜 판도 기록을 남겨야 한다");
        }

        [Test]
        public void FirstFloorCounts_ProgressIndexIsZeroBased()
        {
            // 진행 지수 0 = 첫 층 도달이다. +1 을 빠뜨리면 첫 층에서 죽은 판이 0이 된다.
            Assert.AreEqual(1, RunRecordRules.ReachedFloors(0));
            Assert.AreEqual(4, RunRecordRules.ReachedFloors(3));
            Assert.AreEqual(1, RunRecordRules.ReachedFloors(-5), "음수는 첫 층으로 접는다");
        }

        [Test]
        public void PushingFurther_BeatsRepeatingTheSameFloors()
        {
            // 이 게임에서 반복 파밍이 이득이 되면 진행이 멈춘다 — 전진이 확실히 더 나아야 한다.
            int repeat = RunRecordRules.Award(reachedFloors: 5, bestFloorsEver: 5, secretRoomsFound: 0);
            int push = RunRecordRules.Award(reachedFloors: 6, bestFloorsEver: 5, secretRoomsFound: 0);

            Assert.Greater(push, repeat, "한 층 더 간 판이 같은 층 반복보다 나아야 한다");
            Assert.Greater(
                push - repeat, RunRecordRules.RepeatRatePerFloor,
                "개척 한 층이 반복 한 층보다 확실히 커야 의미가 있다");
        }

        [Test]
        public void RepeatingIsWorthLess_ThanTheFirstTime()
        {
            int first = RunRecordRules.Award(reachedFloors: 3, bestFloorsEver: 0, secretRoomsFound: 0);
            int again = RunRecordRules.Award(reachedFloors: 3, bestFloorsEver: 3, secretRoomsFound: 0);

            Assert.Greater(first, again, "처음 밟은 층이 다시 밟은 층보다 커야 한다");
            Assert.Greater(again, 0, "그래도 0은 아니다");
        }

        [Test]
        public void FrontierOnlyCountsTheNewFloors()
        {
            // 5층까지 가 본 상태에서 7층까지 갔다 = 새 층은 둘뿐이다.
            int award = RunRecordRules.Award(reachedFloors: 7, bestFloorsEver: 5, secretRoomsFound: 0);
            int expected = 5 * RunRecordRules.RepeatRatePerFloor +
                           2 * RunRecordRules.FrontierRatePerFloor;

            Assert.AreEqual(expected, award);
        }

        [Test]
        public void SecretRooms_AddOnTop()
        {
            int without = RunRecordRules.Award(3, 3, 0);
            int with = RunRecordRules.Award(3, 3, 2);

            Assert.AreEqual(without + 2 * RunRecordRules.SecretRoomRate, with);
        }

        // ── 해금 판정: 부분 진행이 살아난다 ────────────────────────────────

        [Test]
        public void PartialProgress_IsNoLongerThrownAway()
        {
            // 예전 규칙이 버린 바로 그 판: 목표 12에 11까지 가고 죽었다.
            Assert.IsFalse(RunRecordRules.IsConditionMet(11, 0, 12), "아직은 못 연다");
            // 그런데 그 11이 남아 있어서 기록 하나로 메워진다.
            Assert.IsTrue(RunRecordRules.IsConditionMet(11, 1, 12), "기록 하나로 열려야 한다");
        }

        [Test]
        public void OneGoodRun_StillOpensItAlone()
        {
            // 한 판에 몰아치는 도전은 그대로 남는다 — 기록 없이도 열려야 빠른 길이 유효하다.
            Assert.IsTrue(RunRecordRules.IsConditionMet(12, 0, 12));
        }

        [Test]
        public void RecordsAlone_CanOpenIt()
        {
            // 플레이로 한 번도 못 채워도 느린 길로 도달할 수 있어야 벽에 막히지 않는다.
            Assert.IsTrue(RunRecordRules.IsConditionMet(0, 12, 12));
        }

        [Test]
        public void NegativesAreFolded_NotTrusted()
        {
            Assert.IsFalse(RunRecordRules.IsConditionMet(-100, 0, 1));
            Assert.IsFalse(RunRecordRules.IsConditionMet(0, -100, 1));
        }

        // ── 저장 연동 ─────────────────────────────────────────────────────

        [Test]
        public void AwardRecords_RaisesTheFrontierAfterPaying()
        {
            var meta = new MetaSaveData();

            int first = meta.AwardRecords(reachedFloors: 3, secretRoomsFound: 0);
            Assert.AreEqual(3, meta.deepestFloorsEver, "기준선이 올라가야 한다");
            Assert.AreEqual(first, meta.records);

            // 같은 층을 다시 갔다 — 이번엔 개척 보너스가 없다.
            int second = meta.AwardRecords(reachedFloors: 3, secretRoomsFound: 0);
            Assert.Less(second, first, "기준선을 올린 뒤엔 같은 판이 덜 준다");
            Assert.AreEqual(3, meta.deepestFloorsEver, "기준선은 내려가지 않는다");
            Assert.AreEqual(first + second, meta.records);
        }

        [Test]
        public void AwardRecords_PaysTheFrontierBeforeRaisingIt()
        {
            // 순서가 뒤집히면 개척 보너스가 통째로 사라진다 — 첫 판이 가장 심하게 손해본다.
            var meta = new MetaSaveData();
            int gained = meta.AwardRecords(reachedFloors: 4, secretRoomsFound: 0);

            Assert.AreEqual(
                RunRecordRules.Award(4, 0, 0), gained,
                "적립은 갱신 전 기준선으로 계산해야 한다");
        }

        [Test]
        public void InvestRecords_SpendsOnlyWhatYouHave()
        {
            var meta = new MetaSaveData { records = 5 };

            Assert.AreEqual(5, meta.InvestRecords(ItemKind.FrostBomb, 20),
                "보유량을 넘는 요청은 가진 만큼만 넣는다");
            Assert.AreEqual(0, meta.records);
            Assert.AreEqual(5, meta.InvestedRecords(ItemKind.FrostBomb));

            Assert.AreEqual(0, meta.InvestRecords(ItemKind.FrostBomb, 1), "없으면 아무것도 안 넣는다");
            Assert.AreEqual(5, meta.InvestedRecords(ItemKind.FrostBomb));
        }

        [Test]
        public void InvestRecords_AccumulatesPerCondition()
        {
            var meta = new MetaSaveData { records = 10 };
            meta.InvestRecords(ItemKind.FrostBomb, 3);
            meta.InvestRecords(ItemKind.FrostBomb, 4);
            meta.InvestRecords(ItemKind.ThrowingKnife, 2);

            Assert.AreEqual(7, meta.InvestedRecords(ItemKind.FrostBomb));
            Assert.AreEqual(2, meta.InvestedRecords(ItemKind.ThrowingKnife));
            Assert.AreEqual(1, meta.records);
        }

        // ── 기록실 투입 ───────────────────────────────────────────────────

        [Test]
        public void Investing_OpensTheToolTheMomentItIsEnough()
        {
            ItemUnlockCondition c = ItemUnlockRules.Conditions[0];
            var meta = new MetaSaveData { records = 100 };
            meta.RecordUnlockProgress(c.Kind, c.Target - 2);

            Assert.AreEqual(2, ItemUnlockRules.RemainingFor(meta, c));
            Assert.AreEqual(2, ItemUnlockRules.InvestRecords(meta, c, 2));
            Assert.IsTrue(meta.IsItemUnlocked(c.Kind), "충족되면 그 자리에서 열려야 한다");
            Assert.AreEqual(98, meta.records);
        }

        [Test]
        public void Investing_NeverOverpays()
        {
            // 남은 만큼만 받는다 — 넘치게 부으면 기록을 버리는 셈이다.
            ItemUnlockCondition c = ItemUnlockRules.Conditions[0];
            var meta = new MetaSaveData { records = 100 };
            meta.RecordUnlockProgress(c.Kind, c.Target - 3);

            Assert.AreEqual(3, ItemUnlockRules.InvestRecords(meta, c, 50), "남은 3만 받는다");
            Assert.AreEqual(97, meta.records);
        }

        [Test]
        public void Investing_PartiallyWhenShort_KeepsTheProgress()
        {
            // 부족해도 실패시키지 않는다 — "조금씩 메운다"가 이 축의 전부다.
            ItemUnlockCondition c = ItemUnlockRules.Conditions[0];
            var meta = new MetaSaveData { records = 1 };

            Assert.AreEqual(1, ItemUnlockRules.InvestRecords(meta, c, c.Target));
            Assert.IsFalse(meta.IsItemUnlocked(c.Kind));
            Assert.AreEqual(1, meta.InvestedRecords(c.Kind), "넣은 만큼은 남는다");
            Assert.AreEqual(c.Target - 1, ItemUnlockRules.RemainingFor(meta, c));
        }

        [Test]
        public void AlreadyUnlocked_TakesNothing()
        {
            ItemUnlockCondition c = ItemUnlockRules.Conditions[0];
            var meta = new MetaSaveData { records = 100 };
            meta.UnlockItem(c.Kind);

            Assert.AreEqual(0, ItemUnlockRules.RemainingFor(meta, c));
            Assert.AreEqual(0, ItemUnlockRules.InvestRecords(meta, c, 10), "열린 것에 더 붓지 않는다");
            Assert.AreEqual(100, meta.records);
        }

        [Test]
        public void CodexAndRunEnd_AgreeOnWhatIsOpen()
        {
            // 두 경로가 다른 규칙을 쓰면 "기록실에선 열리는데 판 끝나면 안 열린다"가 된다.
            ItemUnlockCondition c = ItemUnlockRules.Conditions[0];
            var meta = new MetaSaveData { records = 100 };
            meta.RecordUnlockProgress(c.Kind, c.Target - 1);
            meta.InvestRecords(c.Kind, 1);

            Assert.IsTrue(
                RunRecordRules.IsConditionMet(
                    meta.BestUnlockProgress(c.Kind), meta.InvestedRecords(c.Kind), c.Target));
            CollectionAssert.Contains(
                ItemUnlockRules.EvaluateUnlocks(
                    meta.UnlockedItemKinds(), meta.BestUnlockProgress, meta.InvestedRecords),
                c);
        }

        [Test]
        public void FreshSave_HasNothingInvested()
        {
            var meta = new MetaSaveData();
            foreach (ItemUnlockCondition condition in ItemUnlockRules.Conditions)
                Assert.AreEqual(0, meta.InvestedRecords(condition.Kind), condition.Kind.ToString());
            Assert.AreEqual(0, meta.records);
            Assert.AreEqual(0, meta.deepestFloorsEver);
        }
    }
}
