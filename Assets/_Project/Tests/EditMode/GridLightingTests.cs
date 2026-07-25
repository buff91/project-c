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
    }
}
