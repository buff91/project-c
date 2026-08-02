using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 첫 던전 B2 시작 공간의 낮은 비충돌 드레싱.
    /// 별도 장애물을 만들지 않고 선택된 바닥 타일의 완성형 스프라이트만 교체한다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private static readonly Vector2Int[] BarrelBaySconceDirections =
        {
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 0),
        };

        private enum DungeonFloorDressingKind
        {
            ParkingWheelStop,
            FallenWayfinding,
            Service,
            Grate,
            Cracked,
            Macro0,
            Macro1,
            Macro2,
            Macro3,
        }

        private readonly struct DungeonFloorDressing
        {
            public DungeonFloorDressingKind Kind { get; }
            public int WorldFacingQuarterTurns { get; }
            public bool UsesB2BarrelBay { get; }

            public DungeonFloorDressing(
                DungeonFloorDressingKind kind,
                int worldFacingQuarterTurns,
                bool usesB2BarrelBay = false)
            {
                Kind = kind;
                WorldFacingQuarterTurns = worldFacingQuarterTurns;
                UsesB2BarrelBay = usesB2BarrelBay;
            }
        }

        private readonly Dictionary<GridPos, DungeonFloorDressing> _dungeonFloorDressing =
            new Dictionary<GridPos, DungeonFloorDressing>();
        private B2HeroRoomLayout _b2HeroRoomLayout;

        private void ResetDungeonDressingForBuild()
        {
            _dungeonFloorDressing.Clear();
            _b2HeroRoomLayout = null;
        }

        private bool TryGetDungeonFloorDressing(
            GridPos pos,
            out Sprite sprite,
            out PrototypeEnvironmentSprites.EnvironmentAccentMode accentMode)
        {
            sprite = null;
            accentMode = PrototypeEnvironmentSprites.EnvironmentAccentMode.Wood;
            if (visualCatalog == null ||
                !_dungeonFloorDressing.TryGetValue(pos, out DungeonFloorDressing dressing))
                return false;

            int viewQuarterTurns = _grid != null ? _grid.iso.viewQuarterTurns : 0;
            int effectiveView = DungeonDressingPlacementRules.ResolveViewIndex(
                dressing.WorldFacingQuarterTurns,
                viewQuarterTurns);
            switch (dressing.Kind)
            {
                case DungeonFloorDressingKind.ParkingWheelStop:
                    sprite = visualCatalog.B2ParkingWheelStopFloorFor(effectiveView);
                    accentMode = PrototypeEnvironmentSprites.EnvironmentAccentMode.Signal;
                    break;
                case DungeonFloorDressingKind.FallenWayfinding:
                    sprite = visualCatalog.B2FallenWayfindingFloorFor(effectiveView);
                    accentMode = PrototypeEnvironmentSprites.EnvironmentAccentMode.Signal;
                    break;
                case DungeonFloorDressingKind.Service:
                    sprite = (dressing.UsesB2BarrelBay
                                 ? visualCatalog.B2BarrelBayFloorFor(false, effectiveView)
                                 : null) ??
                             visualCatalog.HospitalFloorFor(6);
                    break;
                case DungeonFloorDressingKind.Grate:
                    sprite = (dressing.UsesB2BarrelBay
                                 ? visualCatalog.B2BarrelBayFloorFor(true, effectiveView)
                                 : null) ??
                             visualCatalog.HospitalFloorFor(0);
                    break;
                case DungeonFloorDressingKind.Cracked:
                    sprite = visualCatalog.B2CrackedFloorFor();
                    break;
            }
            return sprite != null;
        }

        private bool TryGetB2MacroFloorSprite(GridPos pos, out Sprite sprite)
        {
            sprite = null;
            if (visualCatalog == null ||
                !visualCatalog.HasCompleteB2MacroFloor ||
                !_dungeonFloorDressing.TryGetValue(pos, out DungeonFloorDressing dressing))
                return false;

            int role = (int)dressing.Kind - (int)DungeonFloorDressingKind.Macro0;
            if (role < 0 || role > 3)
                return false;

            int viewQuarterTurns = _grid != null ? _grid.iso.viewQuarterTurns : 0;
            sprite = visualCatalog.B2MacroFloorFor(role, viewQuarterTurns);
            return sprite != null;
        }

        private void PrepareDungeonDressing()
        {
            if (_b2HeroRoomLayout != null) return;
            _dungeonFloorDressing.Clear();
            if (hubMode || _dungeon == null || visualCatalog == null ||
                !_dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo active))
                return;

            int progressIndex = _dungeon.ProgressIndexFor(active.FloorIndex);
            if (!B2HeroRoomLayoutRules.TryCreate(
                    DungeonSelection.Selected.Id,
                    progressIndex,
                    dungeonSeed,
                    _grid.Map,
                    active,
                    _dungeon.OnwardStairOf(active),
                    BuildDungeonDressingReserved(active),
                    out B2HeroRoomLayout layout))
                return;

            _b2HeroRoomLayout = layout;
            // CreateRoomVisuals의 첫 가시성 갱신은 아직 B2 authored 벽 배치를 모른 채
            // 랜덤 sconce 필드를 만들 수 있다. 서비스 작업등 host로 즉시 다시 계산한다.
            MarkStaticLightDirty();
            bool hasBarrelBay = layout.TryGetBarrelBay(
                out GridPos barrelBayService,
                out GridPos barrelBayDrain,
                out int barrelBayFacing);
            foreach (GridPos pos in layout.RoomCells)
            {
                if (!layout.TryGetFloorPatch(pos, out B2HeroFloorPatchKind patch))
                    continue;
                if (layout.TryGetMacroFloorRole(pos, out int macroRole))
                {
                    if (visualCatalog.HasCompleteB2MacroFloor)
                    {
                        _dungeonFloorDressing[pos] = new DungeonFloorDressing(
                            (DungeonFloorDressingKind)(
                                (int)DungeonFloorDressingKind.Macro0 + macroRole),
                            worldFacingQuarterTurns: 0);
                    }
                    continue;
                }
                DungeonFloorDressingKind kind;
                switch (patch)
                {
                    case B2HeroFloorPatchKind.Grate:
                        kind = DungeonFloorDressingKind.Grate;
                        break;
                    case B2HeroFloorPatchKind.Cracked:
                        kind = DungeonFloorDressingKind.Cracked;
                        break;
                    default:
                        kind = DungeonFloorDressingKind.Service;
                        break;
                }
                bool usesBarrelBay = hasBarrelBay &&
                                     (pos == barrelBayService || pos == barrelBayDrain);
                _dungeonFloorDressing[pos] = new DungeonFloorDressing(
                    kind,
                    usesBarrelBay ? barrelBayFacing : 0,
                    usesBarrelBay);
            }

            // 좌표와 world facing은 layout의 named role이 소유한다. 한 카탈로그 슬롯이
            // 비어도 남은 자산이 다른 역할 좌표로 밀려가면 안 된다.
            if (layout.ParkingStop.HasValue &&
                visualCatalog.HasB2ParkingWheelStopFloor)
            {
                _dungeonFloorDressing[layout.ParkingStop.Value] =
                    new DungeonFloorDressing(
                        DungeonFloorDressingKind.ParkingWheelStop,
                        layout.ParkingStopWorldFacingQuarterTurns);
            }
            if (layout.FallenSign.HasValue &&
                visualCatalog.HasB2FallenWayfindingFloor)
            {
                _dungeonFloorDressing[layout.FallenSign.Value] =
                    new DungeonFloorDressing(
                        DungeonFloorDressingKind.FallenWayfinding,
                        layout.FallenSignWorldFacingQuarterTurns);
            }
        }

        private bool IsB2HeroRoomCell(GridPos pos) =>
            _b2HeroRoomLayout != null && _b2HeroRoomLayout.ContainsRoomCell(pos);

        private bool IsB2BarrelBaySconceHost(GridPos pos) =>
            _b2HeroRoomLayout != null &&
            _b2HeroRoomLayout.TryGetBarrelBay(
                out _,
                out GridPos drain,
                out _) &&
            pos == drain;

        private bool IsB2BarrelBaySconceFace(
            GridPos pos,
            Vector2Int outward)
        {
            if (_b2HeroRoomLayout == null ||
                !_b2HeroRoomLayout.TryGetBarrelBay(
                    out GridPos service,
                    out GridPos drain,
                    out _) ||
                pos != drain)
                return false;

            if (_grid == null || _dungeon == null)
                return true;

            int floor = _dungeon.Height.FloorIndex(drain.elevation);
            var pairAxis = new Vector2Int(
                drain.x - service.x,
                drain.y - service.y);
            int exteriorFaceCount = 0;
            Vector2Int firstExterior = default;
            bool pairAxisIsExterior = false;
            foreach (Vector2Int direction in BarrelBaySconceDirections)
            {
                if (HasPlanarTile(
                        drain.x + direction.x,
                        drain.y + direction.y,
                        floor))
                    continue;

                if (exteriorFaceCount == 0)
                    firstExterior = direction;
                exteriorFaceCount++;
                if (direction == pairAxis)
                    pairAxisIsExterior = true;
            }

            // 비코너는 유일한 실제 벽면을 그대로 쓴다. 코너 이상에서만 두 패널 중
            // service→drain 축(없으면 첫 외벽)을 골라 cell-level sconce 중복을 막는다.
            if (exteriorFaceCount <= 1)
                return true;

            Vector2Int preferred = pairAxisIsExterior ? pairAxis : firstExterior;
            return outward == preferred;
        }

        private bool TryGetB2HeroWallDecoration(
            GridPos pos,
            int outwardX,
            int outwardY,
            out int decoration)
        {
            decoration = -1;
            return _b2HeroRoomLayout != null &&
                   _b2HeroRoomLayout.TryGetWallDecoration(
                       pos,
                       outwardX,
                       outwardY,
                       out decoration);
        }

        private bool TryGetB2ServiceWallSegment(
            GridPos pos,
            int outwardX,
            int outwardY,
            out int segment)
        {
            segment = -1;
            return _b2HeroRoomLayout != null &&
                   _b2HeroRoomLayout.TryGetServiceWallSegment(
                       pos,
                       outwardX,
                       outwardY,
                       out segment);
        }

        private HashSet<GridPos> BuildDungeonDressingReserved(DungeonFloorInfo active)
        {
            var reserved = new HashSet<GridPos> { active.Entry };
            if (active.UpStairs.HasValue) reserved.Add(active.UpStairs.Value);
            if (active.DownStairs.HasValue) reserved.Add(active.DownStairs.Value);
            if (active.RestSite.HasValue) reserved.Add(active.RestSite.Value);
            if (active.ExtractionPoint.HasValue) reserved.Add(active.ExtractionPoint.Value);
            if (active.RescueNpc.HasValue) reserved.Add(active.RescueNpc.Value);
            if (active.Landmark.HasValue) reserved.Add(active.Landmark.Value);

            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State != null) reserved.Add(enemy.State.Position);
            }
            foreach (ItemAgent item in _items)
                reserved.Add(item.Spawn.Position);
            if (_barrelRenderer != null && !_barrelExploded)
            {
                reserved.Add(_barrelPos);
                reserved.Add(_barrelPos.North);
                reserved.Add(_barrelPos.East);
                reserved.Add(_barrelPos.South);
                reserved.Add(_barrelPos.West);
            }

            ReserveDungeonRoute(active.Entry, active.UpStairs, reserved);
            ReserveDungeonRoute(active.Entry, active.DownStairs, reserved);
            return reserved;
        }

        private void ReserveDungeonRoute(
            GridPos entry,
            GridPos? destination,
            ISet<GridPos> reserved)
        {
            if (!destination.HasValue) return;
            // 프레젠테이션 주동선은 아직 닫힌 문도 플레이 중 열고 지나갈 길로 본다.
            foreach (GridPos pos in GridPathfinder.FindPath(
                         _grid.Map,
                         entry,
                         destination.Value,
                         openClosedDoors: true))
                reserved.Add(pos);
        }

    }
}
