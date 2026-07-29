using ProjectC.Core;
using UnityEngine;
using static ProjectC.Gameplay.PrototypeSpriteCanvas;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 던전 환경 역할색의 해석기 — `IsoVisualCatalog` 슬롯이 채워져 있으면 그 값을,
    /// 비어 있으면 인스펙터 폴백을 준다. 그리기 코드는 이 한 곳만 물어보고 카탈로그를 직접 보지 않는다.
    ///
    /// 역할색의 의미(청흑 void · 웜 그레이 석재 · 토치 골드 물리광원 · 틸 마법/출구)는
    /// `docs/STATUS.md`의 "던전 공통 톤"과 `project-c-torchstone.gpl` 18색 마스터 팔레트를 따른다.
    /// 여기서 새 색을 발명하지 않는다 — 슬롯을 늘릴 일이면 카탈로그를 먼저 늘린다.
    /// </summary>
    internal sealed class PrototypePalette
    {
        private readonly IsoVisualCatalog _catalog;
        private readonly Fallbacks _fallback;

        /// <summary>카탈로그 슬롯이 비었을 때 쓰는 인스펙터 값 묶음.</summary>
        internal readonly struct Fallbacks
        {
            internal Fallbacks(
                Color32 floorTop,
                Color32 raisedTop,
                Color32 tileSeam,
                Color32 outline,
                Color32 accent,
                Color32 unknownFog,
                Color32 unknownFogEdge)
            {
                FloorTop = floorTop;
                RaisedTop = raisedTop;
                TileSeam = tileSeam;
                Outline = outline;
                Accent = accent;
                UnknownFog = unknownFog;
                UnknownFogEdge = unknownFogEdge;
            }

            internal Color32 FloorTop { get; }
            internal Color32 RaisedTop { get; }
            internal Color32 TileSeam { get; }
            internal Color32 Outline { get; }
            internal Color32 Accent { get; }
            internal Color32 UnknownFog { get; }
            internal Color32 UnknownFogEdge { get; }
        }

        internal PrototypePalette(IsoVisualCatalog catalog, Fallbacks fallback)
        {
            _catalog = catalog;
            _fallback = fallback;
        }

        /// <summary>카탈로그 슬롯이 채워져 있는지 — 그리기 쪽이 매핑 스프라이트를 먼저 볼지 결정한다.</summary>
        internal IsoVisualCatalog Catalog => _catalog;

        internal Color32 Void =>
            _catalog != null ? _catalog.dungeonVoid : new Color32(5, 7, 12, 255);

        internal Color32 Fog =>
            _catalog != null ? _catalog.dungeonFog : _fallback.UnknownFog;

        internal Color32 FogEdge =>
            _catalog != null ? _catalog.dungeonFogEdge : _fallback.UnknownFogEdge;

        internal Color32 Outline =>
            _catalog != null ? _catalog.dungeonOutline : _fallback.Outline;

        internal Color32 Seam =>
            _catalog != null ? _catalog.dungeonSeam : _fallback.TileSeam;

        internal Color32 Stone =>
            _catalog != null ? _catalog.dungeonStone : _fallback.FloorTop;

        internal Color32 StoneShadow =>
            _catalog != null
                ? _catalog.dungeonStoneShadow
                : new Color32(31, 31, 27, 255);

        internal Color32 StoneLight =>
            _catalog != null ? _catalog.dungeonStoneLight : _fallback.RaisedTop;

        internal Color32 WallShadow =>
            _catalog != null
                ? _catalog.dungeonWallShadow
                : new Color32(43, 39, 34, 255);

        internal Color32 Wall =>
            _catalog != null
                ? _catalog.dungeonWall
                : new Color32(74, 64, 56, 255);

        internal Color32 WallLight =>
            _catalog != null
                ? _catalog.dungeonWallLight
                : new Color32(113, 97, 80, 255);

        internal Color32 Moss =>
            _catalog != null
                ? _catalog.dungeonMoss
                : new Color32(127, 178, 65, 255);

        internal Color32 Wood =>
            _catalog != null
                ? _catalog.dungeonWood
                : new Color32(74, 64, 56, 255);

        internal Color32 WoodLight =>
            _catalog != null
                ? _catalog.dungeonWoodLight
                : new Color32(154, 107, 34, 255);

        internal Color32 Iron =>
            _catalog != null
                ? _catalog.dungeonIron
                : new Color32(10, 13, 19, 255);

        internal Color32 Amber =>
            _catalog != null
                ? _catalog.dungeonAmber
                : new Color32(255, 189, 65, 255);

        internal Color32 AmberCore =>
            _catalog != null
                ? _catalog.dungeonAmberCore
                : new Color32(255, 213, 84, 255);

        internal Color32 Magic =>
            _catalog != null ? _catalog.dungeonMagic : _fallback.Accent;

        /// <summary>
        /// 같은 던전 층의 `LocalHeight`는 색상 테마가 아니라 명도로만 구분한다 — 석재색 자체는
        /// 모든 깊이에서 같아야 한다(테스트로 고정된 규칙, `docs/STATUS.md` "던전 공통 톤").
        /// </summary>
        internal Color32 SurfaceFor(DungeonVisualContext context)
        {
            if (_catalog != null)
                return _catalog.DungeonSurfaceFor(context);

            if (!context.IsRaised) return _fallback.FloorTop;
            float amount = Mathf.Clamp01(0.18f + context.LocalHeight * 0.08f);
            return Blend(_fallback.FloorTop, _fallback.RaisedTop, amount);
        }
    }
}
