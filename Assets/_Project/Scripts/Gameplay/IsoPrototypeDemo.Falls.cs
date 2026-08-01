using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// IsoPrototypeDemo의 낙하·넉백·폭발 연출부.
    /// <para>
    /// <b>연쇄의 순서는 여기에 없다.</b> 무엇이 어떤 차례로 일어나는지는 Core의
    /// <see cref="HazardSequence"/>가 정하고, 이 파일은 그 결과인 <see cref="HazardStep"/>을
    /// 받아 애니메이션·안내 문구·텔레메트리·뷰 동기화로 옮기기만 한다. 판정을 여기에 다시
    /// 심으면 두 벌이 갈라지고, 갈라진 쪽은 테스트가 없는 쪽이다.
    /// </para>
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private bool _barrelExploded;

        /// <summary>폭발이 부여하는 화상/빙결 지속 턴.</summary>
        private const int StatusTurnsApplied = 2;

        private List<CombatantState> AllCombatants()
        {
            var all = new List<CombatantState>(_enemies.Count + 1);
            if (_playerState != null) all.Add(_playerState);
            foreach (EnemyAgent enemy in _enemies) all.Add(enemy.State);
            return all;
        }

        private EnemyAgent FindAgentByState(CombatantState state)
        {
            foreach (EnemyAgent enemy in _enemies)
                if (enemy.State == state)
                    return enemy;
            return null;
        }

        private bool IsPositionOccupiedExcept(GridPos pos, CombatantState except)
        {
            if (_playerState != null && _playerState != except &&
                _playerState.IsAlive && _playerState.Position == pos)
                return true;
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State == except || !enemy.State.IsAlive) continue;
                if (enemy.State.Position == pos) return true;
            }
            return false;
        }

        private int BottomElevation => _dungeon.Height.Elevation(_dungeon.BottomFloorIndex);

        // ── Core 연쇄에 넘길 판 상태 ─────────────────────────────────

        /// <summary>
        /// 폭발통은 씬 오브젝트가 있을 때만 넘긴다 — 층에 통이 없으면 유폭 판정 자체가 없다.
        /// </summary>
        private HazardContext BuildHazardContext()
        {
            HazardBarrel barrel = _barrel == null
                ? null
                : new HazardBarrel
                {
                    Position = _barrelPos,
                    Exploded = _barrelExploded,
                    Damage = bombDamage,
                };
            return new HazardContext
            {
                Map = _grid.Map,
                Height = _dungeon.Height,
                Combatants = AllCombatants(),
                Player = _playerState,
                BottomElevation = BottomElevation,
                PlayerSafeFallHeight = _playerLoadout.SafeFallHeight,
                StatusTurns = StatusTurnsApplied,
                Barrel = barrel,
            };
        }

        // ── 진입점 (호출부 시그니처는 그대로) ─────────────────────────

        /// <summary>from 칸에서 플레이어 낙하를 처리하고 층 이동·연출·피해 표시까지 반영한다.</summary>
        private IEnumerator FallPlayer(GridPos from, string cause)
        {
            HazardContext context = BuildHazardContext();
            yield return PlayHazard(
                context,
                HazardSequence.Fall(context, _playerState, from, cause));
        }

        /// <summary>밟거나 충격을 받은 약한 바닥이 무너지고 플레이어가 떨어진다. 턴 진행은 호출부가.</summary>
        private IEnumerator CollapseUnderPlayer(GridPos pos)
        {
            HazardContext context = BuildHazardContext();
            yield return PlayHazard(
                context,
                HazardSequence.CollapseUnder(context, _playerState, pos));
        }

        private IEnumerator CollapseUnderEnemy(EnemyAgent enemy, GridPos pos)
        {
            HazardContext context = BuildHazardContext();
            yield return PlayHazard(
                context,
                HazardSequence.CollapseUnder(context, enemy.State, pos));
        }

        /// <summary>
        /// 폭발 한 번의 전체 처리. 넉백으로 구멍/허공에 밀리면 낙하로 이어지고,
        /// 불 폭발은 화상과 기름 발화·폭발통 유폭을, 냉기 폭발은 빙결과 웅덩이 결빙을 부른다.
        /// 순서는 <see cref="HazardSequence.Explode"/>가 소유한다.
        /// </summary>
        private IEnumerator ResolveExplosion(GridPos center, int damage, bool fiery = true)
        {
            HazardContext context = BuildHazardContext();
            yield return PlayHazard(
                context,
                HazardSequence.Explode(context, center, damage, fiery));
            RefreshFloorVisibility();
        }

        /// <summary>
        /// 폭발이 아닌 단일 넉백(둔기 타격). 구멍·약한 바닥으로 이어지는 처리는
        /// 폭발 넉백과 같은 규칙을 탄다.
        /// </summary>
        private IEnumerator KnockbackCombatant(GridPos center, CombatantState target)
        {
            HazardContext context = BuildHazardContext();
            yield return PlayHazard(
                context,
                HazardSequence.Knockback(context, center, target));
        }

        // ── 스텝 재생 ──────────────────────────────────────────────

        /// <summary>
        /// 연쇄를 한 스텝씩 꺼내 화면에 옮긴다.
        /// <para>
        /// <b>열거는 지연된다</b> — 한 스텝을 연출하는 동안 다음 판정은 아직 일어나지 않았다.
        /// 그래서 "폭발 애니메이션 → 피해 → 밀려남" 같은 끼어드는 순서가 유지된다.
        /// </para>
        /// </summary>
        private IEnumerator PlayHazard(HazardContext context, IEnumerable<HazardStep> steps)
        {
            // 폭발 상태 부여 묶음의 요약 문구는 그 묶음이 끝날 때 한 번 낸다.
            bool statusRunOpen = false;
            bool statusRunVisible = false;
            StatusKind statusRunKind = StatusKind.Burn;

            foreach (HazardStep step in steps)
            {
                bool blastStatus = step.Kind == HazardStepKind.StatusApplied &&
                                   step.Source == "Blast";
                if (statusRunOpen && !blastStatus)
                {
                    AnnounceStatusRun(statusRunVisible, statusRunKind);
                    statusRunOpen = false;
                    statusRunVisible = false;
                }

                switch (step.Kind)
                {
                    case HazardStepKind.Detonated:
                        yield return AnimateBlast(step.Origin, step.Fiery);
                        break;

                    case HazardStepKind.Damaged:
                        yield return PlayBlastDamage(step);
                        break;

                    case HazardStepKind.WeakFloorsCollapsed:
                        PlayCollapse(step);
                        break;

                    case HazardStepKind.SecretDoorsRevealed:
                        PlaySecretDoors(step);
                        break;

                    case HazardStepKind.StatusApplied:
                        PresentStatusApplied(step.Actor, step.Status, step.StatusResult);
                        if (blastStatus)
                        {
                            statusRunOpen = true;
                            statusRunKind = step.Status;
                            statusRunVisible |= IsCombatantVisibleToPlayer(step.Actor);
                        }
                        break;

                    case HazardStepKind.OilIgnited:
                        _runTelemetry?.RecordOilIgnition(step.Cells.Count);
                        InteractionFeedback?.Invoke($"OIL IGNITED ×{step.Cells.Count}!");
                        Debug.Log($"[Oil] 기름 발화 {step.Origin}: {step.Cells.Count}칸");
                        break;

                    case HazardStepKind.WaterEvaporated:
                        _runTelemetry?.RecordWaterEvaporation(step.Cells.Count);
                        if (step.Cells.Count > 0)
                            Debug.Log($"[Water] 증발 {step.Origin}: {step.Cells.Count}칸");
                        break;

                    case HazardStepKind.WaterFrozen:
                        _runTelemetry?.RecordWaterFreeze(step.Cells.Count);
                        InteractionFeedback?.Invoke($"PUDDLE FROZEN ×{step.Cells.Count}!");
                        Debug.Log($"[Water] 웅덩이 결빙 {step.Origin}: {step.Cells.Count}칸");
                        break;

                    case HazardStepKind.Knocked:
                        yield return PlayKnockback(step);
                        break;

                    case HazardStepKind.Fell:
                        yield return PlayFall(step);
                        break;

                    case HazardStepKind.Crushed:
                        yield return PlayCrush(step);
                        break;

                    case HazardStepKind.BarrelChained:
                        _barrelExploded = true;
                        SetSpriteHierarchyVisible(_barrel, false);
                        InteractionFeedback?.Invoke("BARREL CHAIN EXPLOSION!");
                        Debug.Log($"[Bomb] 폭발통 유폭 {step.Origin}");
                        break;
                }
            }

            if (statusRunOpen) AnnounceStatusRun(statusRunVisible, statusRunKind);
            if (context.Barrel != null) _barrelExploded = context.Barrel.Exploded;
        }

        private void AnnounceStatusRun(bool anyVisible, StatusKind kind)
        {
            if (!anyVisible) return;
            InteractionFeedback?.Invoke(kind == StatusKind.Burn ? "BURNING!" : "FROZEN!");
        }

        private bool IsCombatantVisibleToPlayer(CombatantState combatant)
        {
            if (combatant == _playerState) return true;
            EnemyAgent agent = FindAgentByState(combatant);
            return agent != null && IsEnemyVisibleToPlayer(agent);
        }

        private IEnumerator PlayBlastDamage(HazardStep step)
        {
            int visibleHitCount = 0;
            foreach (CombatantState damaged in step.Actors)
                if (IsCombatantVisibleToPlayer(damaged))
                    visibleHitCount++;
            InteractionFeedback?.Invoke(
                visibleHitCount > 0 ? $"BOOM · {visibleHitCount} HIT" : "BOOM");
            Debug.Log($"[Bomb] {step.Origin} 폭발: {step.Actors.Count}명 피해");

            foreach (CombatantState damaged in step.Actors)
            {
                if (damaged == _playerState)
                {
                    yield return ShowPlayerHit(step.Amount, step.Source);
                    continue;
                }
                EnemyAgent agent = FindAgentByState(damaged);
                if (agent != null) yield return ShowEnemyHit(agent, step.Amount, step.Source);
            }
        }

        private void PlayCollapse(HazardStep step)
        {
            if (step.Source == "COLLAPSE")
            {
                MarkStaticLightDirty(); // 새 개구부 = 새 광원
                if (step.Actor == _playerState)
                {
                    InteractionFeedback?.Invoke("WEAK FLOOR COLLAPSED!");
                    Debug.Log($"[Fall] 플레이어 밑의 약한 바닥 붕괴 {step.Origin}");
                }
                else
                {
                    Debug.Log($"[Fall] {step.Actor?.Id} 밑의 약한 바닥 붕괴 {step.Origin}");
                }
                return;
            }

            InteractionFeedback?.Invoke($"WEAK FLOOR COLLAPSED ×{step.Cells.Count}");
            Debug.Log($"[Bomb] 약한 바닥 {step.Cells.Count}칸 붕괴");
        }

        private void PlaySecretDoors(HazardStep step)
        {
            foreach (GridPos _ in step.Cells)
                _runTelemetry?.RecordSecretRoomFound(GlobalFloorIndex(_activeFloorIndex));
            InteractionFeedback?.Invoke(
                step.Cells.Count == 1
                    ? "폭발로 숨은 통로가 드러났다!"
                    : $"폭발로 숨은 통로 {step.Cells.Count}곳이 드러났다!");
            Debug.Log($"[SecretRoom] 폭발 발견: {string.Join(", ", step.Cells)}");
        }

        private IEnumerator PlayKnockback(HazardStep step)
        {
            if (step.Actor == _playerState)
            {
                yield return ShiftPlayerTo(step.Origin, step.Destination);
                yield break;
            }
            EnemyAgent agent = FindAgentByState(step.Actor);
            if (agent != null) ApplyEnemyVisuals(agent);
        }

        private IEnumerator PlayFall(HazardStep step)
        {
            if (step.Actor == _playerState)
            {
                _runTelemetry?.RecordFall(
                    player: true,
                    intentional: step.Source == "DROP",
                    fallenFloorCount: step.FloorsFallen);
                // 밀려서 떨어진 것은 스스로 뛰어내린 것과 다르게 읽혀야 한다.
                if (step.Source == "KNOCKBACK")
                    InteractionFeedback?.Invoke("KNOCKED INTO THE PIT!");
                int destinationFloor = _dungeon.Height.FloorIndex(step.Destination.elevation);
                InteractionFeedback?.Invoke($"{step.Source} → {FloorLabel(destinationFloor)}");
                yield return AnimateHoleDrop(step.Origin, step.Destination);

                SyncPlayerView(step.Destination, floorChanged: true);
                InteractionFeedback?.Invoke($"LANDED · {LocationLabel}");

                if (step.Amount > 0)
                {
                    InteractionFeedback?.Invoke($"FALL DAMAGE -{step.Amount} HP");
                    yield return ShowPlayerHit(step.Amount, "Fall");
                }
                if (_playerState.IsAlive)
                    TryCollectItemAt(step.Destination);

                // 낙하로 최심층에 닿아도 승리 — 단, 낙뎀 사망이 먼저면 패배가 유지된다.
                TryDeclareVictory();
                yield break;
            }

            EnemyAgent agent = FindAgentByState(step.Actor);
            if (agent == null) yield break;

            _runTelemetry?.RecordFall(
                player: false,
                intentional: false,
                fallenFloorCount: step.FloorsFallen);
            if (IsPositionVisibleToPlayer(step.Origin))
                InteractionFeedback?.Invoke(
                    $"{RunSummary.FormatCause(step.Actor.Id)} FELL!");
            Debug.Log(
                $"[Fall] {step.Actor.Id} {step.Source} 낙하 → {step.Destination} " +
                $"(-{step.Amount} HP)");
            agent.Brain?.Rehome(step.Destination); // 새 층에서 순찰하도록 홈 이동
            yield return ShowEnemyHit(agent, step.Amount, "Fall");
            ApplyEnemyVisuals(agent); // 대개 다른 층으로 사라진다
        }

        /// <summary>착지점에 있던 상대(플레이어/몬스터)의 충돌 피해 연출.</summary>
        private IEnumerator PlayCrush(HazardStep step)
        {
            if (step.Actor == _playerState)
            {
                InteractionFeedback?.Invoke("CRUSHED FROM ABOVE!");
                yield return ShowPlayerHit(step.Amount, "Crush");
                yield break;
            }

            EnemyAgent agent = FindAgentByState(step.Actor);
            if (agent != null)
            {
                yield return ShowEnemyHit(agent, step.Amount, "Crush");
                ApplyEnemyVisuals(agent);
            }
        }

        /// <summary>낙하 없는 1칸 밀림(넉백). 위치는 이미 Core가 옮겼고 여기서는 보간만 한다.</summary>
        private IEnumerator ShiftPlayerTo(GridPos from, GridPos destination)
        {
            Vector3 start = _player.transform.position;
            Vector3 end = _grid.GridToWorld(destination);
            float elapsed = 0f;
            const float duration = 0.08f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _player.transform.position = Vector3.Lerp(start, end, t);
                ApplyMovingActorVisualSorting(_playerRenderer, from, destination, t);
                yield return null;
            }
            _player.transform.position = end;

            SyncPlayerView(destination, floorChanged: false);
            TryCollectItemAt(destination);
        }

        /// <summary>이동류 처리 뒤 플레이어 관련 뷰 상태(정렬·시야·선택·카메라·이벤트)를 한 번에 동기화.</summary>
        private void SyncPlayerView(GridPos position, bool floorChanged)
        {
            _playerPos = position;
            _playerSorting.Pos = position;
            ApplyPlayerVisualSorting(position);
            if (floorChanged)
            {
                _activeFloorIndex = _dungeon.Height.FloorIndex(position.elevation);
                _runSummary.RecordFloor(
                GlobalFloorIndex(_activeFloorIndex),
                GlobalDepth(_activeFloorIndex));
                int globalFloor = GlobalFloorIndex(_activeFloorIndex);
                if (_runTelemetry != null && _runTelemetry.currentFloorIndex != globalFloor)
                    _runTelemetry.RecordFloorEntered(
                        globalFloor,
                        GlobalDepth(_activeFloorIndex),
                        FloorLabel(_activeFloorIndex));
                AnnounceBossApproachIfNeeded();
                UpdateInputFloorRange();
                SaveCheckpoint();
            }
            RefreshFloorVisibility();
            PositionSelection(position);
            ConfigureCamera(Camera.main);
            if (floorChanged) ActiveFloorChanged?.Invoke(_activeFloorIndex);
            PlayerPositionChanged?.Invoke();
        }

        // ── 폭발통 밀기 (오브젝트 상호작용) ─────────────────────────

        private IEnumerator ApproachAndPushBarrel(IReadOnlyList<GridPos> path)
        {
            yield return MovePlayerPath(path);

            if (_playerState.IsAlive && !_barrelExploded && IsPlayerAdjacentTo(_barrelPos))
            {
                KnockbackOutcome outcome = KnockbackRules.Resolve(
                    _grid.Map, _playerPos, _barrelPos,
                    pos => IsPositionOccupiedExcept(pos, null),
                    out GridPos destination);

                if (outcome == KnockbackOutcome.None)
                {
                    InteractionFeedback?.Invoke("BARREL WON'T BUDGE");
                }
                else
                {
                    if (_runTelemetry != null) _runTelemetry.barrelPushes++;
                    if (outcome == KnockbackOutcome.PushedIntoFall)
                    {
                        GridPos? landing = _grid.Map.FindLandingBelow(destination, BottomElevation);
                        if (landing.HasValue)
                        {
                            _barrelPos = landing.Value;
                            InteractionFeedback?.Invoke("BARREL FELL BELOW!");
                        }
                        else
                        {
                            _barrelPos = destination; // 무저갱 방어 — 제자리 유사 처리
                        }
                    }
                    else
                    {
                        _barrelPos = destination;
                        InteractionFeedback?.Invoke("BARREL PUSHED");
                    }
                    Debug.Log($"[Barrel] 폭발통 이동 → {_barrelPos}");
                    _barrel.transform.position = VisualPosition(_barrelPos);
                    _barrelRenderer.sortingOrder = _grid.iso.SortingOrder(_barrelPos, 1);
                    RefreshFloorVisibility();
                    yield return ResolveEnemyPhase(); // 밀기는 행동 1회
                }
            }
        }
    }
}
