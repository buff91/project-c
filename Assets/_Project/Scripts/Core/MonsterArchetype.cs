using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 몬스터 종류 하나의 스탯·행동 파라미터. (GDD §5.7, M5 다양화 대비)
    /// M5에서 새 몬스터 = 이 데이터 인스턴스 + 스프라이트 슬롯 추가로 끝나야 한다.
    /// </summary>
    public sealed class MonsterArchetype
    {
        public string Id { get; }
        public int MaxHp { get; }
        public int AttackPower { get; }

        /// <summary>이 거리(체비셰프) 안에서 서로 보이면 추격을 시작한다.</summary>
        public int AggroRange { get; }

        /// <summary>순찰 시 스폰 지점에서 벗어나지 않는 반경(체비셰프).</summary>
        public int PatrolRadius { get; }

        /// <summary>HP 비율이 이 값 미만이면 도주. 0이면 도주하지 않는다. (도주는 MonsterBrain.DecideFlee 에서 구현됨)</summary>
        public float FleeThreshold { get; }

        /// <summary>표시용 이름(정산/텔레메트리 문구의 SSOT). 미지정 시 코드 ID 로 폴백한다.</summary>
        public string DisplayName { get; }

        /// <summary>
        /// 원거리 공격 사거리. 판정은 플레이어와 같은 <see cref="CombatRules.RangedReachCost"/>
        /// (맨해튼 + 높이차)를 쓴다. 0이면 근접 전용.
        /// </summary>
        public int RangedRange { get; }

        /// <summary>원거리 공격력. 근접(<see cref="AttackPower"/>)과 따로 둬서 "붙으면 약한 사수"를 표현한다.</summary>
        public int RangedPower { get; }

        /// <summary>
        /// 플레이어가 이 거리(체비셰프) 이하로 붙으면 거리를 벌린다. 0이면 물러서지 않는다.
        /// 플레이어의 무피해 카이팅을 억제하는 반대 압력이자, 엄폐·돌진을 의미 있게 만드는 값.
        /// </summary>
        public int KeepAwayRange { get; }

        /// <summary>원거리 교전을 하는 몬스터인가.</summary>
        public bool IsRanged => RangedRange > 0 && RangedPower > 0;

        /// <summary>
        /// 사다리를 탈 수 있는가. <b>기본값은 false</b> — 새 아키타입을 늘릴 때 조용히
        /// 전부 오르게 되면 이 축이 죽는다. 오를 수 있는 쪽을 명시적으로 적는다.
        /// <para>
        /// 이 값이 사다리를 전술 자원으로 만든다: 높은 곳은 사다리로만 닿고,
        /// 못 오르는 적은 거기까지 따라오지 못한다. <b>실루엣과 일치</b>시켜야
        /// 플레이어가 배우지 않고도 읽는다 — 인간형은 오르고, 기계·무정형은 못 오른다.
        /// </para>
        /// </summary>
        public bool CanClimb { get; }

        public MonsterArchetype(
            string id,
            int maxHp,
            int attackPower,
            int aggroRange,
            int patrolRadius,
            float fleeThreshold = 0f,
            string displayName = null,
            int rangedRange = 0,
            int rangedPower = 0,
            int keepAwayRange = 0,
            bool canClimb = false)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("몬스터 ID가 필요합니다.", nameof(id));
            if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (attackPower <= 0) throw new ArgumentOutOfRangeException(nameof(attackPower));
            if (aggroRange < 1) throw new ArgumentOutOfRangeException(nameof(aggroRange));
            if (patrolRadius < 0) throw new ArgumentOutOfRangeException(nameof(patrolRadius));
            if (fleeThreshold < 0f || fleeThreshold > 1f) throw new ArgumentOutOfRangeException(nameof(fleeThreshold));
            if (rangedRange < 0) throw new ArgumentOutOfRangeException(nameof(rangedRange));
            if (rangedPower < 0) throw new ArgumentOutOfRangeException(nameof(rangedPower));
            if (keepAwayRange < 0) throw new ArgumentOutOfRangeException(nameof(keepAwayRange));

            Id = id;
            MaxHp = maxHp;
            AttackPower = attackPower;
            AggroRange = aggroRange;
            PatrolRadius = patrolRadius;
            FleeThreshold = fleeThreshold;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            RangedRange = rangedRange;
            RangedPower = rangedPower;
            KeepAwayRange = keepAwayRange;
            CanClimb = canClimb;
        }
    }
}
