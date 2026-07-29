using System;
using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public sealed class MessageLogTests
    {
        [Test]
        public void KeepsNewestLines_AndDropsOldestBeyondCapacity()
        {
            var log = new MessageLog(4);
            log.Add("a");
            log.Add("b");
            log.Add("c");
            log.Add("d");
            log.Add("e");

            Assert.That(log.Count, Is.EqualTo(4));
            Assert.That(log.Lines(), Is.EqualTo(new[] { "b", "c", "d", "e" }));
            Assert.That(log.Newest, Is.EqualTo("e"));
        }

        [Test]
        public void CollapsesConsecutiveDuplicates_IntoRepeatSuffix()
        {
            var log = new MessageLog(4);
            log.Add("약탈자를 쳤다 · 3 피해");
            log.Add("약탈자를 쳤다 · 3 피해");
            log.Add("약탈자를 쳤다 · 3 피해");

            Assert.That(log.Count, Is.EqualTo(1));
            Assert.That(log.Newest, Is.EqualTo("약탈자를 쳤다 · 3 피해 ×3"));
        }

        [Test]
        public void DoesNotCollapse_WhenDuplicatesAreNotAdjacent()
        {
            var log = new MessageLog(4);
            log.Add("a");
            log.Add("b");
            log.Add("a");

            Assert.That(log.Lines(), Is.EqualTo(new[] { "a", "b", "a" }));
        }

        [Test]
        public void CollapsedRun_CountsAsOneLineAgainstCapacity()
        {
            // 같은 적을 계속 때려도 로그가 한 행동으로 가득 차면 안 된다.
            var log = new MessageLog(2);
            log.Add("hit");
            for (int i = 0; i < 20; i++) log.Add("hit");
            log.Add("burn");

            Assert.That(log.Count, Is.EqualTo(2));
            Assert.That(log.Lines(), Is.EqualTo(new[] { "hit ×21", "burn" }));
        }

        [Test]
        public void IgnoresNullAndEmpty()
        {
            var log = new MessageLog(3);
            log.Add(null);
            log.Add(string.Empty);

            Assert.That(log.Count, Is.EqualTo(0));
            Assert.That(log.Newest, Is.Null);
        }

        [Test]
        public void NeverExceedsCapacity_UnderHeavyChurn()
        {
            var log = new MessageLog(3);
            for (int i = 0; i < 500; i++)
            {
                log.Add("line " + i);
                Assert.That(log.Count, Is.LessThanOrEqualTo(3));
            }
            Assert.That(log.Lines(), Is.EqualTo(new[] { "line 497", "line 498", "line 499" }));
        }

        [Test]
        public void Clear_EmptiesTheRing()
        {
            var log = new MessageLog(3);
            log.Add("a");
            log.Clear();

            Assert.That(log.Count, Is.EqualTo(0));
            Assert.That(log.Newest, Is.Null);
        }

        [Test]
        public void RejectsNonPositiveCapacity()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MessageLog(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MessageLog(-1));
        }
    }
}
