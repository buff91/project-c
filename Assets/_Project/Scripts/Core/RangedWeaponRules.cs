using System;

namespace ProjectC.Core
{
    /// <summary>원거리 사격이 불가능한 이유. 장비 → 충전 → 사선 순으로 진단한다.</summary>
    public enum RangedFireBlock
    {
        None = 0,
        /// <summary>원거리를 주는 무기가 없다(내장 이미터가 있으므로 정상 플레이에선 안 나온다).</summary>
        NoWeapon = 1,
        /// <summary>충전이 비었다 — 기다리거나 에너지 셀로 채운다.</summary>
        NoCharge = 2,
        /// <summary>사거리 밖이거나 사선이 막혔다 — 상세는 <see cref="CombatRules.DiagnoseRanged"/>.</summary>
        NoShot = 3
    }

    /// <summary>
    /// 원거리 충전 상태. 쏘면 줄고 턴이 지나면 스스로 찬다 — <b>자원 절벽이 아니라 리듬</b>이다.
    /// 탄약 아이템만으로 막으면 다 쓴 판은 원거리가 통째로 사라져 그 축을 저울질할 수 없고,
    /// 무제한이면 카이팅이 항상 정답이 된다. 그 사이를 재충전 속도로 잡는다(SPD 완드 계보).
    /// </summary>
    [Serializable]
    public sealed class RangedChargeState
    {
        /// <summary>남은 충전(= 지금 쏠 수 있는 횟수).</summary>
        public int charges;

        /// <summary>마지막 회복 이후 지난 턴.</summary>
        public int turnsSinceGain;

        public RangedChargeState() { }

        public RangedChargeState(int charges)
        {
            this.charges = charges < 0 ? 0 : charges;
        }

        /// <summary>만충으로 시작한다 — 출발선에서 원거리를 한 번은 써 보게 한다.</summary>
        public static RangedChargeState Full(CombatLoadout loadout) =>
            new RangedChargeState(loadout.RangedCapacity);

        /// <summary>
        /// 체크포인트·던전 전환용 값 복사. 런타임 상태와 저장 스냅샷이 같은 인스턴스를
        /// 공유하면 저장 뒤 진행한 턴이 과거 체크포인트까지 바꾸므로 반드시 복제한다.
        /// </summary>
        public RangedChargeState Snapshot() =>
            new RangedChargeState(charges) { turnsSinceGain = turnsSinceGain };

        /// <summary>
        /// 저장 상태를 현재 장비 규격으로 복원한다. 이 필드가 없던 구세이브(null)는
        /// 만충으로 시작하며, 저장 객체 자체는 수정하지 않는다.
        /// </summary>
        public static RangedChargeState Restore(
            RangedChargeState saved,
            CombatLoadout loadout)
        {
            RangedChargeState restored = saved?.Snapshot() ?? Full(loadout);
            restored.ClampTo(loadout);
            return restored;
        }

        public bool IsFull(CombatLoadout loadout) => charges >= loadout.RangedCapacity;

        /// <summary>
        /// 턴 경과. 만충이면 카운터를 재우고, 아니면 <see cref="CombatLoadout.RangedRechargeTurns"/>
        /// 마다 1칸 회복한다. 회복했으면 true.
        /// </summary>
        public bool Tick(CombatLoadout loadout, int turns = 1)
        {
            if (turns <= 0) return false;
            if (IsFull(loadout))
            {
                // 만충 상태에서 턴을 쌓아 두면 다음 사격 직후 즉시 재충전되는 공짜 한 발이 생긴다.
                turnsSinceGain = 0;
                return false;
            }

            turnsSinceGain += turns;
            bool gained = false;
            while (turnsSinceGain >= loadout.RangedRechargeTurns && !IsFull(loadout))
            {
                turnsSinceGain -= loadout.RangedRechargeTurns;
                charges++;
                gained = true;
            }
            if (IsFull(loadout)) turnsSinceGain = 0;
            return gained;
        }

