using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// Aseprite 태그 하나를 구운 프레임 시퀀스. 타이밍은 "프레임 시작 시각 + 클립 총 길이"로
    /// 저장한다 — sprite 커브의 ObjectReferenceKeyframe을 무손실로 옮기고(가변 지속시간 보존),
    /// 런타임 프레임 선택이 "t 이하인 마지막 키" 하나로 끝난다(<c>SpriteClipRules.FrameAt</c>).
    /// </summary>
    [Serializable]
    public sealed class SpriteClip
    {
        public string tag;              // SpriteClipTags 6종 중 하나
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
    public sealed class ActorAnimationSet
    {
        public string actorKey;
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
