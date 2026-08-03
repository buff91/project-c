using System.Collections.Generic;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 월드 앵커 행동 휠의 전체 footprint를 화면 고정 HUD 밖으로 밀어낸다.
    /// 버튼 하나가 아니라 여섯 셀의 외곽을 검사해야 긴 문맥 옵션이 나타난 순간에도
    /// 좌하 로그·하단 레일·우상 계기를 침범하지 않는다.
    /// </summary>
    internal static class HudWheelPlacement
    {
        internal static Vector2 FindSafeCenter(
            Vector2 desired,
            Vector2 panelSize,
            Vector2 buttonSize,
            float radius,
            float margin,
            IReadOnlyList<Rect> reserved)
        {
            float extentX = radius + buttonSize.x * 0.5f;
            float extentY = radius + buttonSize.y * 0.5f;
            Vector2 anchor = ClampCenter(desired, panelSize, extentX, extentY, margin);
            float minX = extentX + margin;
            float minY = extentY + margin;
            float maxX = Mathf.Max(minX, panelSize.x - extentX - margin);
            float maxY = Mathf.Max(minY, panelSize.y - extentY - margin);

            var blockedRects = new List<Rect>(reserved?.Count ?? 0);
            var candidateX = new List<float> { anchor.x, minX, maxX };
            var candidateY = new List<float> { anchor.y, minY, maxY };
            if (reserved != null)
            {
                for (int i = 0; i < reserved.Count; i++)
                {
                    Rect blocked = Expand(reserved[i], margin);
                    blockedRects.Add(blocked);
                    AddCandidate(candidateX, Mathf.Clamp(
                        blocked.xMin - extentX, minX, maxX));
                    AddCandidate(candidateX, Mathf.Clamp(
                        blocked.xMax + extentX, minX, maxX));
                    AddCandidate(candidateY, Mathf.Clamp(
                        blocked.yMin - extentY, minY, maxY));
                    AddCandidate(candidateY, Mathf.Clamp(
                        blocked.yMax + extentY, minY, maxY));
                }
            }

            Vector2 bestSafe = anchor;
            float bestSafeDistance = float.PositiveInfinity;
            Vector2 bestFallback = anchor;
            float bestFallbackOverlap = float.PositiveInfinity;
            float bestFallbackDistance = float.PositiveInfinity;
            for (int y = 0; y < candidateY.Count; y++)
            {
                for (int x = 0; x < candidateX.Count; x++)
                {
                    var candidate = new Vector2(candidateX[x], candidateY[y]);
                    Rect wheel = Bounds(candidate, extentX, extentY);
                    float overlap = TotalOverlap(wheel, blockedRects);
                    float distance = (candidate - desired).sqrMagnitude;

                    if (overlap <= 0.0001f)
                    {
                        if (distance < bestSafeDistance)
                        {
                            bestSafe = candidate;
                            bestSafeDistance = distance;
                        }
                        continue;
                    }

                    if (overlap < bestFallbackOverlap ||
                        (Mathf.Approximately(overlap, bestFallbackOverlap) &&
                         distance < bestFallbackDistance))
                    {
                        bestFallback = candidate;
                        bestFallbackOverlap = overlap;
                        bestFallbackDistance = distance;
                    }
                }
            }

            // 모든 HUD가 화면을 덮어 안전 위치 자체가 없는 경우에도 화면 경계는 지키고,
            // 겹치는 총 면적이 가장 작은 후보를 반환한다. 일반 배치에서는 exhaustive
            // boundary 후보 중 항상 무충돌점을 고르므로 blocker 처리 순서에 좌우되지 않는다.
            return float.IsPositiveInfinity(bestSafeDistance)
                ? bestFallback
                : bestSafe;
        }

        internal static Rect Bounds(
            Vector2 center,
            Vector2 buttonSize,
            float radius) =>
            Bounds(
                center,
                radius + buttonSize.x * 0.5f,
                radius + buttonSize.y * 0.5f);

        private static Rect Bounds(Vector2 center, float extentX, float extentY) =>
            Rect.MinMaxRect(
                center.x - extentX,
                center.y - extentY,
                center.x + extentX,
                center.y + extentY);

        private static void AddCandidate(List<float> values, float candidate)
        {
            for (int i = 0; i < values.Count; i++)
                if (Mathf.Approximately(values[i], candidate)) return;
            values.Add(candidate);
        }

        private static float TotalOverlap(Rect wheel, IReadOnlyList<Rect> blocked)
        {
            float total = 0f;
            for (int i = 0; i < blocked.Count; i++)
            {
                float width = Mathf.Max(
                    0f,
                    Mathf.Min(wheel.xMax, blocked[i].xMax) -
                    Mathf.Max(wheel.xMin, blocked[i].xMin));
                float height = Mathf.Max(
                    0f,
                    Mathf.Min(wheel.yMax, blocked[i].yMax) -
                    Mathf.Max(wheel.yMin, blocked[i].yMin));
                total += width * height;
            }
            return total;
        }

        private static Vector2 ClampCenter(
            Vector2 center,
            Vector2 panelSize,
            float extentX,
            float extentY,
            float margin)
        {
            float minX = extentX + margin;
            float minY = extentY + margin;
            float maxX = Mathf.Max(minX, panelSize.x - extentX - margin);
            float maxY = Mathf.Max(minY, panelSize.y - extentY - margin);
            return new Vector2(
                Mathf.Clamp(center.x, minX, maxX),
                Mathf.Clamp(center.y, minY, maxY));
        }

        private static Rect Expand(Rect rect, float amount) =>
            Rect.MinMaxRect(
                rect.xMin - amount,
                rect.yMin - amount,
                rect.xMax + amount,
                rect.yMax + amount);
    }
}
