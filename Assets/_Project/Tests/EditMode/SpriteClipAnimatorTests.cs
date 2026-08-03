using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEngine;

namespace ProjectC.Tests
{
    public class SpriteClipAnimatorTests
    {
        [Test]
        public void SetFacing_LoopingClip_PreservesPhaseAndAppliesFrameImmediately()
        {
            Texture2D texture = new Texture2D(4, 1);
            Sprite east0 = SpriteAt(texture, 0);
            Sprite east1 = SpriteAt(texture, 1);
            Sprite west0 = SpriteAt(texture, 2);
            Sprite west1 = SpriteAt(texture, 3);
            var set = new ActorAnimationSet
            {
                clips = new List<SpriteClip>
                {
                    Clip("idle-east", loop: true, east0, east1),
                    Clip("idle-west", loop: true, west0, west1)
                }
            };
            var host = new GameObject("SpriteClipAnimator Phase Test");

            try
            {
                SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
                SpriteClipAnimator animator = host.AddComponent<SpriteClipAnimator>();
                animator.Configure(renderer, set);
                SetPrivate(animator, "_clipTime", 0.75f);
                renderer.sprite = east1;

                animator.SetFacing(ActorFacing4.West);

                Assert.That(renderer.sprite, Is.SameAs(west1));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(east0);
                Object.DestroyImmediate(east1);
                Object.DestroyImmediate(west0);
                Object.DestroyImmediate(west1);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SetFacing_CompletedDeathClip_HoldsNewDirectionalLastFrame()
        {
            Texture2D texture = new Texture2D(6, 1);
            Sprite idleEast = SpriteAt(texture, 0);
            Sprite idleWest = SpriteAt(texture, 1);
            Sprite deathEast0 = SpriteAt(texture, 2);
            Sprite deathEast1 = SpriteAt(texture, 3);
            Sprite deathWest0 = SpriteAt(texture, 4);
            Sprite deathWest1 = SpriteAt(texture, 5);
            var set = new ActorAnimationSet
            {
                clips = new List<SpriteClip>
                {
                    Clip("idle-east", loop: true, idleEast),
                    Clip("idle-west", loop: true, idleWest),
                    Clip("death-east", loop: false, deathEast0, deathEast1),
                    Clip("death-west", loop: false, deathWest0, deathWest1)
                }
            };
            var host = new GameObject("SpriteClipAnimator Death Test");

            try
            {
                SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
                SpriteClipAnimator animator = host.AddComponent<SpriteClipAnimator>();
                animator.Configure(renderer, set);
                animator.PlayDeath();
                SetPrivate(animator, "_clipTime", 1f);
                SetPrivate(animator, "_holdingLastFrame", true);
                renderer.sprite = deathEast1;

                animator.SetFacing(ActorFacing4.West);

                Assert.That(renderer.sprite, Is.SameAs(deathWest1));
                Assert.That(GetPrivate<bool>(animator, "_holdingLastFrame"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(idleEast);
                Object.DestroyImmediate(idleWest);
                Object.DestroyImmediate(deathEast0);
                Object.DestroyImmediate(deathEast1);
                Object.DestroyImmediate(deathWest0);
                Object.DestroyImmediate(deathWest1);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SetFacing_CompletedHiddenDeathClip_PrimesNewDirectionalLastFrame()
        {
            Texture2D texture = new Texture2D(6, 1);
            Sprite idleEast = SpriteAt(texture, 0);
            Sprite idleWest = SpriteAt(texture, 1);
            Sprite deathEast0 = SpriteAt(texture, 2);
            Sprite deathEast1 = SpriteAt(texture, 3);
            Sprite deathWest0 = SpriteAt(texture, 4);
            Sprite deathWest1 = SpriteAt(texture, 5);
            var set = new ActorAnimationSet
            {
                clips = new List<SpriteClip>
                {
                    Clip("idle-east", loop: true, idleEast),
                    Clip("idle-west", loop: true, idleWest),
                    Clip("death-east", loop: false, deathEast0, deathEast1),
                    Clip("death-west", loop: false, deathWest0, deathWest1)
                }
            };
            var host = new GameObject("SpriteClipAnimator Hidden Death Test");

            try
            {
                SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
                SpriteClipAnimator animator = host.AddComponent<SpriteClipAnimator>();
                animator.Configure(renderer, set);
                animator.PlayDeath();
                SetPrivate(animator, "_clipTime", 1f);
                SetPrivate(animator, "_holdingLastFrame", true);
                renderer.sprite = deathEast1;
                renderer.enabled = false;

                animator.SetFacing(ActorFacing4.West);

                Assert.That(renderer.sprite, Is.SameAs(deathWest1));
                Assert.That(GetPrivate<bool>(animator, "_holdingLastFrame"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(idleEast);
                Object.DestroyImmediate(idleWest);
                Object.DestroyImmediate(deathEast0);
                Object.DestroyImmediate(deathEast1);
                Object.DestroyImmediate(deathWest0);
                Object.DestroyImmediate(deathWest1);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void SetFacing_ActiveDeathClip_PreservesProgressAndAppliesFrame()
        {
            Texture2D texture = new Texture2D(6, 1);
            Sprite idleEast = SpriteAt(texture, 0);
            Sprite idleWest = SpriteAt(texture, 1);
            Sprite deathEast0 = SpriteAt(texture, 2);
            Sprite deathEast1 = SpriteAt(texture, 3);
            Sprite deathWest0 = SpriteAt(texture, 4);
            Sprite deathWest1 = SpriteAt(texture, 5);
            var set = new ActorAnimationSet
            {
                clips = new List<SpriteClip>
                {
                    Clip("idle-east", loop: true, idleEast),
                    Clip("idle-west", loop: true, idleWest),
                    Clip("death-east", loop: false, deathEast0, deathEast1),
                    Clip("death-west", loop: false, deathWest0, deathWest1)
                }
            };
            var host = new GameObject("SpriteClipAnimator Active Death Test");

            try
            {
                SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
                SpriteClipAnimator animator = host.AddComponent<SpriteClipAnimator>();
                animator.Configure(renderer, set);
                animator.PlayDeath();
                SetPrivate(animator, "_clipTime", 0.75f);
                renderer.sprite = deathEast1;

                animator.SetFacing(ActorFacing4.West);

                Assert.That(renderer.sprite, Is.SameAs(deathWest1));
                Assert.That(GetPrivate<float>(animator, "_clipTime"), Is.EqualTo(0.75f));
                Assert.That(GetPrivate<bool>(animator, "_holdingLastFrame"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(idleEast);
                Object.DestroyImmediate(idleWest);
                Object.DestroyImmediate(deathEast0);
                Object.DestroyImmediate(deathEast1);
                Object.DestroyImmediate(deathWest0);
                Object.DestroyImmediate(deathWest1);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void PlayOnceAndHold_CompletedFall_HoldsLastFrameUntilWorldMotionEnds()
        {
            Texture2D texture = new Texture2D(3, 1);
            Sprite idle = SpriteAt(texture, 0);
            Sprite fall0 = SpriteAt(texture, 1);
            Sprite fall1 = SpriteAt(texture, 2);
            var set = new ActorAnimationSet
            {
                clips = new List<SpriteClip>
                {
                    Clip("idle-east", loop: true, idle),
                    Clip("fall-east", loop: false, fall0, fall1)
                }
            };
            var host = new GameObject("SpriteClipAnimator Fall Hold Test");

            try
            {
                SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
                SpriteClipAnimator animator = host.AddComponent<SpriteClipAnimator>();
                animator.Configure(renderer, set);
                animator.PlayOnceAndHold(SpriteClipTags.Fall);
                SetPrivate(animator, "_clipTime", 1f);

                InvokeUpdate(animator);

                Assert.That(renderer.sprite, Is.SameAs(fall1));
                Assert.That(GetPrivate<bool>(animator, "_holdingLastFrame"), Is.True);

                animator.StopToIdle();

                Assert.That(renderer.sprite, Is.SameAs(idle));
                Assert.That(GetPrivate<bool>(animator, "_holdingLastFrame"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(idle);
                Object.DestroyImmediate(fall0);
                Object.DestroyImmediate(fall1);
                Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void PlayLoopForDuration_FitsWholeCycleAndPreservesRateAcrossFacing()
        {
            Texture2D texture = new Texture2D(6, 1);
            Sprite idleEast = SpriteAt(texture, 0);
            Sprite idleWest = SpriteAt(texture, 1);
            Sprite walkEast0 = SpriteAt(texture, 2);
            Sprite walkEast1 = SpriteAt(texture, 3);
            Sprite walkWest0 = SpriteAt(texture, 4);
            Sprite walkWest1 = SpriteAt(texture, 5);
            var set = new ActorAnimationSet
            {
                clips = new List<SpriteClip>
                {
                    Clip("idle-east", loop: true, idleEast),
                    Clip("idle-west", loop: true, idleWest),
                    Clip("walk-east", loop: true, walkEast0, walkEast1),
                    Clip("walk-west", loop: true, walkWest0, walkWest1)
                }
            };
            var host = new GameObject("SpriteClipAnimator Timed Walk Test");

            try
            {
                SpriteRenderer renderer = host.AddComponent<SpriteRenderer>();
                SpriteClipAnimator animator = host.AddComponent<SpriteClipAnimator>();
                animator.Configure(renderer, set);

                animator.PlayLoopForDuration(SpriteClipTags.Walk, 0.25f);

                Assert.That(GetPrivate<float>(animator, "_playbackRate"), Is.EqualTo(4f));
                Assert.That(renderer.sprite, Is.SameAs(walkEast0));

                animator.SetFacing(ActorFacing4.West);

                Assert.That(GetPrivate<float>(animator, "_playbackRate"), Is.EqualTo(4f));
                Assert.That(renderer.sprite, Is.SameAs(walkWest0));

                animator.StopToIdle();
                Assert.That(GetPrivate<float>(animator, "_playbackRate"), Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(idleEast);
                Object.DestroyImmediate(idleWest);
                Object.DestroyImmediate(walkEast0);
                Object.DestroyImmediate(walkEast1);
                Object.DestroyImmediate(walkWest0);
                Object.DestroyImmediate(walkWest1);
                Object.DestroyImmediate(texture);
            }
        }

        private static SpriteClip Clip(string tag, bool loop, params Sprite[] frames)
        {
            var starts = new float[frames.Length];
            for (int i = 0; i < starts.Length; i++) starts[i] = i * 0.5f;
            return new SpriteClip
            {
                tag = tag,
                loop = loop,
                frames = frames,
                frameStartTimes = starts,
                length = frames.Length * 0.5f
            };
        }

        private static Sprite SpriteAt(Texture2D texture, int x)
        {
            return Sprite.Create(
                texture,
                new Rect(x, 0, 1, 1),
                new Vector2(0.5f, 0.5f),
                1f);
        }

        private static void SetPrivate<T>(SpriteClipAnimator animator, string name, T value)
        {
            typeof(SpriteClipAnimator)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(animator, value);
        }

        private static T GetPrivate<T>(SpriteClipAnimator animator, string name)
        {
            return (T)typeof(SpriteClipAnimator)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(animator);
        }

        private static void InvokeUpdate(SpriteClipAnimator animator)
        {
            typeof(SpriteClipAnimator)
                .GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(animator, null);
        }
    }
}
