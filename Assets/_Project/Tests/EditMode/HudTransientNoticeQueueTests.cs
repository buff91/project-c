using NUnit.Framework;
using ProjectC.Gameplay;

namespace ProjectC.Tests
{
    public sealed class HudTransientNoticeQueueTests
    {
        [Test]
        public void PreservesDistinctNoticesInArrivalOrder()
        {
            var queue = new HudTransientNoticeQueue();
            var entry = new HudTransientNotice("진입", "기본 지급품", null);
            var route = new HudTransientNotice("사다리", "윗층 이동", "route-ladder");

            Assert.IsTrue(queue.Enqueue(entry));
            Assert.IsTrue(queue.Enqueue(route));
            Assert.IsTrue(queue.TryGetOrActivate(out HudTransientNotice first));
            Assert.AreEqual(entry, first);
            Assert.AreEqual(1, queue.PendingCount);

            queue.CompleteActive();
            Assert.IsTrue(queue.TryGetOrActivate(out HudTransientNotice second));
            Assert.AreEqual(route, second);
            Assert.AreEqual(0, queue.PendingCount);
        }

        [Test]
        public void DeduplicatesActiveAndPendingCopies()
        {
            var queue = new HudTransientNoticeQueue();
            var notice = new HudTransientNotice("길", "설명", "route-floor");

            Assert.IsTrue(queue.Enqueue(notice));
            Assert.IsFalse(queue.Enqueue(notice));
            Assert.IsTrue(queue.TryGetOrActivate(out _));
            Assert.IsFalse(queue.Enqueue(notice));
            Assert.AreEqual(0, queue.PendingCount);
        }

        [Test]
        public void ActiveNoticeCanBeReadAgainAfterVisualPause()
        {
            var queue = new HudTransientNoticeQueue();
            var notice = new HudTransientNotice("길", "설명", null);
            queue.Enqueue(notice);

            Assert.IsTrue(queue.TryGetOrActivate(out HudTransientNotice first));
            Assert.IsTrue(queue.TryGetOrActivate(out HudTransientNotice resumed));
            Assert.AreEqual(first, resumed);
            Assert.IsTrue(queue.HasActive);
        }

        [Test]
        public void ClearRemovesActiveAndPendingNotices()
        {
            var queue = new HudTransientNoticeQueue();
            queue.Enqueue(new HudTransientNotice("a", "1", null));
            queue.Enqueue(new HudTransientNotice("b", "2", null));
            queue.TryGetOrActivate(out _);

            queue.Clear();

            Assert.IsFalse(queue.HasActive);
            Assert.AreEqual(0, queue.PendingCount);
            Assert.IsFalse(queue.TryGetOrActivate(out _));
        }
    }
}
