using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public partial class IsoPrototypeDemo
    {

        /// <summary>
        /// 방향키 한 칸 이동 (PC). 탭 파이프라인을 재사용해 인접 적 공격·문 열기·
        /// 계단 오르내림이 그대로 성립한다. 조준 중엔 오발 방지를 위해 무시.
        /// </summary>
        private void HandleStepRequested(int dx, int dy)
        {
            if (!Application.isPlaying || _resolvingAction || _bombAiming ||
                _playerState == null || !_playerState.IsAlive || _runSummary.Ended)
                return;

            int x = _playerPos.x + dx;
            int y = _playerPos.y + dy;
            // 계단 승강 커버: 위 → 같은 높이 → 아래 순으로 존재하는 타일을 고른다.
            for (int deltaElevation = 1; deltaElevation >= -1; deltaElevation--)
            {
                var candidate = new GridPos(x, y, _playerPos.elevation + deltaElevation);
                if (!_grid.Map.Has(candidate)) continue;

                // 이동 입력은 이동이 우선: 열린 문은 닫기 토글이 아니라 그냥 지나간다.
                // (문을 닫고 싶으면 Space/탭으로.)
                TileData candidateTile = _grid.Map.Get(candidate);
                if (candidateTile != null && candidateTile.kind == TileKind.DoorOpen &&
                    !IsLivingEnemyAt(candidate))
                {
                    List<GridPos> stepPath = GridPathfinder.FindPath(_grid.Map, _playerPos, candidate);
                    if (stepPath.Count > 1)
                    {
                        StartPlayerAction(candidate, MovePlayerPath(stepPath));
                        return;
                    }
                }

                HandleTileTapped(candidate, tileExists: true);
                return;
            }
            HandleTileTapped(new GridPos(x, y, _playerPos.elevation), tileExists: false);
        }

        /// <summary>
        /// 인접 상호작용 (스페이스바/액션 휠): 적 공격 > 문 > 허브 오브젝트 > 폭발통.
        /// 대상이 없으면 아무 일도 하지 않는다 — 오입력이 턴을 낭비하지 않게.
        /// </summary>
        public void InteractAdjacent()
        {
            if (!Application.isPlaying || _resolvingAction || _bombAiming ||
                _playerState == null || !_playerState.IsAlive || _runSummary.Ended)
                return;

            if (TryActivateHubPortal() || TryActivateCurrentConnector())
                return;

            if (TryFindAdjacentInteraction(out GridPos target, out _))
                HandleTileTapped(target, tileExists: true);
        }

        private bool TryGetCurrentConnectorInteraction(out string label)
        {
            label = null;
            if (_grid == null || _dungeon == null) return false;

            TileKind? kind = _grid.Map.Get(_playerPos)?.kind;
            if (kind == TileKind.Ladder)
            {
                IReadOnlyList<GridPos> ladderLinks = _grid.Map.LinksFrom(_playerPos);
                if (ladderLinks.Count == 0)
                {
                    // 링크 없는 사다리 타일은 전원이 없는 엘리베이터다. 아무 말도 없으면
                    // 플레이어는 고장인지 조작 실수인지 구분할 수 없다.
                    if (!IsElevatorTile(_playerPos)) return false;
                    label = $"멈춘 엘리베이터 — {BossName}를 쓰러뜨리면 전원이 들어온다";
                    return true;
                }

                GridPos destination = ladderLinks[0];
                // 층을 건너는 사다리 링크는 엘리베이터 통로다 — 같은 층 사다리와 다르게 읽혀야
                // 한다. 몇 층을 되감는지 목적지 라벨로 알려 주지 않으면 후퇴 비용을 모른 채 누른다.
                if (_dungeon.Height.FloorIndex(destination.elevation) !=
                    _dungeon.Height.FloorIndex(_playerPos.elevation))
                {
                    label = $"통로로 내려가기 → {FloorLabel(_dungeon.Height.FloorIndex(destination.elevation))}";
                    return true;
                }

                label = destination.elevation > _playerPos.elevation
                    ? "사다리 오르기"
                    : "사다리 내려가기";
                return true;
            }

            // 던전 출구는 **링크 없는 진출 계단**이다. 종류(StairsUp/Down)는 공간 이름이라
            // 방향을 타므로, 여기서 종류로 분기하면 상승 던전에서 출구를 못 밟는다.
            if (kind == TileKind.StairsUp || kind == TileKind.StairsDown)
            {
                bool dungeonExit = IsBottomExit(_playerPos);
                bool hasDestination = _grid.Map.LinksFrom(_playerPos).Count > 0 || dungeonExit;
                if (!hasDestination) return false;

                label = dungeonExit
                    ? !BossExitUnlocked
                        ? "출구 봉인됨 — 보스를 처치하라"
                        : HasNextStage ? "다음 던전으로" : "던전 정복"
                    : kind == TileKind.StairsUp
                        ? $"{AboveFloorLabel}로 이동"
                        : $"{BelowFloorLabel}로 이동";
                return true;
            }

            return false;
        }

        private bool TryActivateHubPortal()
        {
            if (!hubMode || _playerPos != HubLayout.Portal) return false;
            InteractionFeedback?.Invoke("포탈 활성화 — 목적지를 선택하세요");
            HubInteractionRequested?.Invoke("dungeon-select");
            return true;
        }

        private bool TryActivateCurrentConnector()
        {
            if (!TryGetCurrentConnectorInteraction(out string label)) return false;

            TileKind kind = _grid.Map.Get(_playerPos).kind;
            IReadOnlyList<GridPos> links = _grid.Map.LinksFrom(_playerPos);
            if (links.Count > 0)
            {
                var path = new List<GridPos> { _playerPos, links[0] };
                InteractionFeedback?.Invoke(label);
                StartPlayerAction(links[0], MovePlayerPath(path));
                return true;
            }

            if ((kind == TileKind.StairsUp || kind == TileKind.StairsDown) &&
                IsBottomExit(_playerPos))
            {
                if (!BossExitUnlocked)
                {
                    InteractionFeedback?.Invoke($"{BossName}의 봉인이 출구를 막고 있다");
                    return true;
                }

                var path = new List<GridPos> { _playerPos };
                InteractionFeedback?.Invoke(label);
                StartPlayerAction(_playerPos, MoveAndAdvanceStage(path, _playerPos));
                return true;
            }

            return false;
        }

        /// <summary>인접 상호작용 대상 탐색. 액션 휠 라벨에도 쓴다.</summary>
        public bool TryFindAdjacentInteraction(out GridPos target, out string label)
        {
            target = default;
            label = null;
            if (_playerState == null || _grid == null || _dungeon == null) return false;

            GridPos player = _playerPos;
            foreach (GridPos candidate in new[] { player.North, player.East, player.South, player.West })
            {
                EnemyAgent enemy = FindLivingEnemyAt(candidate);
                if (enemy != null && (viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(candidate)))
                {
                    target = candidate;
                    label = "공격";
                    return true;
                }
            }

            foreach (GridPos candidate in new[] { player.North, player.East, player.South, player.West })
            {
                TileData tile = _grid.Map.Get(candidate);
                if (SecretRoomRules.IsSecretDoor(tile))
                {
                    target = candidate;
                    label = "수상한 벽 조사";
                    return true;
                }
                if (tile != null && (tile.CanOpen || tile.CanClose))
                {
                    target = candidate;
                    label = tile.CanOpen ? "문 열기" : "문 닫기";
                    return true;
                }
                if (!hubMode && IsRescueNpcAt(candidate))
                {
                    target = candidate;
                    label = "동료 구출";
                    return true;
                }
                if (hubMode && _hubInteractables.TryGetValue(candidate, out string hubId))
                {
                    target = candidate;
                    label = hubId == "merchant" ? "상인"
                        : hubId == "stash" ? "창고"
                        : hubId == "smith" ? "대장간"
                        : hubId == "bounty" ? "의뢰 게시판"
                        : hubId == "codex" ? "기록실"
                        : "영웅";
                    return true;
                }
                if (!hubMode && TryGetRestSiteAt(candidate, out RestSiteAgent restSite))
                {
                    target = candidate;
                    label = IsRestSiteUsed(restSite)
                        ? "휴식 완료"
                        : DungeonRestRules.HealingAmount(_playerState.Hp, _playerState.MaxHp) > 0
                            ? "휴식"
                            : "휴식 불필요";
                    return true;
                }
                if (!hubMode && !_barrelExploded && candidate == _barrelPos)
                {
                    target = candidate;
                    label = "밀기";
                    return true;
                }
            }
            return false;
        }

        /// <summary>대기(턴 스킵): 제자리에서 행동 1회를 소비하고 적 턴만 돌린다.</summary>
        public void WaitTurn()
        {
            if (!Application.isPlaying || _resolvingAction || _bombAiming ||
                _playerState == null || !_playerState.IsAlive || _runSummary.Ended)
                return;

            if (_runTelemetry != null) _runTelemetry.waitActions++;
            InteractionFeedback?.Invoke("대기 — 주변을 살핀다");
            _moveRoutine = StartCoroutine(RunPlayerAction(ResolveEnemyPhase()));
        }

        /// <summary>게임 포기: 소지품을 전부 잃고(창고는 유지) 허브로 돌아간다.</summary>
        public void AbandonRun()
        {
            if (!Application.isPlaying || hubMode) return;
            FinishRunTelemetry(RunTelemetryOutcome.Abandoned, "Abandoned");
            RunSaveStore.Clear();
            LoseCarriedEquipment();
            Debug.Log("[Run] 게임 포기 — 소지품·반입 장비 소실, 허브 복귀");
            UnityEngine.SceneManagement.SceneManager.LoadScene(FrontEndFlow.HubScene);
        }

        private void HandleTileTapped(GridPos target, bool tileExists)
        {
            if (!Application.isPlaying || _playerState == null || !_playerState.IsAlive ||
                _runSummary.Ended)
                return;

            // 화면의 검은 여백(void)은 미탐색 타일이 아니다. 이 검사를 FOV/자동 이동보다
            // 먼저 해야 맵 밖 탭이 가까운 탐색 경계로 걷는 명령으로 바뀌지 않는다.
            // IsoTapInput에서도 차단하지만 키보드/테스트 등 직접 호출 경로를 위해 재검증한다.
            if (!tileExists || !WorldInputRules.IsMapTile(_grid.Map, target))
                return;

            // 자동 이동 중 재탭 = 취소 요청. 다음 스텝 경계에서 멈춘다.
            if (_resolvingAction)
            {
                _travelCancelRequested = true;
                return;
            }

            if (viewMode == DungeonViewMode.Play &&
                !_visibleTiles.Contains(target) &&
                !_exploredTiles.Contains(target) &&
                !_verticalPreviewTiles.Contains(target))
            {
                TryTravelTowardUnexplored(target);
                return;
            }

            if (_bombAiming)
            {
                HandleBombAimTap(target);
                return;
            }

            // 사다리/층 전환 타일 위에서는 자기 자신 탭이 곧 사용이다.
            // 그 외 타일에서만 액션 휠을 연다.
            if (target == _playerPos)
            {
                if (TryActivateHubPortal() || TryActivateCurrentConnector())
                    return;
                PlayerTapped?.Invoke();
                return;
            }

            // 허브: NPC/오브젝트 탭 → 옆까지 걸어가 상호작용.
            if (hubMode && _hubInteractables.TryGetValue(target, out string hubId))
            {
                if (TryFindApproach(target, out List<GridPos> hubPath))
                    StartPlayerAction(target, ApproachAndInteract(hubPath, target, hubId));
                return;
            }

            // 적 판정을 문/구멍보다 먼저: 열린 문 위에 선 적을 탭하면 공격이지
            // 문 토글이 아니다. 시야 밖(explored 기억)의 적은 이동 탭으로만 취급.
            EnemyAgent tappedEnemy = FindLivingEnemyAt(target);
            if (tappedEnemy != null &&
                (viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(target)))
            {
                if (combatMode == CombatActionMode.Ranged)
                    StartPlayerAction(target, RangedAttack(tappedEnemy));
                // 긴 사거리 근접 장비(창)는 이미 닿으면 걸어 붙지 않고 그 자리에서 찌른다.
                else if (CombatRules.CanMelee(
                             _grid.Map, _playerState, tappedEnemy.State, _playerLoadout.MeleeReach))
                    StartPlayerAction(
                        target, ApproachAndAttack(new List<GridPos> { _playerPos }, tappedEnemy));
                else if (TryFindApproach(tappedEnemy.State.Position, out List<GridPos> attackPath))
                    StartPlayerAction(target, ApproachAndAttack(attackPath, tappedEnemy));
                return;
            }

            // 갇힌 동료 탭 → 옆까지 걸어가 구출. 적 판정 뒤에 두어 동료 위에 겹친 적이
            // 있으면 전투가 먼저 걸리게 한다.
            if (IsRescueNpcAt(target))
            {
                if (TryFindApproach(target, out List<GridPos> rescuePath))
                    StartPlayerAction(target, ApproachAndRescue(rescuePath, target));
                return;
            }

            TileData targetTile = _grid.Map.Get(target);
            if (SecretRoomRules.IsSecretDoor(targetTile))
            {
                if (TryFindApproach(target, out List<GridPos> secretPath))
                    StartPlayerAction(target, ApproachAndRevealSecretDoor(secretPath, target));
                return;
            }

            if (targetTile != null && targetTile.kind == TileKind.Hole)
            {
                if (TryFindApproach(target, out List<GridPos> dropPath))
                    StartPlayerAction(target, ApproachAndDrop(dropPath, target));
                return;
            }

            if (targetTile != null && (targetTile.CanOpen || targetTile.CanClose))
            {
                if (TryFindApproach(target, out List<GridPos> doorPath))
                    StartPlayerAction(target, ApproachAndToggleDoor(doorPath, target));
                return;
            }

            if (!hubMode && TryGetExtractionPointAt(target, out ExtractionAgent extraction))
            {
                if (TryFindApproach(target, out List<GridPos> extractionPath))
                    StartPlayerAction(
                        target, ApproachAndOfferExtraction(extractionPath, extraction));
                return;
            }

            if (!hubMode && TryGetRestSiteAt(target, out RestSiteAgent restSite))
            {
                if (IsRestSiteUsed(restSite))
                {
                    InteractionFeedback?.Invoke("이 휴식처는 이미 식었다");
                    return;
                }
                if (DungeonRestRules.HealingAmount(_playerState.Hp, _playerState.MaxHp) <= 0)
                {
                    InteractionFeedback?.Invoke("지금은 쉴 필요가 없다");
                    return;
                }
                if (TryFindApproach(target, out List<GridPos> restPath))
                    StartPlayerAction(target, ApproachAndRest(restPath, restSite));
                return;
            }

            // 폭발통 밀기 (오브젝트 상호작용). 인접까지 접근한 뒤 민다.
            if (!_barrelExploded && target == _barrelPos &&
                _dungeon.Height.FloorIndex(target.elevation) == _activeFloorIndex)
            {
                if (TryFindApproach(_barrelPos, out List<GridPos> pushPath))
                    StartPlayerAction(target, ApproachAndPushBarrel(pushPath));
                return;
            }

            if (!_grid.Map.IsWalkable(target))
            {
                // 조용히 무시하면 "보이는 것과 갈 수 있는 곳"이 헷갈린다 — 즉시 알려준다.
                InteractionFeedback?.Invoke(
                    _grid.Map.Get(target)?.kind == TileKind.Wall
                        ? "벽이다 — 지나갈 수 없다"
                        : "갈 수 없는 곳");
                return;
            }

            // 다른 층의 탐색된 칸도 목적지로 허용 — 경로 탐색이 계단 링크를 자동 경유한다.
            List<GridPos> path = GridPathfinder.FindPath(_grid.Map, _playerPos, target);
            if (path.Count == 0)
            {
                if (_dungeon.Height.FloorIndex(target.elevation) != _activeFloorIndex)
                    InteractionFeedback?.Invoke("그 층으로 가는 길을 아직 모른다");
                return;
            }

            // 적이 시야에 있는 동안엔 탭당 1스텝만 — 카이팅/오토무브 남용 방지. (SPD 관례)
            int allowedSteps = TravelRules.AllowedSteps(AnyEnemyVisible(), path.Count - 1);
            if (allowedSteps < path.Count - 1)
                path.RemoveRange(allowedSteps + 1, path.Count - allowedSteps - 1);

            // 층 전환 계단만 입구 도착과 링크 이동을 한 행동으로 묶는다.
            // 사다리는 첫 입력에 발판까지 이동해 부착하고, 그 위에서 다시 탭/Space 해야 오른다.
            // 자동 발동 종류를 여기서 다시 열거하지 않고 VerticalTraversalRules를 그대로 쓴다.
            bool automaticallyTraversesLink =
                VerticalTraversalRules.TryGetAutomaticFloorDestination(
                    _grid.Map, target, out GridPos automaticDestination);
            bool arrivedThroughSameLink = false;
            if (path.Count >= 2)
            {
                GridPos previous = path[path.Count - 2];
                foreach (GridPos linked in _grid.Map.LinksFrom(target))
                {
                    if (linked != previous) continue;
                    arrivedThroughSameLink = true;
                    break;
                }
            }
            if (automaticallyTraversesLink &&
                !arrivedThroughSameLink &&
                path[path.Count - 1] == target)
            {
                path.Add(automaticDestination);
            }
            else if (!automaticallyTraversesLink &&
                     (targetTile.kind == TileKind.StairsUp ||
                      targetTile.kind == TileKind.StairsDown) &&
                     IsBottomExit(target))
            {
                // 링크 없는 진출 계단 = 보스 봉인 출구. 상승 던전에서는 이것이 상행이다.
                StartPlayerAction(target, MoveAndAdvanceStage(path, target));
                return;
            }

            StartPlayerAction(target, MovePlayerPath(path));
        }

        private void HandleBombAimTap(GridPos target)
        {
            // 단검은 타일이 아니라 적을 조준한다.
            if (_bombAimKind == ItemKind.ThrowingKnife)
            {
                EnemyAgent knifeTarget = FindLivingEnemyAt(target);
                if (knifeTarget == null || !_visibleTiles.Contains(target))
                {
                    InteractionFeedback?.Invoke("KNIFE: 보이는 적을 탭해라");
                    return;
                }
                StartPlayerAction(target, ThrowKnife(knifeTarget));
                return;
            }

            if (!BombRules.CanThrow(_grid.Map, _playerPos, target, bombThrowRange))
            {
                bool blocked = !CombatRules.HasLineOfSight(_grid.Map, _playerPos, target);
                InteractionFeedback?.Invoke(blocked
                    ? "THROW BLOCKED"
                    : $"OUT OF THROW RANGE · MAX {bombThrowRange}");
                return;
            }

            if (_bombAimKind == ItemKind.OilFlask)
            {
                StartPlayerAction(target, ThrowOil(target));
                return;
            }

            StartPlayerAction(target, ThrowBomb(target, _bombAimKind));
        }
    }
}
