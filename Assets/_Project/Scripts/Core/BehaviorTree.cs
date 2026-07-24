using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 아주 가벼운 행동 트리(Behavior Tree) 노드. 이 게임의 결정은 즉시형이라 Running 상태가 없다:
    /// Tick은 "결정된 행동"(non-null) 또는 null(이 가지는 결정하지 않음 → 다음 형제로)을 돌려준다.
    /// Selector/Condition/Leaf로 조합해, 콘텐츠가 늘어도 가지를 선언적으로 추가·재배치한다.
    /// (AI 아키텍처: 손으로 쓴 FSM → BT. GDD §5.7)
    /// </summary>
    public abstract class BehaviorNode<TContext, TAction> where TAction : struct
    {
        public abstract TAction? Tick(TContext context);
    }

    /// <summary>자식을 순서대로 시도해 처음으로 행동을 낸 자식의 결과를 채택한다(우선순위 선택).</summary>
    public sealed class Selector<TContext, TAction> : BehaviorNode<TContext, TAction>
        where TAction : struct
    {
        private readonly BehaviorNode<TContext, TAction>[] _children;

        public Selector(params BehaviorNode<TContext, TAction>[] children) =>
            _children = children ?? Array.Empty<BehaviorNode<TContext, TAction>>();

        public override TAction? Tick(TContext context)
        {
            foreach (BehaviorNode<TContext, TAction> child in _children)
            {
                TAction? result = child.Tick(context);
                if (result.HasValue) return result;
            }
            return null;
        }
    }

    /// <summary>조건이 참일 때만 자식을 평가하는 가드(데코레이터). 거짓이면 결정 없음(null).</summary>
    public sealed class Condition<TContext, TAction> : BehaviorNode<TContext, TAction>
        where TAction : struct
    {
        private readonly Func<TContext, bool> _predicate;
        private readonly BehaviorNode<TContext, TAction> _child;

        public Condition(Func<TContext, bool> predicate, BehaviorNode<TContext, TAction> child)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _child = child ?? throw new ArgumentNullException(nameof(child));
        }

        public override TAction? Tick(TContext context) =>
            _predicate(context) ? _child.Tick(context) : (TAction?)null;
    }

    /// <summary>
    /// 잎(leaf) 행동. 행동을 내거나(결정) null(결정 없음 → 형제로)을 돌려준다.
    /// 순수 부수효과 노드는 부수효과를 수행한 뒤 null을 반환하면 된다.
    /// </summary>
    public sealed class Leaf<TContext, TAction> : BehaviorNode<TContext, TAction>
        where TAction : struct
    {
        private readonly Func<TContext, TAction?> _behavior;

        public Leaf(Func<TContext, TAction?> behavior) =>
            _behavior = behavior ?? throw new ArgumentNullException(nameof(behavior));

        public override TAction? Tick(TContext context) => _behavior(context);
    }
}
