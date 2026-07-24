using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>경량 행동 트리 프리미티브(Selector/Condition/Leaf) 계약 검증.</summary>
    public class BehaviorTreeTests
    {
        [Test]
        public void Selector_ReturnsFirstDecidingChild_SkippingDecliners()
        {
            var tree = new Selector<int, int>(
                new Leaf<int, int>(_ => (int?)null), // 결정 안 함
                new Leaf<int, int>(c => c + 1),      // 결정
                new Leaf<int, int>(_ => 999));       // 도달하지 않아야

            Assert.AreEqual(6, tree.Tick(5).Value);
        }

        [Test]
        public void Selector_ReturnsNull_WhenAllChildrenDecline()
        {
            var tree = new Selector<int, int>(
                new Leaf<int, int>(_ => (int?)null),
                new Leaf<int, int>(_ => (int?)null));

            Assert.IsFalse(tree.Tick(0).HasValue);
        }

        [Test]
        public void Condition_GatesChild_AndSkipsEvaluationWhenFalse()
        {
            bool evaluated = false;
            var gated = new Condition<int, int>(
                c => c > 10,
                new Leaf<int, int>(c => { evaluated = true; return c; }));

            Assert.IsFalse(gated.Tick(5).HasValue, "조건 거짓이면 결정 없음");
            Assert.IsFalse(evaluated, "조건 거짓이면 자식을 평가하지 않는다(가지치기)");

            Assert.AreEqual(20, gated.Tick(20).Value);
            Assert.IsTrue(evaluated);
        }
    }
}
