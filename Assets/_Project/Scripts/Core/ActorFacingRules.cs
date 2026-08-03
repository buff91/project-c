using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 격자 또는 현재 뷰에서 액터가 바라보는 4방향. 값의 순서는 시계 방향이라
    /// <see cref="ActorFacingRules.RotateToView"/>가 정수 덧셈으로 회전할 수 있다.
    /// </summary>
    public enum ActorFacing4
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    /// <summary>
    /// 격자상의 출발점/목표점과 뷰 회전으로 액터의 화면 기준 방향을 결정한다.
    /// UnityEngine 무의존 — 이동, 근접 공격, 원거리 공격이 같은 규칙을 공유한다.
    /// </summary>
    public static class ActorFacingRules
    {
        /// <summary>
        /// 회전하지 않은 월드 격자에서 origin이 target을 향하는 방향을 구한다.
        /// 같은 평면 좌표면 방향을 새로 정할 수 없으므로 false를 반환한다.
        /// </summary>
        public static bool TryResolveWorld(
            GridPos origin,
            GridPos target,
            out ActorFacing4 facing)
        {
            int dx = target.x - origin.x;
            int dy = target.y - origin.y;
            if (dx == 0 && dy == 0)
            {
                facing = ActorFacing4.South;
                return false;
            }

            facing = ResolveUnrotated(dx, dy);
            return true;
        }

        /// <summary>
        /// 월드 방향을 현재 뷰 기준 방향으로 변환한다. viewQuarterTurns의 부호는
        /// IsoGrid.RotateToView와 같다(양수 1회: North → East).
        /// </summary>
        public static bool TryResolveView(
            GridPos origin,
            GridPos target,
            int viewQuarterTurns,
            out ActorFacing4 facing)
        {
            if (!TryResolveWorld(origin, target, out ActorFacing4 worldFacing))
            {
                facing = ActorFacing4.South;
                return false;
            }

            facing = RotateToView(worldFacing, viewQuarterTurns);
            return true;
        }

        /// <summary>
        /// 방향을 판정할 수 없는 동일 좌표에서는 fallback을 그대로 유지한다.
        /// 피격 대상과 공격자가 같은 칸인 예외 연출에서도 호출부 분기가 필요 없다.
        /// </summary>
        public static ActorFacing4 ResolveViewOr(
            GridPos origin,
            GridPos target,
            int viewQuarterTurns,
            ActorFacing4 fallback)
        {
            Validate(fallback);
            return TryResolveView(origin, target, viewQuarterTurns, out ActorFacing4 resolved)
                ? resolved
                : fallback;
        }

        /// <summary>IsoGrid.RotateToView와 같은 부호로 90도 단위 회전을 적용한다.</summary>
        public static ActorFacing4 RotateToView(ActorFacing4 worldFacing, int viewQuarterTurns)
        {
            Validate(worldFacing);
            int rotated = ((int)worldFacing + NormalizeQuarterTurns(viewQuarterTurns)) % 4;
            return (ActorFacing4)rotated;
        }

        private static ActorFacing4 ResolveUnrotated(int dx, int dy)
        {
            long absX = Math.Abs((long)dx);
            long absY = Math.Abs((long)dy);
            if (absX > absY) return dx > 0 ? ActorFacing4.East : ActorFacing4.West;
            if (absY > absX) return dy > 0 ? ActorFacing4.North : ActorFacing4.South;

            // 정확한 대각선은 반시계 쪽 반평면에 포함한다. 이 경계 선택은 뷰를
            // 90도 돌렸을 때 결과도 정확히 한 방향 회전하도록 만든다.
            if (dx > 0) return dy > 0 ? ActorFacing4.North : ActorFacing4.East;
            return dy > 0 ? ActorFacing4.West : ActorFacing4.South;
        }

        private static int NormalizeQuarterTurns(int value)
        {
            int normalized = value % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }

        private static void Validate(ActorFacing4 facing)
        {
            if ((int)facing < (int)ActorFacing4.North ||
                (int)facing > (int)ActorFacing4.West)
            {
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "Unknown actor facing.");
            }
        }
    }

    /// <summary>
    /// 방향별 Aseprite 태그 계약. 기존 기본 태그(idle, walk 등)는 그대로 유효하고,
    /// 방향 클립이 있을 때만 "{base}-{direction}"을 우선 조회할 수 있게 한다.
    /// </summary>
    public static class DirectionalSpriteClipTags
    {
        public static bool IsSupportedBaseTag(string tag)
        {
            switch (tag)
            {
                case SpriteClipTags.Idle:
                case SpriteClipTags.Walk:
                case SpriteClipTags.Attack:
                case SpriteClipTags.Hit:
                case SpriteClipTags.Fall:
                case SpriteClipTags.Death:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// 유효한 기본 태그와 방향을 조합한다. 잘못된 태그는 에셋 빌드 단계에서
        /// 즉시 드러나야 하므로 조용히 문자열을 만들지 않고 예외를 던진다.
        /// </summary>
        public static string Compose(string baseTag, ActorFacing4 facing)
        {
            if (!IsSupportedBaseTag(baseTag))
            {
                throw new ArgumentException("Unsupported base sprite clip tag.", nameof(baseTag));
            }

            return baseTag + "-" + Suffix(facing);
        }

        public static bool TryCompose(string baseTag, ActorFacing4 facing, out string directionalTag)
        {
            directionalTag = null;
            if (!IsSupportedBaseTag(baseTag) || !IsDefined(facing)) return false;
            directionalTag = baseTag + "-" + Suffix(facing);
            return true;
        }

        /// <summary>방향 태그를 검증하고 기본 태그와 방향으로 분해한다.</summary>
        public static bool TryParse(
            string directionalTag,
            out string baseTag,
            out ActorFacing4 facing)
        {
            baseTag = null;
            facing = ActorFacing4.South;
            if (string.IsNullOrEmpty(directionalTag)) return false;

            int separator = directionalTag.LastIndexOf('-');
            if (separator <= 0 || separator == directionalTag.Length - 1) return false;

            string candidateBase = directionalTag.Substring(0, separator);
            if (!IsSupportedBaseTag(candidateBase) ||
                !TryParseSuffix(directionalTag.Substring(separator + 1), out ActorFacing4 parsedFacing))
            {
                return false;
            }

            baseTag = candidateBase;
            facing = parsedFacing;
            return true;
        }

        /// <summary>기존 기본 태그와 새 방향 태그를 모두 공식 태그로 인정한다.</summary>
        public static bool IsSupportedTag(string tag)
        {
            return IsSupportedBaseTag(tag) || TryParse(tag, out _, out _);
        }

        private static string Suffix(ActorFacing4 facing)
        {
            switch (facing)
            {
                case ActorFacing4.North: return "north";
                case ActorFacing4.East: return "east";
                case ActorFacing4.South: return "south";
                case ActorFacing4.West: return "west";
                default:
                    throw new ArgumentOutOfRangeException(nameof(facing), facing, "Unknown actor facing.");
            }
        }

        private static bool TryParseSuffix(string suffix, out ActorFacing4 facing)
        {
            switch (suffix)
            {
                case "north": facing = ActorFacing4.North; return true;
                case "east": facing = ActorFacing4.East; return true;
                case "south": facing = ActorFacing4.South; return true;
                case "west": facing = ActorFacing4.West; return true;
                default: facing = ActorFacing4.South; return false;
            }
        }

        private static bool IsDefined(ActorFacing4 facing)
        {
            return (int)facing >= (int)ActorFacing4.North &&
                   (int)facing <= (int)ActorFacing4.West;
        }
    }
}