        /// <summary>장비 교체 후 정합 — 용량이 줄면 넘치는 충전을 깎는다.</summary>
        public void ClampTo(CombatLoadout loadout)
        {
            if (charges > loadout.RangedCapacity) charges = loadout.RangedCapacity;
            if (charges < 0) charges = 0;
            if (turnsSinceGain < 0 || IsFull(loadout)) turnsSinceGain = 0;
        }

        /// <summary>에너지 셀 등으로 즉시 만충. 이미 가득이면 false(셀을 낭비하지 않는다).</summary>
        public bool TryRefill(CombatLoadout loadout)
        {
            if (IsFull(loadout)) return false;
            charges = loadout.RangedCapacity;
            turnsSinceGain = 0;
            return true;
        }
    }

    /// <summary>
    /// 플레이어 원거리 사격의 단일 관문. 원거리는 <b>기본으로 쥐어 주되 연사할 수 없다</b> —
    /// 내장 이미터가 사거리 3·충전 2를 주고, 아크 캐스터가 그 축을 사거리 5·충전 4로 넓힌다.
    ///
    /// <para>
    /// <b>판정과 소비는 한 함수 안에서 원자적으로 일어난다.</b> 게임플레이 쪽에서 "쏠 수
    /// 있나?"와 "충전을 깎는다"를 따로 호출하면, 그 사이에 낀 연출·이동 때문에 맞지도 않은
    /// 사격이 충전을 먹거나 그 반대가 된다. 실패하면 충전은 그대로다.
    /// </para>
    /// </summary>
    public static class RangedWeaponRules
    {
        /// <summary>
        /// 내장 이미터 — 장비가 없어도 쓰는 기본 원거리. 값은 실플레이 전 임시다
        /// (조정 축: 판당 사격 횟수. 재충전이 빠르면 옛 무료 원거리로, 느리면 죽은 축으로 간다).
        /// </summary>
        public static class Baseline
        {
            public const int Range = 3;
            public const int Capacity = 2;
            public const int RechargeTurns = 6;
        }

        /// <summary>이 조합·충전으로 사격을 시도할 수 있는가(사선은 보지 않는다).</summary>
        public static bool CanFire(CombatLoadout loadout, RangedChargeState charges) =>
            Diagnose(loadout, charges) == RangedFireBlock.None;

        /// <summary>장비 → 충전 순으로 막힌 첫 이유. 사선은 호출부가 별도로 진단한다.</summary>
        public static RangedFireBlock Diagnose(CombatLoadout loadout, RangedChargeState charges)
        {
            if (!loadout.HasRanged) return RangedFireBlock.NoWeapon;
            if (charges == null || charges.charges < 1) return RangedFireBlock.NoCharge;
            return RangedFireBlock.None;
        }

        /// <summary>
        /// 사격 한 발. 장비·충전·사선이 모두 충족될 때만 피해가 들어가고 그때만 충전이 준다.
        /// <paramref name="attackPower"/>는 원거리 전용 피해(생략 시 근접과 같다).
        /// </summary>
        public static bool TryFire(
            CombatantState attacker,
            CombatantState target,
            GridMap map,
            CombatLoadout loadout,
            RangedChargeState charges,
            out int damage,
            out RangedFireBlock block,
            int? attackPower = null,
            int targetArmor = 0)
        {
            damage = 0;
            block = Diagnose(loadout, charges);
            if (block != RangedFireBlock.None) return false;

            // 먼저 맞는지 본다 — 빗나간 사격이 충전을 먹지 않게.
            if (!CombatRules.TryRanged(
                    attacker, target, map, loadout.RangedRange,
                    out damage, attackPower, targetArmor))
            {
                block = RangedFireBlock.NoShot;
                return false;
            }

            charges.charges--;
            if (charges.charges < 0)
                throw new InvalidOperationException("충전 소비가 음수가 됐다 — Diagnose 와 상태가 어긋났다.");
            return true;
        }
    }
}
