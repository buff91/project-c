using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class StatusEffectsTests
    {
        [Test]
        public void Apply_And_Tick_CountDownAndExpire()
        {
            var statuses = new StatusEffects();
            statuses.Apply(StatusKind.Burn, 2);

            StatusTick first = statuses.Tick();
            Assert.AreEqual(StatusEffects.BurnDamagePerTurn, first.BurnDamage);
            Assert.IsFalse(first.Frozen);
            Assert.AreEqual(1, statuses.RemainingTurns(StatusKind.Burn));

            StatusTick second = statuses.Tick();
            Assert.AreEqual(StatusEffects.BurnDamagePerTurn, second.BurnDamage);
            Assert.IsFalse(statuses.Has(StatusKind.Burn), "지속 턴 소진 후 해제");

            Assert.AreEqual(0, statuses.Tick().BurnDamage);
        }

        [Test]
        public void Apply_ExistingStatus_ExtendsToLongerDuration()
        {
            var statuses = new StatusEffects();
            statuses.Apply(StatusKind.Burn, 3);
            statuses.Apply(StatusKind.Burn, 1);
            Assert.AreEqual(3, statuses.RemainingTurns(StatusKind.Burn), "짧은 재부여가 남은 턴을 줄이지 않는다");

            statuses.Apply(StatusKind.Burn, 5);
            Assert.AreEqual(5, statuses.RemainingTurns(StatusKind.Burn));
        }

        [Test]
        public void Freeze_MarksTurnAsSkipped()
        {
            var statuses = new StatusEffects();
            statuses.Apply(StatusKind.Freeze, 2);

            Assert.IsTrue(statuses.Tick().Frozen);
            Assert.IsTrue(statuses.Tick().Frozen);
            Assert.IsFalse(statuses.Tick().Frozen, "지속 턴 이후 해제");
        }

        [Test]
        public void Reaction_BurnThawsFreeze_AndFreezeQuenchesBurn()
        {
            // 반응 테이블: 불↔얼음 상쇄. 상쇄가 일어나면 새 상태는 소모된다.
            var statuses = new StatusEffects();
            statuses.Apply(StatusKind.Freeze, 3);
            statuses.Apply(StatusKind.Burn, 2);
            Assert.IsFalse(statuses.Has(StatusKind.Freeze), "화상이 빙결을 해동");
            Assert.IsFalse(statuses.Has(StatusKind.Burn), "상쇄에 쓰인 화상은 붙지 않는다");

            statuses.Apply(StatusKind.Burn, 2);
            statuses.Apply(StatusKind.Freeze, 3);
            Assert.IsFalse(statuses.Has(StatusKind.Burn), "빙결이 화상을 진화");
            Assert.IsFalse(statuses.Has(StatusKind.Freeze));
        }

        [Test]
        public void Poison_TicksDamageIndependently_AndExpires()
        {
            var statuses = new StatusEffects();
            statuses.Apply(StatusKind.Poison, 2);

            StatusTick first = statuses.Tick();
            Assert.AreEqual(StatusEffects.PoisonDamagePerTurn, first.PoisonDamage);
            Assert.AreEqual(0, first.BurnDamage, "독은 화상과 별개");
            Assert.IsFalse(first.Frozen);

            statuses.Tick(); // 남은 1턴 소진
            Assert.IsFalse(statuses.Has(StatusKind.Poison), "지속 턴 소진 후 해제");
            Assert.AreEqual(0, statuses.Tick().PoisonDamage);
        }

        [Test]
        public void BurnAndPoison_StackTheirDamage()
        {
            var statuses = new StatusEffects();
            statuses.Apply(StatusKind.Burn, 2);
            statuses.Apply(StatusKind.Poison, 2);

            StatusTick tick = statuses.Tick();
            Assert.AreEqual(StatusEffects.BurnDamagePerTurn, tick.BurnDamage);
            Assert.AreEqual(StatusEffects.PoisonDamagePerTurn, tick.PoisonDamage);
            Assert.AreEqual(tick.BurnDamage + tick.PoisonDamage, tick.TotalDamage);
        }

        [Test]
        public void Freeze_DoesNotCancelPoison()
        {
            var statuses = new StatusEffects();
            statuses.Apply(StatusKind.Poison, 3);
            statuses.Apply(StatusKind.Freeze, 2); // 화상은 상쇄하지만 독은 못 지운다

            Assert.IsTrue(statuses.Has(StatusKind.Poison), "빙결은 독을 지우지 않는다");
            Assert.IsTrue(statuses.Has(StatusKind.Freeze));

            StatusTick tick = statuses.Tick();
            Assert.IsTrue(tick.Frozen);
            Assert.AreEqual(StatusEffects.PoisonDamagePerTurn, tick.PoisonDamage, "얼어도 독 피해는 들어간다");
        }

        [Test]
        public void Combatant_CarriesStatuses()
        {
            var state = new CombatantState("hero", new GridPos(0, 0, 0), 10, 1);
            state.Statuses.Apply(StatusKind.Burn, 2);

            StatusTick tick = state.Statuses.Tick();
            state.TakeDamage(tick.BurnDamage);

            Assert.AreEqual(10 - StatusEffects.BurnDamagePerTurn, state.Hp);
        }
    }
}
