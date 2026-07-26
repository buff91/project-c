using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 프레임 애니메이션 시간 규칙 — Unity 엔진 타입을 쓰지 않아 dotnet shim에서도 돈다.
    /// 가변 프레임 지속시간(시작 시각 [0, 0.1, 0.3], 총 길이 0.5)을 기준 클립으로 쓴다:
    /// 프레임 0=0.1초, 1=0.2초, 2=0.2초.
    /// </summary>
    public class SpriteClipRulesTests
    {
        private static readonly float[] Times = { 0f, 0.1f, 0.3f };
        private const float Length = 0.5f;

        [TestCase(0f, 0)]
        [TestCase(0.09f, 0)]
        [TestCase(0.1f, 1)]
        [TestCase(0.29f, 1)]
        [TestCase(0.3f, 2)]
        [TestCase(0.49f, 2)]
        public void FrameAt_VariableDurations_PicksLastStartedFrame(float time, int expected)
        {
            Assert.AreEqual(
                expected,
                SpriteClipRules.FrameAt(Times, Length, loop: false, time, out bool finished));
            Assert.IsFalse(finished);
        }

        [Test]
        public void FrameAt_Loop_WrapsByLength_NeverFinishes()
        {
            Assert.AreEqual(1, SpriteClipRules.FrameAt(Times, Length, true, 0.6f, out bool f1));
            Assert.IsFalse(f1);
            Assert.AreEqual(0, SpriteClipRules.FrameAt(Times, Length, true, 1.0f, out bool f2));
            Assert.IsFalse(f2);
            Assert.AreEqual(2, SpriteClipRules.FrameAt(Times, Length, true, 5.4f, out bool f3));
            Assert.IsFalse(f3);
        }

        [Test]
        public void FrameAt_NonLoop_ClampsToLastFrame_AndFinishes()
        {
            Assert.AreEqual(2, SpriteClipRules.FrameAt(Times, Length, false, 0.5f, out bool atEnd));
            Assert.IsTrue(atEnd);
            Assert.AreEqual(2, SpriteClipRules.FrameAt(Times, Length, false, 100f, out bool far));
            Assert.IsTrue(far);
        }

        [Test]
        public void FrameAt_NegativeTime_ClampsToFirstFrame()
        {
            Assert.AreEqual(0, SpriteClipRules.FrameAt(Times, Length, false, -1f, out bool finished));
            Assert.IsFalse(finished);
        }

        [Test]
        public void FrameAt_DegenerateClips_FinishImmediately()
        {
            Assert.AreEqual(0, SpriteClipRules.FrameAt(null, 1f, true, 0f, out bool noFrames));
            Assert.IsTrue(noFrames);
            Assert.AreEqual(0, SpriteClipRules.FrameAt(new float[0], 1f, true, 0f, out bool empty));
            Assert.IsTrue(empty);
            Assert.AreEqual(0, SpriteClipRules.FrameAt(Times, 0f, true, 0f, out bool zeroLength));
            Assert.IsTrue(zeroLength);
        }

        [Test]
        public void FrameAt_SingleFrame_LoopsForever_OrFinishesAtLength()
        {
            float[] single = { 0f };
            Assert.AreEqual(0, SpriteClipRules.FrameAt(single, 0.2f, true, 7f, out bool looping));
            Assert.IsFalse(looping);
            Assert.AreEqual(0, SpriteClipRules.FrameAt(single, 0.2f, false, 0.2f, out bool done));
            Assert.IsTrue(done);
        }
    }
}
