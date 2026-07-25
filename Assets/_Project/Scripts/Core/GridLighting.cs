namespace ProjectC.Core
{
    /// <summary>
    /// 타일 단위 광량(light level) 계산. 순수 C# — UnityEngine 비의존, EditMode 테스트 가능.
    /// (GDD 핵심 기둥 #3 제한된 시야 + 아트 방향 "청흑 void 바탕 + 국소 앰버 광원")
    ///
    /// 조명 엔진(Light2D)을 쓰지 않고, 이미 있는 SpriteRenderer.color 틴트에 곱할
    /// 0..1 밝기 한 축만 제공한다. 시야(FOV)가 "무엇이 보이는가"라면 여기는
    /// "보이는 것이 얼마나 밝은가"다. 알파(Unknown/Explored/Visible)와 직교한다.
    ///
    /// 규칙:
    /// - 앰비언트는 던전 깊이로 정해진다. 얕은 층은 지상에 가까워 밝고, 깊이 내려갈수록
    ///   어둠에 잠긴다. (지상 밝음 → 지하 어둠)
    /// - 광원(플레이어가 든 등불 등)은 거리에 따라 부드럽게 감쇠하는 빛 웅덩이를 만든다.
    ///   웅덩이 밖은 앰비언트만 남으므로, 깊은 층에서는 웅덩이 가장자리부터 그림자로 가라앉는다.
    /// - 여기서는 차폐(벽 뒤 그림자)를 계산하지 않는다. 플레이어 광원은 이미 FOV로
    ///   벽 너머가 걸러진 가시 집합에만 적용되므로 차폐가 사실상 공짜다. 정적 광원의
    ///   독립 차폐는 다음 단계(광원별 섀도우캐스트 캐시)에서 다룬다.
    /// </summary>
    public static class GridLighting
    {
        /// <summary>
        /// 던전 깊이별 앰비언트 밝기. depthIndex 0(최상층·지상 근처)=surfaceAmbient,
        /// deepestDepthIndex(최심층)=deepAmbient 로 선형 보간한다.
        /// 깊이 범위가 없으면(단층) 항상 surfaceAmbient.
        /// </summary>
        public static float AmbientForDepth(
            int depthIndex,
            int deepestDepthIndex,
            float surfaceAmbient,
            float deepAmbient)
        {
            if (depthIndex <= 0 || deepestDepthIndex <= 0) return surfaceAmbient;
            float t = (float)depthIndex / deepestDepthIndex;
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            return surfaceAmbient + (deepAmbient - surfaceAmbient) * t;
        }

        /// <summary>
        /// 점 광원의 거리 감쇠. distance=0에서 intensity, radius에서 0, 반경 밖은 0.
        /// 가장자리를 부드럽게 하려고 2차(감쇠²) 곡선을 쓴다 — 빛 웅덩이로 읽힌다.
        /// </summary>
        public static float PointFalloff(float distance, float radius, float intensity)
        {
            if (radius <= 0f || intensity <= 0f) return 0f;
            if (distance <= 0f) return intensity;
            if (distance >= radius) return 0f;
            float k = 1f - distance / radius; // 중심 1 → 가장자리 0
            return intensity * k * k;
        }

        /// <summary>
        /// 앰비언트 + 광원 하나를 합친 타일 광량(0..1로 포화).
        /// </summary>
        public static float TileLight(
            float ambient,
            float distanceToLight,
            float lightRadius,
            float lightIntensity)
        {
            float light = ambient + PointFalloff(distanceToLight, lightRadius, lightIntensity);
            if (light < 0f) return 0f;
            return light > 1f ? 1f : light;
        }
    }
}
