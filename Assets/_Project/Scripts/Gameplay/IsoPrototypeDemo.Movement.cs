using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public partial class IsoPrototypeDemo
    {

        private IEnumerator ApproachAndToggleDoor(IReadOnlyList<GridPos> path, GridPos door)
        {
            yield return MovePlayerPath(path);

            TileData tile = _grid.Map.Get(door);
            if (IsPlayerAdjacentTo(door) && tile != null && (tile.CanOpen || tile.CanClose))
            {
                TileKind nextKind = tile.CanOpen ? TileKind.DoorOpen : TileKind.DoorClosed;
                if (nextKind == TileKind.DoorClosed && IsLivingEnemyAt(door))
                {
                    InteractionFeedback?.Invoke("무언가 문을 막고 있다!");
                    yield break;
                }
                yield return SetDoorState(door, nextKind);
                if (_runTelemetry != null) _runTelemetry.doorInteractions++;
                RefreshFloorVisibility();
                string feedback = nextKind == TileKind.DoorOpen ? "DOOR OPENED" : "DOOR CLOSED";
                InteractionFeedback?.Invoke(feedback);
                Debug.Log($"[Door] {door} {feedback}");
                yield return ResolveEnemyPhase();
            }
        }

        private IEnumerator ApproachAndRevealSecretDoor(
            IReadOnlyList<GridPos> path,
            GridPos secretDoor)
        {
            yield return MovePlayerPath(path);

            TileData tile = _grid.Map.Get(secretDoor);
            if (!_playerState.IsAlive ||
                !SecretRoomRules.CanInvestigate(_playerPos, secretDoor) ||
                !SecretRoomRules.IsSecretDoor(tile))
                yield break;

            yield return SetDoorState(secretDoor, TileKind.SecretPassage);
            _runTelemetry?.RecordSecretRoomFound(GlobalFloorIndex(_activeFloorIndex));
            RefreshFloorVisibility();
            FloatingText?.Show(_player.transform.position, "!", FloatingTextKind.Alert);
            InteractionFeedback?.Invoke("숨은 통로 발견 — 안쪽에서 희귀한 기운이 느껴진다");
            Debug.Log($"[SecretRoom] {FloorLabel(_activeFloorIndex)} 비밀문 발견 {secretDoor}");
            yield return ResolveEnemyPhase();
        }

        private IEnumerator ApproachAndDrop(IReadOnlyList<GridPos> path, GridPos hole)
        {
            yield return MovePlayerPath(path);

            // 의도적 낙하도 TryFall 하나로 수렴 — 낙뎀을 감수하는 하강 수단이다. (GDD §5.3)
            GridPos? landing = _grid.Map.FindLandingBelow(hole, BottomElevation);
            if (_playerState.IsAlive && landing.HasValue && IsPlayerAdjacentTo(hole))
            {
                yield return FallPlayer(hole, "DROP");
                if (_playerState.IsAlive)
                    yield return ResolveEnemyPhase();
            }
        }

        private IEnumerator AnimateHoleDrop(GridPos hole, GridPos landing)
        {
            Vector3 start = _player.transform.position;
            Vector3 holeWorld = _grid.GridToWorld(hole);
            Vector3 landingWorld = _grid.GridToWorld(landing);
            Color original = _playerRenderer.color;

            float elapsed = 0f;
            const float hopDuration = 0.14f;
            while (elapsed < hopDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hopDuration);
                _player.transform.position = Vector3.Lerp(start, holeWorld, t) +
                                             Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.18f;
                yield return null;
            }

            elapsed = 0f;
            const float fallDuration = 0.34f;
            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                _player.transform.position = Vector3.Lerp(holeWorld, landingWorld, SmoothStep(t));
                _player.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, Mathf.Sin(t * Mathf.PI));
                _playerRenderer.color = new Color(original.r, original.g, original.b, Mathf.Lerp(1f, 0.35f, Mathf.Sin(t * Mathf.PI)));
                yield return null;
            }

            _player.transform.position = landingWorld;
            _player.transform.localScale = Vector3.one;
            _playerRenderer.color = original;
        }

        private IEnumerator AnimateDoorTransition(SpriteRenderer renderer, GridPos door, TileKind nextKind)
        {
            float duration = 0.11f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                renderer.transform.localScale = new Vector3(Mathf.Lerp(1f, 0.08f, t), 1f, 1f);
                yield return null;
            }

            _grid.Map.Set(door, nextKind);
            renderer.sprite = GetTileSprite(nextKind, door);
            renderer.sortingOrder = _grid.iso.SortingOrder(door, TileSortOffset(nextKind));

            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                renderer.transform.localScale = new Vector3(Mathf.Lerp(0.08f, 1f, t), 1f, 1f);
                yield return null;
            }
            renderer.transform.localScale = Vector3.one;
            yield return AnimateDoorInteractionFx(
                door,
                nextKind == TileKind.DoorOpen || nextKind == TileKind.SecretPassage);
        }

        private IEnumerator AnimateDoorInteractionFx(GridPos door, bool opening)
        {
            var effect = new GameObject(opening ? "Door Open Burst" : "Door Close Burst");
            effect.transform.SetParent(_visualRoot, false);
            effect.transform.position = _grid.GridToWorld(door) + Vector3.up * 0.42f;
            var renderer = effect.AddComponent<SpriteRenderer>();
            renderer.sprite = GetDoorInteractionSprite(opening);
            renderer.sortingOrder = OverlaySorting.Burst;

            float elapsed = 0f;
            const float duration = 0.16f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(0.45f, 1.25f, SmoothStep(t));
                effect.transform.localScale = new Vector3(scale, scale, 1f);
                Color color = renderer.color;
                color.a = 1f - t;
                renderer.color = color;
                yield return null;
            }

            Destroy(effect);
        }

        private IEnumerator MovePlayerPath(IReadOnlyList<GridPos> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                GridPos next = path[i];
                if (IsLivingEnemyAt(next))
                {
                    // 자동 이동 경로에 적이 들어오면 조용히 멈추지 않는다.
                    // 특히 계단/사다리를 탭했을 때 멈춘 이유를 이동 장치 고장으로 오해하기 쉽다.
                    bool blockerVisible =
                        viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(next);
                    FloatingText?.Show(_player.transform.position, "!", FloatingTextKind.Alert);
                    InteractionFeedback?.Invoke(
                        blockerVisible
                            ? "적이 길을 막아 이동을 멈췄다 — 적을 먼저 처치하라"
                            : "앞길이 막혀 이동을 멈췄다");
                    yield break;
                }

                // 스텝 전 스냅샷 — 이 스텝(내 이동+적 턴)으로 "새로" 보이게 된 것만 인터럽트한다.
                SnapshotTravelSight();
                int hpBeforeStep = _playerState.Hp;

                Vector3 start = _player.transform.position;
                Vector3 end = _grid.GridToWorld(next);
                ApplyPlayerVisualSorting(next);

                bool changesDungeonFloor = !_dungeon.Height.SameFloor(_playerState.Position, next);
                if (changesDungeonFloor)
                {
                    yield return AnimateFloorTransition(end);
                }
                else
                {
                    float elapsed = 0f;
                    while (elapsed < secondsPerStep)
                    {
                        elapsed += Time.deltaTime;
                        float t = Mathf.Clamp01(elapsed / secondsPerStep);
                        _player.transform.position = Vector3.LerpUnclamped(start, end, SmoothStep(t));
                        yield return null;
                    }
                }

                _player.transform.position = end;

                // 던전 층 전환 계단은 "입구 칸에 서기"와 "링크 이동"을 한 행동으로 묶는다.
                // 둘 사이에 적 턴/자동 이동 인터럽트가 끼면 계단 위에 남아 층이 안 바뀐 것처럼
                // 보이므로, 같은 층에서 입구를 밟은 즉시 반대편 출구까지 이동한다.
                bool enteredFromSameFloor =
                    _dungeon.Height.SameFloor(_playerState.Position, next);
                if (enteredFromSameFloor &&
                    VerticalTraversalRules.TryGetAutomaticFloorDestination(
                        _grid.Map, next, out GridPos floorDestination))
                {
                    InteractionFeedback?.Invoke(
                        $"{FloorLabel(_dungeon.Height.FloorIndex(floorDestination.elevation))}로 이동");
                    yield return AnimateFloorTransition(_grid.GridToWorld(floorDestination));

                    if (i + 1 < path.Count && path[i + 1] == floorDestination)
                        i++; // 경로 탐색이 넣은 링크 목적지는 이미 함께 소비했다.
                    next = floorDestination;
                    end = _grid.GridToWorld(next);
                }

                _playerPos = next;
                _playerState.MoveTo(next);
                PlayerPositionChanged?.Invoke();
                _playerSorting.x = next.x;
                _playerSorting.y = next.y;
                _playerSorting.elevation = next.elevation;
                _player.transform.position = end;
                ConfigureCamera(Camera.main);
                TryCollectItemAt(next);

                // 허브 포탈은 밟는 순간 목적지 선택을 연다. 던전 진입은 확인 버튼에서만 확정한다.
                if (hubMode && next == HubLayout.Portal)
                {
                    TryActivateHubPortal();
                    yield break;
                }

                // 약한 바닥은 밟는 순간 무너진다 — 낙하로 경로가 무효화된다. (GDD §5.3)
                if (_grid.Map.Get(next)?.kind == TileKind.WeakFloor)
                {
                    yield return CollapseUnderPlayer(next);
                    if (_playerState.IsAlive)
                        yield return ResolveEnemyPhase();
                    yield break;
                }

                int nextFloor = _dungeon.Height.FloorIndex(next.elevation);
                if (nextFloor != _activeFloorIndex)
                {
                    _activeFloorIndex = nextFloor;
                    _runTelemetry?.RecordFloorEntered(
                        GlobalFloorIndex(_activeFloorIndex), GlobalDepth(_activeFloorIndex));
                    AnnounceBossApproachIfNeeded();
                    UpdateInputFloorRange();
                    RefreshFloorVisibility();
                    PositionSelection(next);
                    ActiveFloorChanged?.Invoke(_activeFloorIndex);
                    Debug.Log($"[Dungeon] 층 이동: {FloorLabel(_activeFloorIndex)} / " +
                              $"층 내부 높이 {_dungeon.Height.LocalHeight(next.elevation)}");
                    _runSummary.RecordFloor(GlobalFloorIndex(_activeFloorIndex));
                    TryDeclareVictory();
                    if (_runSummary.Ended) yield break;
                    SaveCheckpoint();
                }
                else
                {
                    RefreshFloorVisibility();
                }

                yield return ResolveEnemyPhase();
                if (!_playerState.IsAlive)
                    yield break;

                if (i >= path.Count - 1) continue; // 마지막 스텝 뒤엔 멈출 이동이 없다

                if (_travelCancelRequested)
                {
                    InteractionFeedback?.Invoke("MOVE CANCELED");
                    yield break;
                }

                TravelInterrupt interrupt = TravelRules.Evaluate(
                    _travelVisibleEnemyIds,
                    EnemySightStates(),
                    AnyNewVisibleItem(),
                    _playerState.Hp < hpBeforeStep);
                if (interrupt != TravelInterrupt.None)
                {
                    FloatingText?.Show(_player.transform.position, "!", FloatingTextKind.Alert);
                    InteractionFeedback?.Invoke(interrupt switch
                    {
                        TravelInterrupt.PlayerDamaged => "INTERRUPTED — 피해를 입어 멈췄다",
                        TravelInterrupt.EnemySighted => "ENEMY SIGHTED — 적 발견!",
                        _ => "ITEM SIGHTED — 무언가 보인다"
                    });
                    yield break;
                }
            }
        }

        /// <summary>스텝 시작 전 시야 스냅샷: 보이는 살아있는 적 ID + 보이는 미수집 아이템 칸.</summary>
        private void SnapshotTravelSight()
        {
            _travelVisibleEnemyIds.Clear();
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State.IsAlive && _visibleTiles.Contains(enemy.State.Position))
                    _travelVisibleEnemyIds.Add(enemy.State.Id);
            }

            _travelVisibleItemTiles.Clear();
            foreach (ItemAgent item in _items)
            {
                if (!item.Collected && _visibleTiles.Contains(item.Spawn.Position))
                    _travelVisibleItemTiles.Add(item.Spawn.Position);
            }
        }

        private IEnumerable<(string, bool, bool)> EnemySightStates()
        {
            foreach (EnemyAgent enemy in _enemies)
                yield return (
                    enemy.State.Id,
                    _visibleTiles.Contains(enemy.State.Position),
                    enemy.State.IsAlive);
        }

        private bool AnyEnemyVisible()
        {
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State.IsAlive && _visibleTiles.Contains(enemy.State.Position))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 미탐색 칸 탭: 아는(탐색된) 타일 중 목표에 평면 거리로 가장 가까운 칸까지
        /// 아는 타일만 밟아 이동한다. (SPD의 미탐색 탭 관례)
        /// </summary>
        private void TryTravelTowardUnexplored(GridPos target)
        {
            static int PlanarDistance(GridPos a, GridPos b) =>
                Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

            var candidates = new List<GridPos>();
            foreach (GridPos pos in _exploredTiles)
            {
                if (_dungeon.Height.FloorIndex(pos.elevation) != _activeFloorIndex) continue;
                if (!_grid.Map.IsWalkable(pos) || pos == _playerPos) continue;
                if (IsLivingEnemyAt(pos)) continue;
                if (PlanarDistance(pos, target) >= PlanarDistance(_playerPos, target)) continue;
                candidates.Add(pos);
            }

            if (candidates.Count == 0)
            {
                InteractionFeedback?.Invoke("UNEXPLORED — 아는 길이 없다");
                return;
            }

            candidates.Sort((a, b) =>
            {
                int byTarget = PlanarDistance(a, target).CompareTo(PlanarDistance(b, target));
                return byTarget != 0
                    ? byTarget
                    : PlanarDistance(a, _playerPos).CompareTo(PlanarDistance(b, _playerPos));
            });

            // 최상위 후보 몇 개만 경로 검증 — 후보 전수 탐색은 탭마다 너무 비싸다.
            bool Unknown(GridPos pos) => !_exploredTiles.Contains(pos) && !_visibleTiles.Contains(pos);
            int attempts = Mathf.Min(8, candidates.Count);
            for (int i = 0; i < attempts; i++)
            {
                List<GridPos> path = GridPathfinder.FindPath(
                    _grid.Map, _playerPos, candidates[i], pos => Unknown(pos));
                if (path.Count < 2) continue;

                int allowedSteps = TravelRules.AllowedSteps(AnyEnemyVisible(), path.Count - 1);
                if (allowedSteps < path.Count - 1)
                    path.RemoveRange(allowedSteps + 1, path.Count - allowedSteps - 1);
                InteractionFeedback?.Invoke("미탐색 방향으로 이동...");
                StartPlayerAction(candidates[i], MovePlayerPath(path));
                return;
            }

            InteractionFeedback?.Invoke("UNEXPLORED — 아는 길이 없다");
        }

        private bool AnyNewVisibleItem()
        {
            foreach (ItemAgent item in _items)
            {
                if (!item.Collected &&
                    _visibleTiles.Contains(item.Spawn.Position) &&
                    !_travelVisibleItemTiles.Contains(item.Spawn.Position))
                    return true;
            }
            return false;
        }

        private IEnumerator AnimateFloorTransition(Vector3 destination)
        {
            Color original = _playerRenderer.color;
            _playerRenderer.color = new Color(original.r, original.g, original.b, 0.2f);
            yield return new WaitForSeconds(0.12f);
            _player.transform.position = destination;
            _playerRenderer.color = original;
            yield return new WaitForSeconds(0.12f);
        }
    }
}
