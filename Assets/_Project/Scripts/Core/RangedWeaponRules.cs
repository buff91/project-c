using System;

namespace ProjectC.Core
{
    /// <summary>원거리 사격이 불가능한 이유. 장비 → 탄약 → 사선 순으로 진단한다.</summary>
    public enum RangedFireBlock
    {
        None = 0,
        /// <summary>원거리 무기를 끼지 않았다(<see cref="CombatLoadout.HasRanged"/>).</summary>
        NoWeapon = 1,
        /// <summary>에너지 셀이 없다.</summary>
        NoAmmo = 2,
        /// <summary>사거리 밖이거나 사선이 막혔다 — 상세는 <see cref="CombatRules.DiagnoseRanged"/>.</summary>
        NoShot = 3
    }

    /// <summary>
    /// 플레이어 원거리 사격의 단일 관문. **원거리는 기본 능력이 아니다** — 무기(사거리)와
    /// 탄약(에너지 셀)이 함께 있어야 열린다. 무료 원거리였을 때는 파밍·장비 선택이 전투에
    /// 아무 영향을 주지 않았고, 카이팅이 언제나 최적이라 접근전이 성립하지 않았다.
    ///
    /// <para>
    /// <b>판정과 소비는 한 함수 안에서 원자적으로 일어난다.</b> 게임플레이 쪽에서 "쏠 수
    /// 있나?"와 "셀을 깎는다"를 따로 호출하면, 그 사이에 낀 연출·이동 때문에 맞지도 않은
    /// 사격이 탄을 먹거나 그 반대가 된다. 실패하면 인벤토리는 손대지 않는다.
    /// </para>
    /// </summary>
    public static class RangedWeaponRules
    {
        /// <summary>사격 1회가 소비하는 에너지 셀 충전.</summary>
        public const int ChargesPerShot = 1;

        /// <summary>이 조합·소지품으로 사격을 시도할 수 있는가(사선은 보지 않는다).</summary>
        public static bool CanFire(CombatLoadout loadout, Inventory inventory) =>
            Diagnose(loadout, inventory) == RangedFireBlock.None;

        /// <summary>장비 → 탄약 순으로 막힌 첫 이유. 사선은 호출부가 별도로 진단한다.</summary>
        public static RangedFireBlock Diagnose(CombatLoadout loadout, Inventory inventory)
        {
            if (!loadout.HasRanged) return RangedFireBlock.NoWeapon;
            if (inventory == null || inventory.Count(ItemKind.EnergyCell) < ChargesPerShot)
                return RangedFireBlock.NoAmmo;
            return RangedFireBlock.None;
        }

        /// <summary>
        /// 사격 한 발. 장비·탄약·사선이 모두 충족될 때만 피해가 들어가고 그때만 셀이 준다.
        /// <paramref name="attackPower"/>는 원거리 전용 피해(생략 시 근접과 같다).
        /// </summary>
        public static bool TryFire(
            CombatantState attacker,
            CombatantState target,
            GridMap map,
            CombatLoadout loadout,
            Inventory inventory,
            out int damage,
            out RangedFireBlock block,
            int? attackPower = null,
            int targetArmor = 0)
        {
            damage = 0;
            block = Diagnose(loadout, inventory);
            if (block != RangedFireBlock.None) return false;

            // 먼저 맞는지 본다 — 빗나간 사격이 탄을 먹지 않게.
            if (!CombatRules.TryRanged(
                    attacker, target, map, loadout.RangedRange,
                    out damage, attackPower, targetArmor))
            {
                block = RangedFireBlock.NoShot;
                return false;
            }

            // 명중이 확정된 뒤에만 소비한다. 여기서 실패하면 위 Diagnose 와 어긋난 것이므로
            // 조용히 넘기지 않는다 — 탄 없이 쏘는 상태가 계속 굴러가는 편이 더 나쁘다.
            if (!inventory.TryUse(ItemKind.EnergyCell))
                throw new InvalidOperationException(
                    "에너지 셀 소비가 실패했다 — Diagnose 와 인벤토리 상태가 어긋났다.");

            return true;
        }
    }
}
