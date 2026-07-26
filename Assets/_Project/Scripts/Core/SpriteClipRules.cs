namespace ProjectC.Core
{
    /// <summary>
    /// Aseprite 태그 애니메이션의 공식 태그 이름들. 베이크(에디터)와 재생 트리거(게임플레이)가
    /// 이 상수를 공유한다 — 문자열이 갈라지면 클립이 조용히 무시된다.
    /// </summary>
    public static class SpriteClipTags
    {
        public const string Idle = "idle";
        public const string Walk = "walk";
        public const string Attack = "attack";
        public const string Hit = "hit";
        public const string Fall = "fall";
        public const string Death = "death";
    }

    /// <summary>
    /// 프레임 애니메이션의 시간 → 프레임 인덱스 규칙. UnityEngine 무의존 —
    /// 재생 컴포넌트(SpriteClipAnimator)는 이 함수만 호출하고 렌더러 대입만 한다.
    /// 클립 데이터 계약: frameStartTimes는 오름차순이고 [0] == 0, length는 클립 총 길이
    /// (마지막 프레임의 지속시간이 여기서 나온다).
    /// </summary>
    public static class SpriteClipRules
    {
        /// <summary>
        /// 경계 규칙(테스트로 고정):
        /// 프레임 0개 → 0 반환·즉시 종료. 1개 → 항상 0(루프면 영원히, 아니면 length에서 종료).
        /// length ≤ 0 → 0·즉시 종료. 음수 clipTime은 0으로 클램프.
        /// 루프는 length 모듈로 순환하며 절대 finished가 되지 않는다.
        /// 비루프는 length 도달 시 마지막 프레임 유지 + finished.
        /// </summary>
        public static int FrameAt(
            float[] frameStartTimes,
            float length,
            bool loop,
            float clipTime,
            out bool finished)
        {
            int frameCount = frameStartTimes?.Length ?? 0;
            if (frameCount == 0 || length <= 0f)
            {
                finished = true;
                return 0;
            }

            if (clipTime < 0f) clipTime = 0f;

            float localTime;
            if (loop)
            {
                finished = false;
                localTime = clipTime % length;
            }
            else if (clipTime >= length)
            {
                finished = true;
                return frameCount - 1;
            }
            else
            {
                finished = false;
                localTime = clipTime;
            }

            // "localTime 이하의 시작 시각을 가진 마지막 프레임" — 클립은 6~12프레임이라 선형이 최선.
            int frame = 0;
            for (int i = 1; i < frameCount; i++)
            {
                if (frameStartTimes[i] <= localTime) frame = i;
                else break;
            }

            return frame;
        }
    }
}
