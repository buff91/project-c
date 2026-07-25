using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    /// <summary>
    /// 타일 광량 규칙 검증: 깊이별 앰비언트(지상 밝음 → 지하 어둠)와
    /// 플레이어 광원 웅덩이 감쇠.
    /// </summary>
    public class GridLightingTests
    {
        [Test]
        public void Ambient_SurfaceIsBright_DeepIsDark()
        {
            float surface = GridLighting.AmbientForDepth(0, 9, 0.9f, 0.12f);
            float deep = GridLighting.AmbientForDepth(9, 9, 0.9f, 0.12f);
            Assert.AreEqual(0.9f, surface, 1e-4f, "최상층은 지상 밝기");
            Assert.AreEqual(0.12f, deep, 1e-4f, "최심층은 어둠 밝기");
            Assert.Less(deep, surface, "깊이 내려갈수록 어두워진다");
        }

        [Test]
        public void Ambient_Depth_DecreasesMonotonically()
        {
            float prev = GridLighting.AmbientForDepth(0, 10, 1f, 0f);
            for (int d = 1; d <= 10; d++)
            {
                float cur = GridLighting.AmbientForDepth(d, 10, 1f, 0f);
                Assert.LessOrEqual(cur, prev, $"깊이 {d}에서 밝기가 단조 감소하지 않음");
                prev = cur;
            }
        }

        [Test]
        public void Ambient_NoDepthRange_ReturnsSurface()
        {
            Assert.AreEqual(0.8f, GridLighting.AmbientForDepth(3, 0, 0.8f, 0.1f), 1e-4f);
            Assert.AreEqual(0.8f, GridLighting.AmbientForDepth(0, 9, 0.8f, 0.1f), 1e-4f);
        }

        [Test]
        public void PointFalloff_CenterIsIntensity_EdgeIsZero()
        {
            Assert.AreEqual(0.9f, GridLighting.PointFalloff(0f, 4f, 0.9f), 1e-4f, "중심");
            Assert.AreEqual(0f, GridLighting.PointFalloff(4f, 4f, 0.9f), 1e-4f, "반경 끝");
            Assert.AreEqual(0f, GridLighting.PointFalloff(6f, 4f, 0.9f), 1e-4f, "반경 밖");
        }

        [Test]
        public void PointFalloff_DecreasesWithDistance()
        {
            float near = GridLighting.PointFalloff(1f, 5f, 1f);
            float far = GridLighting.PointFalloff(3f, 5f, 1f);
            Assert.Greater(near, far, "가까울수록 밝다");
            Assert.Greater(near, 0f);
        }

        [Test]
        public void TileLight_PlayerTileFullyLit_EvenDeep()
        {
            // 깊은 층 앰비언트가 낮아도 광원 중심(거리 0)은 포화한다.
            float light = GridLighting.TileLight(0.12f, 0f, 4f, 0.95f);
            Assert.AreEqual(1f, light, 1e-4f);
        }

        [Test]
        public void TileLight_BeyondRadius_FallsToAmbient()
        {
            float ambient = 0.14f;
            float light = GridLighting.TileLight(ambient, 6f, 4f, 0.95f);
            Assert.AreEqual(ambient, light, 1e-4f, "웅덩이 밖은 앰비언트만 남는다");
        }

        [Test]
        public void TileLight_ClampedToOne()
        {
            float light = GridLighting.TileLight(0.9f, 0f, 4f, 0.95f);
            Assert.AreEqual(1f, light, 1e-4f, "앰비언트+광원이 1을 넘지 않는다");
        }

        [Test]
        public void TileLight_InsidePoolBrighterThanEdge()
        {
            float ambient = 0.14f;
            float near = GridLighting.TileLight(ambient, 1f, 4f, 0.95f);
            float edge = GridLighting.TileLight(ambient, 4f, 4f, 0.95f);
            Assert.Greater(near, edge, "웅덩이 안이 가장자리보다 밝다(어둠 속 그림자 그라데이션)");
        }

        private static GridMap Flat(int size)
        {
            var map = new GridMap();
            for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.Set(new GridPos(x, y, 0), TileKind.Floor);
            return map;
        }

        [Test]
        public void StaticField_LightsNearTiles_FallsWithDistance()
        {
            GridMap map = Flat(11);
            var lights = new[] { new GridLighting.PointLight(new GridPos(5, 5, 0), 4f, 0.9f) };

            var field = GridLighting.ComputeStaticField(map, lights, 0, 0);

            Assert.IsTrue(field.TryGetValue(new GridPos(5, 5, 0), out float center));
            Assert.IsTrue(field.TryGetValue(new GridPos(7, 5, 0), out float mid));
            Assert.Greater(center, mid, "광원에 가까울수록 밝다");
            Assert.Greater(mid, 0f);
        }

        [Test]
        public void StaticField_WallCastsShadow()
        {
            // 광원과 타일 사이에 벽 → 벽 너머는 광량 필드에 들어오지 않는다(캐스트 그림자).
            GridMap map = Flat(13);
            map.Set(new GridPos(8, 6, 0), TileKind.Wall);
            var lights = new[] { new GridLighting.PointLight(new GridPos(6, 6, 0), 6f, 1f) };

            var field = GridLighting.ComputeStaticField(map, lights, 0, 0);

            Assert.IsTrue(field.ContainsKey(new GridPos(7, 6, 0)), "벽 앞은 밝다");
            Assert.IsFalse(
                field.ContainsKey(new GridPos(10, 6, 0)),
                "벽 뒤 그림자에는 정적 광이 닿지 않는다");
        }

        [Test]
        public void StaticField_MultipleLights_Accumulate()
        {
            GridMap map = Flat(11);
            var one = new[] { new GridLighting.PointLight(new GridPos(5, 5, 0), 4f, 0.4f) };
            var two = new[]
            {
                new GridLighting.PointLight(new GridPos(5, 5, 0), 4f, 0.4f),
                new GridLighting.PointLight(new GridPos(6, 5, 0), 4f, 0.4f),
            };

            float single = GridLighting.ComputeStaticField(map, one, 0, 0)[new GridPos(5, 5, 0)];
            float doubled = GridLighting.ComputeStaticField(map, two, 0, 0)[new GridPos(5, 5, 0)];

            Assert.Greater(doubled, single, "겹치는 광원은 합산된다");
            Assert.LessOrEqual(doubled, 1f, "합산은 1로 포화한다");
        }

        [Test]
        public void StaticField_NoLights_IsEmpty()
        {
            GridMap map = Flat(5);
            var field = GridLighting.ComputeStaticField(
                map, new GridLighting.PointLight[0], 0, 0);
            Assert.AreEqual(0, field.Count);
        }

        [Test]
        public void ShadowedByNeighbor_WallNeighbor_CastsShadow()
        {
            GridMap map = Flat(5);
            map.Set(new GridPos(3, 2, 0), TileKind.Wall);
            Assert.IsTrue(GridLighting.ShadowedByNeighbor(
                map, new GridPos(2, 2, 0), 0, 0, 1, 0), "벽 이웃은 발치에 그림자를 드리운다");
        }

        [Test]
        public void ShadowedByNeighbor_HigherNeighbor_CastsShadow()
        {
            var map = new GridMap();
            map.Set(new GridPos(2, 2, 0), TileKind.Floor);
            map.Set(new GridPos(3, 2, 2), TileKind.Floor); // 두 단 높은 이웃 표면
            Assert.IsTrue(GridLighting.ShadowedByNeighbor(
                map, new GridPos(2, 2, 0), 0, 3, 1, 0), "더 높은 이웃이 그림자를 드리운다");
        }

        [Test]
        public void ShadowedByNeighbor_FlatOrEmpty_NoShadow()
        {
            GridMap map = Flat(5);
            Assert.IsFalse(GridLighting.ShadowedByNeighbor(
                map, new GridPos(2, 2, 0), 0, 0, 1, 0), "평평한 이웃은 그림자 없음");
            Assert.IsFalse(GridLighting.ShadowedByNeighbor(
                map, new GridPos(4, 4, 0), 0, 0, 1, 0), "이웃이 비어(맵 밖) 있으면 그림자 없음");
        }
    }
}
