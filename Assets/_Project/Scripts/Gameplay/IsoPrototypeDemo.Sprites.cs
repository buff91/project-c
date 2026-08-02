using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 환경 스프라이트의 **어댑터**. 그림은 여기서 그리지 않는다 —
    /// 격자를 아는 이 클래스가 필요한 사실만 풀어서 <see cref="PrototypeEnvironmentSprites"/>에 넘긴다.
    ///
    /// 이 방향을 유지할 것: 픽셀을 만지는 코드가 다시 이 파일로 들어오면
    /// 격자·던전·플레이어에 손이 닿아 신(神) 클래스로 되돌아간다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private PrototypePalette _paletteInstance;
        private PrototypeEnvironmentSprites _environmentSpritesInstance;

        /// <summary>
        /// 던전 역할색. 카탈로그가 비어 있으면 인스펙터 폴백으로 떨어진다.
        /// 지연 생성 이유는 <see cref="ActorSprites"/>와 같다(편집 모드에는 Awake 가 없다).
        /// </summary>
        private PrototypePalette Palette =>
            _paletteInstance ??= new PrototypePalette(
                visualCatalog,
                new PrototypePalette.Fallbacks(
                    floorTop,
                    raisedTop,
                    tileSeam,
                    outline,
                    accent,
                    unknownFogColor,
                    unknownFogEdge));

        /// <summary>바닥·벽·문·광원 타일 임시 아트 팩토리.</summary>
        private PrototypeEnvironmentSprites EnvironmentSprites =>
            _environmentSpritesInstance ??=
                new PrototypeEnvironmentSprites(_spriteCache, Palette);

        private Sprite GetDungeonFogBackdropSprite() =>
            EnvironmentSprites.GetDungeonFogBackdropSprite();

        private Sprite GetWallSprite(bool torch) => EnvironmentSprites.GetWallSprite(torch);

        private Sprite GetHubWallSprite(bool torch, int decoration) =>
            EnvironmentSprites.GetHubWallSprite(torch, decoration);

        private Sprite GetHubLightTileSprite(string kind, int strength) =>
            EnvironmentSprites.GetHubLightTileSprite(kind, strength);

        private Sprite GetToneMappedEnvironmentSprite(
            Sprite sourceSprite,
            Color32 target,
            PrototypeEnvironmentSprites.EnvironmentAccentMode accentMode =
                PrototypeEnvironmentSprites.EnvironmentAccentMode.None) =>
            EnvironmentSprites.GetToneMappedEnvironmentSprite(sourceSprite, target, accentMode);

        private Sprite GetTileSprite(TileKind kind, GridPos pos)
        {
            TileVisualFacts facts = TileFactsFor(kind, pos);
            // 2×2 연속 바닥은 일반 바닥 재질이다. 완성형 소품의 Wood 경로가 아니라
            // 표준 mapped-tile 경로를 써야 전면 셀의 단차 측면과 B2 표면 톤을 보존한다.
            if (kind == TileKind.Floor &&
                TryGetB2MacroFloorSprite(pos, out Sprite macroFloor))
            {
                return EnvironmentSprites.GetMappedTileSprite(
                    macroFloor,
                    visualCatalog.DungeonSurfaceFor(facts.Context),
                    facts.Extruded,
                    facts.HubMode);
            }

            // B2의 낮은 장식은 바닥까지 합성된 완성형 타일이다. 이 경로로 들어와야
            // 기존 FOV·조명·높이 틴트·회전·정렬을 전부 그대로 공유한다. 일반 바닥은
            // 카탈로그 경로에서 dungeonStone으로 톤매핑되므로, 완성형 드레싱도 같은
            // 표면 램프를 거쳐야 원본 env-floor의 밝은 베이지가 섬처럼 남지 않는다.
            if (kind == TileKind.Floor &&
                TryGetDungeonFloorDressing(
                    pos,
                    out Sprite dressing,
                    out PrototypeEnvironmentSprites.EnvironmentAccentMode accentMode))
            {
                return GetToneMappedEnvironmentSprite(
                    dressing,
                    visualCatalog.DungeonSurfaceFor(facts.Context),
                    accentMode);
            }

            return EnvironmentSprites.GetTileSprite(kind, pos, facts);
        }

        /// <summary>
        /// 격자 질의를 종류에 맞게 골라 답만 채운다.
        ///
        /// 평면 방향·비밀문 힌트는 **해당 종류일 때만** 계산한다. 모든 타일에 걸면
        /// FOV 갱신 때마다 타일 수 × 격자 조회가 붙는다 — 원본의 지연 평가를 그대로 지킨다.
        /// </summary>
        private TileVisualFacts TileFactsFor(TileKind kind, GridPos pos)
        {
            bool door = kind == TileKind.DoorClosed ||
                        kind == TileKind.DoorOpen ||
                        kind == TileKind.SecretDoor ||
                        kind == TileKind.SecretPassage;
            bool stair = kind == TileKind.Stairs ||
                         kind == TileKind.StairsUp ||
                         kind == TileKind.StairsDown;

            DungeonVisualContext context = VisualContext(pos);
            return new TileVisualFacts(
                context,
                context.IsRaised || (!IsB2HeroRoomCell(pos) && IsFrontEdge(pos)),
                stair ? StairPlaneRisesRight(pos) : door && DoorPlaneRisesRight(pos),
                kind == TileKind.SecretDoor && IsSecretDoorHinted(pos),
                hubMode,
                !hubMode &&
                (_dungeon?.Region ?? DungeonRegionProfile.Facility) ==
                    DungeonRegionProfile.Facility &&
                // B2 시작방은 좌표 해시로 타일을 흩뿌리지 않고 히어로 룸 계획이
                // 선택한 설비/균열 군집만 쓴다. 나머지 방은 기존 희소 변주를 유지한다.
                !IsB2HeroRoomCell(pos));
        }

        // 진행 지수는 레이아웃이 소유한다 — elevation 으로 역산하지 않는다(GDD §5.1).
        private DungeonVisualContext VisualContext(GridPos pos) =>
            _dungeon != null
                ? DungeonVisualContext.From(_dungeon, pos.elevation)
                : DungeonVisualContext.Preview(pos.elevation);

        private bool IsSecretDoorHinted(GridPos pos) =>
            _playerState != null &&
            SecretRoomRules.CanInvestigate(_playerPos, pos);

        private bool DoorPlaneRisesRight(GridPos pos)
        {
            bool passageNorthSouth = HasDoorSide(pos.North) && HasDoorSide(pos.South);
            Vector2Int planeAxis = passageNorthSouth ? Vector2Int.right : Vector2Int.up;
            return AxisRisesRight(pos, planeAxis);
        }

        private bool StairPlaneRisesRight(GridPos pos)
        {
            if (StairTopology.TryGetHigherLanding(_grid.Map, pos, out GridPos landing))
                return _grid.iso.ProjectsToScreenRight(pos, landing);

            return AxisRisesRight(pos, Vector2Int.up);
        }

        private bool AxisRisesRight(GridPos pos, Vector2Int worldAxis)
        {
            Vector3 center = _grid.GridToWorld(pos);
            Vector3 alongPlane = _grid.GridToWorld(pos.Offset(worldAxis.x, worldAxis.y)) - center;
            return alongPlane.x * alongPlane.y >= 0f;
        }

        private bool HasDoorSide(GridPos pos)
        {
            TileData tile = _grid.Map.Get(pos);
            return tile != null && tile.IsSolidGround;
        }
    }
}
