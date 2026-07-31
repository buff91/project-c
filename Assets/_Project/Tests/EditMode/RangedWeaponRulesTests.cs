using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 원거리는 <b>기본으로 쥐어 주되 연사할 수 없다</b>. 무제한이면 카이팅이 항상 정답이 되고,
    /// 탄약 아이템만으로 막으면 다 쓴 판은 그 축이 통째로 사라져 저울질 자체가 불가능해진다.
    /// 그 사이를 잡는 것이 충전·재충전이며, 판정과 소비가 갈라지지 않는 것도 여기서 고정한다.
    /// </summary>
    public class RangedWeaponRulesTests
    {
        private static GridMap Corridor(int length)
        {
            var map = new GridMap();
            for (int x = 0; x < length; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            return map;
        }

        private static CombatLoadout Emitter() => CombatLoadout.Unarmed;
        private static CombatLoadout ArcCaster() => EquipmentRules.LoadoutFor("arc-caster", "");

        [Test]
        public void EveryLoadout_KeepsRanged_SoThePlayerAlwaysSeesTheAxis()
        {
            Assert.IsTrue(Emitter().HasRanged, "내장 이미터가 없으면 초반에 원거리를 볼 수 없다");
            Assert.IsTrue(ArcCaster().HasRanged);
            // 근접 무기를 골라도 원거리 축은 남는다(기본형으로 내려갈 뿐).
            CombatLoadout lance = EquipmentRules.LoadoutFor("pipe-spear", "");
            Assert.IsTrue(lance.HasRanged);
            Assert.AreEqual(RangedWeaponRules.Baseline.Range, lance.RangedRange);
        }

        [Test]
        public void ArcCaster_DeepensTheAxis_WithoutTouchingMeleeReach()
        {
            CombatLoadout caster = ArcCaster();
            Assert.Greater(caster.RangedRange, Emitter().RangedRange);
            Assert.Greater(caster.RangedCapacity, Emitter().RangedCapacity);
            Assert.Less(caster.RangedRechargeTurns, Emitter().RangedRechargeTurns);
            // 원거리 무기가 근접까지 늘리면 "붙을까 떨어질까"가 선택이 아니게 된다.
            Assert.AreEqual(1, caster.MeleeReach);
        }

        [Test]
        public void EmptyCharges_DiagnoseNoCharge()
        {
            var charges = new RangedChargeState(0);
            Assert.AreEqual(
                RangedFireBlock.NoCharge,
                RangedWeaponRules.Diagnose(Emitter(), charges));
        }

        [Test]
        public void Fire_SpendsExactlyOneCharge_PerHit()
        {
            GridMap map = Corridor(4);
            var attacker = new CombatantState("p", new GridPos(0, 0, 0), 10, 3);
            var target = new CombatantState("e", new GridPos(2, 0, 0), 20, 1);
            CombatLoadout loadout = Emitter();
            RangedChargeState charges = RangedChargeState.Full(loadout);
            int before = charges.charges;

            Assert.IsTrue(RangedWeaponRules.TryFire(
                attacker, target, map, loadout, charges,
                out int damage, out RangedFireBlock block, attackPower: 1));

            Assert.AreEqual(RangedFireBlock.None, block);
            Assert.Greater(damage, 0);
            Assert.AreEqual(before - 1, charges.charges);
        }

        [Test]
        public void Fire_OutOfRange_KeepsCharges_AndReportsNoShot()
        {
            // 내장 이미터 사거리 3 — 그 밖은 닿지 않는다.
            GridMap map = Corridor(9);
            var attacker = new CombatantState("p", new GridPos(0, 0, 0), 10, 3);
            var target = new CombatantState("e", new GridPos(8, 0, 0), 20, 1);
            CombatLoadout loadout = Emitter();
            RangedChargeState charges = RangedChargeState.Full(loadout);

            Assert.IsFalse(RangedWeaponRules.TryFire(
                attacker, target, map, loadout, charges,
                out int damage, out RangedFireBlock block, attackPower: 1));

            Assert.AreEqual(RangedFireBlock.NoShot, block);
            Assert.AreEqual(0, damage);
            Assert.AreEqual(loadout.RangedCapacity, charges.charges, "빗나간 사격이 충전을 먹었다");
        }

        [Test]
        public void Charges_RefillOverTurns_ButNeverPastCapacity()
        {
            CombatLoadout loadout = Emitter();
            var charges = new RangedChargeState(0);

            Assert.IsFalse(charges.Tick(loadout, RangedWeaponRules.Baseline.RechargeTurns - 1));
            Assert.AreEqual(0, charges.charges);

            Assert.IsTrue(charges.Tick(loadout));
            Assert.AreEqual(1, charges.charges);

            charges.Tick(loadout, RangedWeaponRules.Baseline.RechargeTurns * 10);
            Assert.AreEqual(loadout.RangedCapacity, charges.charges);
        }

        [Test]
        public void FullCharges_DoNotBankTurns_ForAFreeInstantShot()
        {
            CombatLoadout loadout = Emitter();
            RangedChargeState charges = RangedChargeState.Full(loadout);

            // 만충인 채로 오래 서 있어도 회복 카운터가 쌓이면 안 된다.
            charges.Tick(loadout, RangedWeaponRules.Baseline.RechargeTurns * 5);
            charges.charges--; // 한 발 쏜 셈

            Assert.IsFalse(
                charges.Tick(loadout),
                "만충 중 쌓인 턴이 사격 직후 공짜 재충전으로 터졌다");
        }

        [Test]
        public void Cell_RefillsToFull_ButIsRefusedWhenAlreadyFull()
        {
            CombatLoadout loadout = ArcCaster();
            var charges = new RangedChargeState(1);

            Assert.IsTrue(charges.TryRefill(loadout));
            Assert.AreEqual(loadout.RangedCapacity, charges.charges);
            // 가득이면 셀을 낭비하지 않는다.
            Assert.IsFalse(charges.TryRefill(loadout));
        }

        [Test]
        public void SwappingToASmallerWeapon_ClampsOverflowCharges()
        {
            RangedChargeState charges = RangedChargeState.Full(ArcCaster());
            charges.charges--;
            charges.turnsSinceGain = 3;
            CombatLoadout emitter = Emitter();

            charges.ClampTo(emitter);

            Assert.AreEqual(emitter.RangedCapacity, charges.charges);
            Assert.AreEqual(
                0,
                charges.turnsSinceGain,
                "용량 축소로 만충이 됐는데 이전 무기의 회복 턴을 비축했다");
        }

        [Test]
        public void Snapshot_IsIndependentFromLiveState()
        {
            var live = new RangedChargeState(1) { turnsSinceGain = 3 };

            RangedChargeState snapshot = live.Snapshot();
            live.Tick(Emitter(), 3);

            Assert.AreEqual(1, snapshot.charges);
            Assert.AreEqual(3, snapshot.turnsSinceGain);
            Assert.AreNotSame(live, snapshot);
        }

        [Test]
        public void Restore_LegacyNullStartsFull()
        {
            CombatLoadout loadout = ArcCaster();

            RangedChargeState restored = RangedChargeState.Restore(null, loadout);

            Assert.AreEqual(loadout.RangedCapacity, restored.charges);
            Assert.AreEqual(0, restored.turnsSinceGain);
        }

        [Test]
        public void Restore_ClampsACopyWithoutMutatingSavedState()
        {
            var saved = new RangedChargeState(3) { turnsSinceGain = 2 };

            RangedChargeState restored = RangedChargeState.Restore(saved, Emitter());

            Assert.AreEqual(2, restored.charges);
            Assert.AreEqual(0, restored.turnsSinceGain);
            Assert.AreEqual(3, saved.charges);
            Assert.AreEqual(2, saved.turnsSinceGain);
            Assert.AreNotSame(saved, restored);
        }
    }
}
