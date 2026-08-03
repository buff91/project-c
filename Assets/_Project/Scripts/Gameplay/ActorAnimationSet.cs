using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>공통 프레임 애니메이터가 액터와 환경 클립을 동일하게 조회하는 계약.</summary>
    public interface ISpriteClipSet
    {
        bool HasClips { get; }
        SpriteClip Find(string tag);
    }

    /// <summary>
    /// Aseprite 태그 하나를 구운 프레임 시퀀스. 타이밍은 "프레임 시작 시각 + 클립 총 길이"로
    /// 저장한다 — sprite 커브의 ObjectReferenceKeyframe을 무손실로 옮기고(가변 지속시간 보존),
    /// 런타임 프레임 선택이 "t 이하인 마지막 키" 하나로 끝난다(<c>SpriteClipRules.FrameAt</c>).
    /// </summary>
    [Serializable]
    public sealed class SpriteClip
    {
        public string tag;              // 기본 6태그 또는 idle-north 같은 방향 태그
        public bool loop;               // clip.isLooping에서 베이크 (원샷 태그는 Repeat=1 규약)
        public Sprite[] frames;
        public float[] frameStartTimes; // 오름차순, [0] == 0
        public float length;            // clip.length — 마지막 프레임 지속시간의 출처

        public bool IsPlayable =>
            frames != null &&
            frames.Length > 0 &&
            frameStartTimes != null &&
            frameStartTimes.Length == frames.Length;
    }

    /// <summary>
    /// 액터 하나의 애니메이션 묶음. <c>actorKey</c>는 카탈로그 Sprite 슬롯 필드명과 같은
    /// 계약("player"·"knight"·"goblin"…)이다 — 베이크(SyncCatalog)가 파일명→슬롯 매핑
    /// (<c>CatalogSlots</c>)을 그대로 재사용해 키를 얻는다.
    /// </summary>
    [Serializable]
    public sealed class ActorAnimationSet : ISpriteClipSet
    {
        public string actorKey;
        public List<SpriteClip> clips = new List<SpriteClip>();

        public bool HasClips => clips != null && clips.Count > 0;

        public bool HasDirectionalClips
        {
            get
            {
                if (clips == null) return false;
                for (int i = 0; i < clips.Count; i++)
                {
                    SpriteClip clip = clips[i];
                    if (clip != null &&
                        ProjectC.Core.DirectionalSpriteClipTags.TryParse(
                            clip.tag,
                            out _,
                            out _))
                        return true;
                }

                return false;
            }
        }

        public SpriteClip Find(string tag)
        {
            if (clips == null || string.IsNullOrEmpty(tag)) return null;
            for (int i = 0; i < clips.Count; i++)
            {
                SpriteClip clip = clips[i];
                if (clip != null &&
                    string.Equals(clip.tag, tag, StringComparison.OrdinalIgnoreCase))
                    return clip;
            }

            return null;
        }

        /// <summary>방향 클립을 우선 찾고, 없으면 기존 무방향 클립으로 폴백한다.</summary>
        public SpriteClip Find(string baseTag, ProjectC.Core.ActorFacing4 facing)
        {
            if (ProjectC.Core.DirectionalSpriteClipTags.TryCompose(
                    baseTag,
                    facing,
                    out string directionalTag))
            {
                SpriteClip directional = Find(directionalTag);
                if (directional != null) return directional;
            }

            return Find(baseTag);
        }
    }

    /// <summary>
    /// 환경/소품 슬롯 하나의 Aseprite 태그 클립. 현재 런타임 계약은 <c>idle</c>
    /// 루프만 사용한다. 슬롯 키는 CatalogSlots의 필드명(hubCampfire 등)이다.
    /// </summary>
    [Serializable]
    public sealed class EnvironmentAnimationSet : ISpriteClipSet
    {
        public string slotKey;
        public List<SpriteClip> clips = new List<SpriteClip>();

        public bool HasClips => clips != null && clips.Count > 0;

        public SpriteClip Find(string tag)
        {
            if (clips == null || string.IsNullOrEmpty(tag)) return null;
            for (int i = 0; i < clips.Count; i++)
            {
                SpriteClip clip = clips[i];
                if (clip != null &&
                    string.Equals(clip.tag, tag, StringComparison.OrdinalIgnoreCase))
                    return clip;
            }

            return null;
        }
    }
}
