using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// FOV와 독립적인 현재 층 지도 지식. 실제 타일 렌더러를 켜지 않고 공용 실루엣만
    /// 합성하며, mapped Unknown 목적지의 문 행동 포함 자동 이동을 소유한다. 실루엣 범주는
    /// 지도 구성 시 기억해 시야 밖의 실제 타일 상태 변화가 새 정보로 새지 않게 한다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private readonly HashSet<GridPos> _mappedTiles = new HashSet<GridPos>();
        private readonly Dictionary<GridPos, MapSilhouetteKind> _mappedSilhouettes =
            new Dictionary<GridPos, MapSilhouetteKind>();
        private readonly Dictionary<GridPos, SpriteRenderer> _mappedSilhouetteRenderers =
            new Dictionary<GridPos, SpriteRenderer>();
        private readonly List<GridPos> _staleMappedRendererTiles = new List<GridPos>();

        private void RebuildMappedTopology()
        {
            _mappedTiles.Clear();
            _mappedSilhouettes.Clear();
            _mappedSilhouetteRenderers.Clear();
            if (hubMode || _dungeon == null || _grid == null) return;

            foreach (var pair in _grid.Map.All())
            {
                int floorIndex = _dungeon.Height.FloorIndex(pair.Key.elevation);
                if (!_dungeon.TryGetFloor(floorIndex, out DungeonFloorInfo floor)) continue;
                if (MapKnowledgeRules.TryGetSilhouette(
                        floor,
                        floorIndex,
                        pair.Key,
                        pair.Value,
                        out MapSilhouetteKind silhouette))
                {
                    _mappedTiles.Add(pair.Key);
                    _mappedSilhouettes[pair.Key] = silhouette;
                }
            }
        }

        private void CreateMappedSilhouetteVisuals()
        {
            SynchronizeMappedSilhouetteRenderers();
        }

        private void EnsureMappedSilhouetteRenderer(GridPos pos)
        {
            if (_mappedSilhouetteRenderers.ContainsKey(pos) || _visualRoot == null) return;
            if (_dungeon.Height.FloorIndex(pos.elevation) != _activeFloorIndex) return;
            if (!TryGetMappedSilhouette(pos, out MapSilhouetteKind silhouette)) return;

            var tile = new GameObject($"Mapped Silhouette {pos} {silhouette}");
            tile.hideFlags = HideFlags.DontSaveInEditor;
            tile.transform.SetParent(_visualRoot, false);
            var renderer = tile.AddComponent<SpriteRenderer>();
            renderer.enabled = false;
            _mappedSilhouetteRenderers.Add(pos, renderer);
            ApplyMappedSilhouetteVisual(pos, renderer, silhouette);
        }

        private void RefreshMappedSilhouettes()
        {
            SynchronizeMappedSilhouetteRenderers();
            foreach (var pair in _mappedSilhouetteRenderers)
            {
                GridPos pos = pair.Key;
                SpriteRenderer renderer = pair.Value;
                if (renderer == null) continue;

                bool mapped = TryGetMappedSilhouette(pos, out MapSilhouetteKind silhouette);
                int tileFloor = _dungeon.Height.FloorIndex(pos.elevation);
                renderer.enabled = FloorVisibilityRules.ShouldRenderMappedSilhouette(
                    viewMode == DungeonViewMode.DebugAll,
                    tileFloor,
                    _activeFloorIndex,
                    _visibleTiles.Contains(pos),
                    _exploredTiles.Contains(pos),
                    mapped);
                if (mapped)
                    ApplyMappedSilhouetteVisual(pos, renderer, silhouette);
            }
        }

        /// <summary>
        /// mapped 표현용 GameObject는 현재 활성 층만 유지한다. 지도 지식 데이터는 전 층에
        /// 남지만 비활성 층까지 렌더러를 두 배로 만들 필요는 없다.
        /// </summary>
        private void SynchronizeMappedSilhouetteRenderers()
        {
            _staleMappedRendererTiles.Clear();
            foreach (var pair in _mappedSilhouetteRenderers)
            {
                if (_dungeon.Height.FloorIndex(pair.Key.elevation) == _activeFloorIndex)
                    continue;
                if (pair.Value != null)
                {
                    pair.Value.enabled = false;
                    if (Application.isPlaying) Destroy(pair.Value.gameObject);
                    else DestroyImmediate(pair.Value.gameObject);
                }
                _staleMappedRendererTiles.Add(pair.Key);
            }
            foreach (GridPos pos in _staleMappedRendererTiles)
                _mappedSilhouetteRenderers.Remove(pos);

            foreach (GridPos pos in _mappedTiles)
            {
                if (_dungeon.Height.FloorIndex(pos.elevation) == _activeFloorIndex)
                    EnsureMappedSilhouetteRenderer(pos);
            }
        }

        private void ApplyMappedSilhouetteVisual(
            GridPos pos,
            SpriteRenderer renderer,
            MapSilhouetteKind silhouette)
        {
            TileKind presentationKind = MappedPresentationKind(silhouette);
            renderer.sprite = GetMappedSilhouetteSprite(silhouette, pos);
            renderer.color = MappedSilhouetteColor(silhouette);
            renderer.transform.position = VisualPosition(pos);
            renderer.sortingOrder = _grid.iso.SortingOrder(
                TileVisualSortingPos(pos, presentationKind),
                TileSortOffset(presentationKind));
        }

        private bool TryGetMappedSilhouette(
            GridPos pos,
            out MapSilhouetteKind silhouette)
        {
            silhouette = default;
            if (!_mappedTiles.Contains(pos)) return false;
            if (!_grid.Map.Has(pos)) return false;
            return _mappedSilhouettes.TryGetValue(pos, out silhouette);
        }

        private Sprite GetMappedSilhouetteSprite(MapSilhouetteKind silhouette, GridPos pos)
        {
            TileKind presentationKind = MappedPresentationKind(silhouette);
            TileVisualFacts source = TileFactsFor(presentationKind, pos);
            // mapped는 실제 위치별 마모/Facility 드레싱을 공개하지 않는다. 높이 두께와
            // 문 평면처럼 토폴로지를 읽는 데 필요한 기하만 남기고 공용 샘플을 쓴다.
            var safeFacts = new TileVisualFacts(
                source.Context,
                source.Extruded,
                source.PlaneRisesRight,
                secretHinted: false,
                hubMode: false,
                hospitalDressing: false);
            return EnvironmentSprites.GetTileSprite(
                presentationKind,
                new GridPos(0, 0, pos.elevation),
                safeFacts);
        }

        private static TileKind MappedPresentationKind(MapSilhouetteKind silhouette)
        {
            return silhouette switch
            {
                MapSilhouetteKind.Barrier => TileKind.Wall,
                MapSilhouetteKind.Door => TileKind.DoorClosed,
                MapSilhouetteKind.Gap => TileKind.Hole,
                _ => TileKind.Floor
            };
        }

        private static Color MappedSilhouetteColor(MapSilhouetteKind silhouette)
        {
            return silhouette switch
            {
                MapSilhouetteKind.Barrier => new Color(0.31f, 0.38f, 0.42f, 0.30f),
                MapSilhouetteKind.Door => new Color(0.52f, 0.43f, 0.29f, 0.28f),
                MapSilhouetteKind.Gap => new Color(0.20f, 0.38f, 0.45f, 0.26f),
                _ => new Color(0.39f, 0.49f, 0.53f, 0.22f)
            };
        }

        private static Color32 MappedMinimapColor(MapSilhouetteKind silhouette)
        {
            return silhouette switch
            {
                MapSilhouetteKind.Barrier => new Color32(38, 48, 54, 255),
                MapSilhouetteKind.Door => new Color32(76, 65, 46, 255),
                MapSilhouetteKind.Gap => new Color32(30, 61, 70, 255),
                _ => new Color32(58, 70, 74, 255)
            };
        }

        /// <summary>조사나 폭발로 비밀문이 공개되면 그 층의 숨겨 둔 방 윤곽도 지도에 합친다.</summary>
        private void RevealMappedSecretRoom(GridPos revealedDoor)
        {
            if (_dungeon == null || _grid == null) return;
            int floorIndex = _dungeon.Height.FloorIndex(revealedDoor.elevation);
            if (!_dungeon.TryGetFloor(floorIndex, out DungeonFloorInfo floor) ||
                floor.SecretDoor != revealedDoor)
                return;

            AddRevealedMappedTile(revealedDoor);
            foreach (GridPos pos in floor.SecretRoomTiles)
                AddRevealedMappedTile(pos);
        }

        private void AddRevealedMappedTile(GridPos pos)
        {
            TileData tile = _grid.Map.Get(pos);
            if (tile == null) return;
            _mappedTiles.Add(pos);
            _mappedSilhouettes[pos] = MapKnowledgeRules.SilhouetteFor(tile.kind);
            EnsureMappedSilhouetteRenderer(pos);
        }

        private void TryTravelTowardMapped(GridPos target)
        {
            if (!TryGetMappedSilhouette(target, out MapSilhouetteKind silhouette))
            {
                InteractionFeedback?.Invoke("UNMAPPED — 알려진 길이 없다");
                return;
            }

            TileData targetTile = _grid.Map.Get(target);
            bool canTarget = silhouette == MapSilhouetteKind.Floor ||
                             silhouette == MapSilhouetteKind.Door;
            bool enterable = targetTile != null &&
                             (targetTile.IsWalkable || targetTile.kind == TileKind.DoorClosed);
            if (!canTarget || !enterable)
            {
                InteractionFeedback?.Invoke(
                    silhouette == MapSilhouetteKind.Barrier
                        ? "지도상 장벽이다 — 지나갈 수 없다"
                        : "지도상 갈 수 없는 곳");
                return;
            }

            if (FindMappedTravelPath(target).Count < 2)
            {
                InteractionFeedback?.Invoke("MAPPED — 이어지는 길이 없다");
                return;
            }

            InteractionFeedback?.Invoke("지도 경로로 이동...");
            StartPlayerAction(target, MovePlayerMappedPath(target));
        }

        private List<GridPos> FindMappedTravelPath(GridPos target)
        {
            bool OutsideKnownCurrentFloor(GridPos pos)
            {
                if (_dungeon.Height.FloorIndex(pos.elevation) != _activeFloorIndex)
                    return true;
                bool actualKnowledge = _visibleTiles.Contains(pos) ||
                                       _exploredTiles.Contains(pos);
                TileData tile = _grid.Map.Get(pos);
                if (tile != null &&
                    !MapKnowledgeRules.CanUseForMappedTravelPath(
                        tile.kind,
                        isExplicitKnownTarget: pos == target && actualKnowledge))
                    return true;
                return !_mappedTiles.Contains(pos) &&
                       !actualKnowledge;
            }

            return GridPathfinder.FindPath(
                _grid.Map,
                _playerPos,
                target,
                OutsideKnownCurrentFloor,
                openClosedDoors: true,
                // 사다리 칸까지 걷는 것은 허용하지만 링크 등반은 자기 탭/Space의 명시적 행동이다.
                canClimb: false);
        }

        /// <summary>
        /// mapped 목적지를 행동 단위로 재계획한다. 닫힌 일반 문은 통과 가능한 셀이 아니라
        /// 별도 열기 행동이며, 열고 적 턴/FOV/인터럽트를 처리한 뒤에만 다음 경로를 구한다.
        /// </summary>
        private IEnumerator MovePlayerMappedPath(GridPos target)
        {
            bool singleAction = AnyEnemyVisible();
            int actions = 0;
            bool walking = false;

            try
            {
                while (_playerState.IsAlive && !_runSummary.Ended && _playerPos != target)
                {
                    List<GridPos> path = FindMappedTravelPath(target);
                    if (path.Count < 2)
                    {
                        InteractionFeedback?.Invoke("MAPPED — 경로가 끊겼다");
                        yield break;
                    }

                    GridPos next = path[1];
                    TileData nextTile = _grid.Map.Get(next);
                    if (nextTile != null && nextTile.kind == TileKind.DoorClosed)
                    {
                        if (walking)
                        {
                            _playerAnimator?.StopToIdle();
                            ResetStaticWalkArtOffset();
                            walking = false;
                        }

                        SnapshotTravelSight();
                        int hpBeforeDoor = _playerState.Hp;
                        yield return SetDoorState(next, TileKind.DoorOpen);
                        if (_runTelemetry != null) _runTelemetry.doorInteractions++;
                        RefreshFloorVisibility();
                        InteractionFeedback?.Invoke("DOOR OPENED — 경로 재계산");
                        Debug.Log($"[Door] mapped travel {next} DOOR OPENED");
                        PreserveNewTravelEnemySighted();
                        yield return ResolveEnemyPhase();
                        actions++;

                        if (!_playerState.IsAlive)
                            yield break;
                        if (next == target)
                            yield break;
                        if (ShouldStopMappedTravelAfterAction(hpBeforeDoor) ||
                            singleAction && actions >= 1)
                            yield break;
                        continue;
                    }

                    if (nextTile == null || !nextTile.IsWalkable)
                    {
                        InteractionFeedback?.Invoke("MAPPED — 경로가 막혔다");
                        yield break;
                    }

                    if (!walking)
                    {
                        _playerAnimator?.PlayLoopForDuration(SpriteClipTags.Walk, secondsPerStep);
                        walking = true;
                    }

                    GridPos before = _playerPos;
                    int floorBefore = _activeFloorIndex;
                    int hpBeforeStep = _playerState.Hp;
                    yield return MovePlayerPathSteps(new[] { before, next });
                    actions++;

                    if (!_playerState.IsAlive || _runSummary.Ended ||
                        _activeFloorIndex != floorBefore || _playerPos != next)
                        yield break;
                    if (_playerPos == target)
                    {
                        if (IsBottomExit(target)) TryRequestExitChoice();
                        yield break;
                    }
                    if (ShouldStopMappedTravelAfterAction(hpBeforeStep))
                        yield break;
                    if (singleAction && actions >= 1)
                        yield break;
                }
            }
            finally
            {
                if (walking) _playerAnimator?.StopToIdle();
                ResetStaticWalkArtOffset();
            }
        }

        private bool ShouldStopMappedTravelAfterAction(int hpBeforeAction)
        {
            if (_travelCancelRequested)
            {
                InteractionFeedback?.Invoke("MOVE CANCELED");
                return true;
            }

            TravelInterrupt interrupt = EvaluateTravelInterruptAfterAction(
                _playerState.Hp < hpBeforeAction);
            if (interrupt == TravelInterrupt.None) return false;

            FloatingText?.Show(_player.transform.position, "!", FloatingTextKind.Alert);
            InteractionFeedback?.Invoke(interrupt switch
            {
                TravelInterrupt.PlayerDamaged => "INTERRUPTED — 피해를 입어 멈췄다",
                TravelInterrupt.EnemySighted => "ENEMY SIGHTED — 적 발견!",
                _ => "ITEM SIGHTED — 무언가 보인다"
            });
            return true;
        }
    }
}
