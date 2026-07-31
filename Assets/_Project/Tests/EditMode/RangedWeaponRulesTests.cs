using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 원거리는 기본 능력이 아니라 무기(사거리)와 탄약(에너지 셀)이 함께 있을 때만 열린다.
    /// 이 계약이 깨지면 무료 원거리로 돌아가 카이팅이 언제나 최적해가 된다(M4의 이유).
    /// 판정과 소비가 갈라지지 않는 것도 여기서 고정한다 — 빗나간 사격은 탄을 먹지 않는다.
    /// </summary>
    public class RangedWeaponRulesTests
    {
        private static GridMap Corridor(int length)
        {
            var map = new GridMap();
            for (int x = 0; x < length; x++) map.Set(new GridPos(x, 0, 0), TileKind.Floor);
            return map;
        }

        private static CombatLoadout ArcCaster() =>
            EquipmentRules.LoadoutFor("arc-caster", "");

        private static Inventory WithCells(int charges)
        {
            var inventory = new Inventory();
            if (charges > 0) inventory.Add(ItemKind.EnergyCell, charges);
            return inventory;
        }

        [Test]
        public void Unarmed_HasNoRanged_AndDiagnosesNoWeapon()
        {
            Assert.IsFalse(CombatLoadout.Unarmed.HasRanged);
            Assert.AreEqual(
                RangedFireBlock.NoWeapon,
                RangedWeaponRules.Diagnose(CombatLoadout.Unarmed, WithCells(4)));
        }

        [Test]
        public void ArcCaster_WithoutCells_DiagnosesNoAmmo()
        {
            Assert.IsTrue(ArcCaster().HasRanged);
            Assert.AreEqual(
                RangedFireBlock.NoAmmo,
                RangedWeaponRules.Diagnose(ArcCaster(), WithCells(0)));
        }

        [Test]
        public void Fire_ConsumesExactlyOneCharge_PerHit()
        {
            GridMap map = Corridor(6);
            var attacker = new CombatantState("p", new GridPos(0, 0, 0), 10, 3);
            var target = new CombatantState("e", new GridPos(4, 0, 0), 20, 1);
            Inventory inventory = WithCells(2);

            Assert.IsTrue(RangedWeaponRules.TryFire(
                attacker, target, map, ArcCaster(), inventory,
                out int damage, out RangedFireBlock block, attackPower: 1));

            Assert.AreEqual(RangedFireBlock.None, block);
            Assert.Greater(damage, 0);
            Assert.AreEqual(1, inventory.Count(ItemKind.EnergyCell));
        }

        [Test]
        public void Fire_OutOfRange_KeepsAmmo_AndReportsNoShot()
        {
            // 아크 캐스터 사거리 5 — 6칸 밖은 닿지 않는다.
            GridMap map = Corridor(8);
            var attacker = new CombatantState("p", new GridPos(0, 0, 0), 10, 3);
            var target = new CombatantState("e", new GridPos(7, 0, 0), 20, 1);
            Inventory inventory = WithCells(3);

            Assert.IsFalse(RangedWeaponRules.TryFire(
                attacker, target, map, ArcCaster(), inventory,
                out int damage, out RangedFireBlock block, attackPower: 1));

            Assert.AreEqual(RangedFireBlock.NoShot, block);
            Assert.AreEqual(0, damage);
            Assert.AreEqual(3, inventory.Count(ItemKind.EnergyCell), "빗나간 사격이 탄을 먹었다");
        }

        [Test]
        public void Fire_WithoutWeapon_NeverTouchesAmmo()
        {
            GridMap map = Corridor(4);
            var attacker = new CombatantState("p", new GridPos(0, 0, 0), 10, 3);
            var target = new CombatantState("e", new GridPos(2, 0, 0), 20, 1);
            Inventory inventory = WithCells(4);

            Assert.IsFalse(RangedWeaponRules.TryFire(
                attacker, target, map, CombatLoadout.Unarmed, inventory,
                out _, out RangedFireBlock block));

            Assert.AreEqual(RangedFireBlock.NoWeapon, block);
            Assert.AreEqual(4, inventory.Count(ItemKind.EnergyCell));
        }

        [Test]
        public void Fire_DrainsToEmpty_ThenBlocksOnAmmo()
        {
            GridMap map = Corridor(6);
            var attacker = new CombatantState("p", new GridPos(0, 0, 0), 10, 3);
            var target = new CombatantState("e", new GridPos(3, 0, 0), 99, 1);
            Inventory inventory = WithCells(1);

            Assert.IsTrue(RangedWeaponRules.TryFire(
                attacker, target, map, ArcCaster(), inventory, out _, out _, attackPower: 1));
            Assert.AreEqual(0, inventory.Count(ItemKind.EnergyCell));

            Assert.IsFalse(RangedWeaponRules.TryFire(
                attacker, target, map, ArcCaster(), inventory,
                out _, out RangedFireBlock block, attackPower: 1));
            Assert.AreEqual(RangedFireBlock.NoAmmo, block);
        }

        [Test]
        public void ArcCaster_OpensRange_WithoutTouchingMeleeReach()
        {
            CombatLoadout loadout = ArcCaster();
            Assert.AreEqual(5, loadout.RangedRange);
            // 원거리 무기가 근접까지 늘리면 "붙을까 떨어질까"가 선택이 아니게 된다.
            Assert.AreEqual(1, loadout.MeleeReach);
            Assert.AreEqual(0, EquipmentRules.LoadoutFor("pipe-spear", "").RangedRange);
        }
    }
}
