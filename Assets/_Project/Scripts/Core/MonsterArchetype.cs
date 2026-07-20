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

        /// <summary>HP 비율이 이 값 미만이면 도주. 0이면 도주하지 않는다. (도주 상태는 M5에서 구현)</summary>
        public float FleeThreshold { get; }

        public MonsterArchetype(
            string id,
            int maxHp,
            int attackPower,
            int aggroRange,
            int patrolRadius,
            float fleeThreshold = 0f)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("몬스터 ID가 필요합니다.", nameof(id));
            if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp));
            if (attackPower <= 0) throw new ArgumentOutOfRangeException(nameof(attackPower));
            if (aggroRange < 1) throw new ArgumentOutOfRangeException(nameof(aggroRange));
            if (patrolRadius < 0) throw new ArgumentOutOfRangeException(nameof(patrolRadius));
            if (fleeThreshold < 0f || fleeThreshold > 1f) throw new ArgumentOutOfRangeException(nameof(fleeThreshold));

            Id = id;
            MaxHp = maxHp;
            AttackPower = attackPower;
            AggroRange = aggroRange;
            PatrolRadius = patrolRadius;
            FleeThreshold = fleeThreshold;
        }
    }
}
