using System;
using System.Collections.Generic;
using System.Linq;
using ProjectC.Core;
using ProjectC.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ProjectC.EditorTools
{
    /// <summary>
    /// Aseprite 태그 클립 → 카탈로그 프레임 시퀀스 베이크.
    /// 임포터가 만든 AnimationClip 서브에셋에서 sprite 커브만 뽑아
    /// <see cref="SpriteClip"/>으로 굽는다 — 런타임은 Animator 없이 이 산출물을 재생한다.
    /// (transform/color 커브는 의도적으로 버린다: 액터 루트의 position·scale·color는
    /// CombatFx 코루틴과 ApplyEnemyVisuals가 소유하므로 클립이 가지면 싸운다.)
    /// </summary>
    public static class ActorAnimationBake
    {
        private static readonly string[] KnownTags =
        {
            SpriteClipTags.Idle,
            SpriteClipTags.Walk,
            SpriteClipTags.Attack,
            SpriteClipTags.Hit,
            SpriteClipTags.Fall,
            SpriteClipTags.Death
        };

        private static readonly HashSet<string> OneShotTags = new HashSet<string>(StringComparer.Ordinal)
        {
            SpriteClipTags.Attack,
            SpriteClipTags.Hit,
            SpriteClipTags.Fall,
            SpriteClipTags.Death
        };

        public static bool IsOneShotTag(string tag) => tag != null && OneShotTags.Contains(tag);

        /// <summary>소스 하나의 모든 태그 클립을 굽는다. 같은 태그 중복은 첫 클립이 이긴다.</summary>
        public static ActorAnimationSet ExtractSet(string sourcePath, string actorKey)
        {
            var set = new ActorAnimationSet { actorKey = actorKey };
            IEnumerable<AnimationClip> clips = AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                .OfType<AnimationClip>()
                .OrderBy(clip => clip.name, StringComparer.Ordinal);
            foreach (AnimationClip clip in clips)
            {
                SpriteClip baked = ExtractClip(clip);
                if (baked != null && set.Find(baked.tag) == null)
                    set.clips.Add(baked);
            }

            return set;
        }

        /// <summary>환경/소품 원본에서는 idle 태그만 굽는다.</summary>
        public static EnvironmentAnimationSet ExtractEnvironmentSet(
            string sourcePath,
            string slotKey)
        {
            var set = new EnvironmentAnimationSet { slotKey = slotKey };
            IEnumerable<AnimationClip> clips =
                AssetDatabase.LoadAllAssetsAtPath(sourcePath)
                    .OfType<AnimationClip>()
                    .OrderBy(clip => clip.name, StringComparer.Ordinal);
            foreach (AnimationClip clip in clips)
            {
                SpriteClip baked = ExtractClip(clip);
                if (baked != null &&
                    string.Equals(
                        baked.tag,
                        SpriteClipTags.Idle,
                        StringComparison.OrdinalIgnoreCase) &&
                    set.Find(baked.tag) == null)
                    set.clips.Add(baked);
            }

            return set;
        }

        /// <summary>태그 규약 밖이거나 sprite 커브가 없으면 null.</summary>
        public static SpriteClip ExtractClip(AnimationClip clip)
        {
            if (clip == null) return null;
            string tag = TagFromClipName(clip.name);
            if (tag == null) return null;

            ObjectReferenceKeyframe[] keys = SpriteKeyframes(clip);
            if (keys == null || keys.Length == 0) return null;

            Array.Sort(keys, (a, b) => a.time.CompareTo(b.time));
            var frames = new Sprite[keys.Length];
            var times = new float[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                frames[i] = keys[i].value as Sprite;
                times[i] = keys[i].time;
            }

            return new SpriteClip
            {
                tag = tag,
                loop = clip.isLooping,
                frames = frames,
                frameStartTimes = times,
                length = clip.length
            };
        }

        public static ObjectReferenceKeyframe[] SpriteKeyframes(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in
                     AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite")
                    return AnimationUtility.GetObjectReferenceCurve(clip, binding);
            }

            return null;
        }

        /// <summary>sprite 외의(float) 커브가 있으면 참 — 베이크에서 버려지므로 Validate가 경고한다.</summary>
        public static bool HasNonSpriteCurves(AnimationClip clip) =>
            clip != null && AnimationUtility.GetCurveBindings(clip).Length > 0;

        /// <summary>
        /// 클립명 → 공식 태그. 임포터 명명 편차를 흡수한다: 정확 일치("idle") 또는
        /// 언더스코어 접미("actor-knight_idle"). 규약 밖이면 null.
        /// </summary>
        public static string TagFromClipName(string clipName)
        {
            if (string.IsNullOrEmpty(clipName)) return null;
            string lower = clipName.ToLowerInvariant();
            foreach (string tag in KnownTags)
            {
                if (lower == tag || lower.EndsWith("_" + tag, StringComparison.Ordinal))
                    return tag;
            }

            return null;
        }

        /// <summary>베이크 결과가 기존 카탈로그와 같으면 저장을 건너뛰기 위한 깊은 비교.</summary>
        public static bool SetsEqual(List<ActorAnimationSet> current, List<ActorAnimationSet> built)
        {
            if (current == null || built == null) return current == built;
            if (current.Count != built.Count) return false;
            for (int i = 0; i < current.Count; i++)
            {
                ActorAnimationSet a = current[i];
                ActorAnimationSet b = built[i];
                if (a == null || b == null) return false;
                if (!string.Equals(a.actorKey, b.actorKey, StringComparison.Ordinal)) return false;
                int aCount = a.clips?.Count ?? 0;
                int bCount = b.clips?.Count ?? 0;
                if (aCount != bCount) return false;
                for (int c = 0; c < aCount; c++)
                {
                    SpriteClip x = a.clips[c];
                    SpriteClip y = b.clips[c];
                    if (x == null || y == null) return false;
                    if (!string.Equals(x.tag, y.tag, StringComparison.Ordinal) ||
                        x.loop != y.loop ||
                        !Mathf.Approximately(x.length, y.length))
                        return false;
                    int xFrames = x.frames?.Length ?? 0;
                    int yFrames = y.frames?.Length ?? 0;
                    if (xFrames != yFrames) return false;
                    for (int f = 0; f < xFrames; f++)
                    {
                        if (x.frames[f] != y.frames[f] ||
                            !Mathf.Approximately(x.frameStartTimes[f], y.frameStartTimes[f]))
                            return false;
                    }
                }
            }

            return true;
        }

        public static bool EnvironmentSetsEqual(
            List<EnvironmentAnimationSet> current,
            List<EnvironmentAnimationSet> built)
        {
            if (current == null || built == null) return current == built;
            if (current.Count != built.Count) return false;
            for (int i = 0; i < current.Count; i++)
            {
                EnvironmentAnimationSet a = current[i];
                EnvironmentAnimationSet b = built[i];
                if (a == null || b == null) return false;
                if (!string.Equals(a.slotKey, b.slotKey, StringComparison.Ordinal))
                    return false;
                int aCount = a.clips?.Count ?? 0;
                int bCount = b.clips?.Count ?? 0;
                if (aCount != bCount) return false;
                for (int c = 0; c < aCount; c++)
                {
                    SpriteClip x = a.clips[c];
                    SpriteClip y = b.clips[c];
                    if (x == null || y == null) return false;
                    if (!string.Equals(x.tag, y.tag, StringComparison.Ordinal) ||
                        x.loop != y.loop ||
                        !Mathf.Approximately(x.length, y.length))
                        return false;
                    int xFrames = x.frames?.Length ?? 0;
                    int yFrames = y.frames?.Length ?? 0;
                    if (xFrames != yFrames) return false;
                    for (int f = 0; f < xFrames; f++)
                    {
                        if (x.frames[f] != y.frames[f] ||
                            !Mathf.Approximately(
                                x.frameStartTimes[f],
                                y.frameStartTimes[f]))
                            return false;
                    }
                }
            }

            return true;
        }
    }
}
