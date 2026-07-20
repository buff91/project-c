using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// IsoPrototypeDemo의 낙하·넉백·폭발 상호작용부. (M4 시그니처)
    /// 모든 낙하 트리거(구멍 진입·약한 바닥 붕괴·넉백)는 Core의 FallRules.TryFall 로
    /// 수렴하고, 여기서는 결과를 연출·뷰 상태에 반영만 한다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private bool _barrelExploded;

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

        // ── 플레이어 낙하 ──────────────────────────────────────────

        /// <summary>from 칸에서 플레이어 낙하를 처리하고 층 이동·연출·피해 표시까지 반영한다.</summary>
        private IEnumerator FallPlayer(GridPos from, string cause)
        {
            FallResult fall = FallRules.TryFall(
                _grid.Map, _dungeon.Height, _playerState, from, BottomElevation, AllCombatants());
            if (fall == null) yield break; // 무저갱 — 생성기가 없다고 보장하지만 방어

            int destinationFloor = _dungeon.Height.FloorIndex(fall.FinalPosition.elevation);
            InteractionFeedback?.Invoke($"{cause} → {FloorLabel(destinationFloor)}");
            yield return AnimateHoleDrop(from, fall.FinalPosition);

            _playerPos = fall.FinalPosition;
            _playerSorting.Pos = fall.FinalPosition;
            ApplyPlayerVisualSorting(fall.FinalPosition);
            _activeFloorIndex = destinationFloor;
            UpdateInputFloorRange();
            RefreshFloorVisibility();
            PositionSelection(fall.FinalPosition);
            ConfigureCamera(Camera.main);
            ActiveFloorChanged?.Invoke(_activeFloorIndex);
            PlayerPositionChanged?.Invoke();
            InteractionFeedback?.Invoke($"LANDED · {LocationLabel}");

            if (fall.Damage > 0)
            {
                InteractionFeedback?.Invoke($"FALL DAMAGE -{fall.Damage} HP");
                yield return ShowPlayerHit(fall.Damage, "Fall");
            }
            yield return ShowFallImpact(fall);
            if (_playerState.IsAlive)
                TryCollectItemAt(fall.FinalPosition);
        }

        /// <summary>밟거나 충격을 받은 약한 바닥이 무너지고 플레이어가 떨어진다. 턴 진행은 호출부가.</summary>
        private IEnumerator CollapseUnderPlayer(GridPos pos)
        {
            _grid.Map.Set(pos, TileKind.Hole);
            InteractionFeedback?.Invoke("WEAK FLOOR COLLAPSED!");
            Debug.Log($"[Fall] 플레이어 밑의 약한 바닥 붕괴 {pos}");
            yield return FallPlayer(pos, "COLLAPSE");
        }

        /// <summary>낙하 없는 1칸 밀림(넉백). 층은 그대로, 시야·카메라만 갱신.</summary>
        private IEnumerator ShiftPlayerTo(GridPos destination)
        {
            Vector3 start = _player.transform.position;
            _playerState.MoveTo(destination);
            _playerPos = destination;
            _playerSorting.Pos = destination;
            ApplyPlayerVisualSorting(destination);

            Vector3 end = _grid.GridToWorld(destination);
            float elapsed = 0f;
            const float duration = 0.08f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _player.transform.position = Vector3.Lerp(start, end, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            _player.transform.position = end;

            RefreshFloorVisibility();
            PositionSelection(destination);
            ConfigureCamera(Camera.main);
            PlayerPositionChanged?.Invoke();
            TryCollectItemAt(destination);
        }

        // ── 몬스터 낙하 ──────────────────────────────────────────

        private IEnumerator FallEnemy(EnemyAgent enemy, GridPos from, string cause)
        {
            FallResult fall = FallRules.TryFall(
                _grid.Map, _dungeon.Height, enemy.State, from, BottomElevation, AllCombatants());
            if (fall == null) yield break;

            InteractionFeedback?.Invoke($"{enemy.State.Id} FELL!");
            Debug.Log($"[Fall] {enemy.State.Id} {cause} 낙하 → {fall.FinalPosition} (-{fall.Damage} HP)");
            yield return ShowEnemyHit(enemy, fall.Damage, "Fall");
            ApplyEnemyVisuals(enemy); // 대개 다른 층으로 사라진다
            yield return ShowFallImpact(fall);
        }

        private IEnumerator CollapseUnderEnemy(EnemyAgent enemy, GridPos pos)
        {
            _grid.Map.Set(pos, TileKind.Hole);
            Debug.Log($"[Fall] {enemy.State.Id} 밑의 약한 바닥 붕괴 {pos}");
            yield return FallEnemy(enemy, pos, "붕괴");
        }

        /// <summary>착지점에 있던 상대(플레이어/몬스터)의 충돌 피해 연출.</summary>
        private IEnumerator ShowFallImpact(FallResult fall)
        {
            if (fall.CrushedOccupant == null) yield break;

            if (fall.CrushedOccupant == _playerState)
            {
                InteractionFeedback?.Invoke("CRUSHED FROM ABOVE!");
                yield return ShowPlayerHit(fall.Damage, "Crush");
                yield break;
            }

            EnemyAgent agent = FindAgentByState(fall.CrushedOccupant);
            if (agent != null)
            {
                yield return ShowEnemyHit(agent, fall.Damage, "Crush");
                ApplyEnemyVisuals(agent);
            }
        }

        // ── 폭발: 피해 → 넉백 → 폭발통 연쇄 ───────────────────────

        /// <summary>
        /// 폭발 한 번의 전체 처리. 넉백으로 구멍/허공에 밀리면 TryFall 로 이어지고,
        /// 반경 안의 폭발통은 유폭해 재귀적으로 폭발한다(요소 반응: 폭발+폭발통=연쇄).
        /// </summary>
        private IEnumerator ResolveExplosion(GridPos center, int damage)
        {
            yield return AnimateBlast(center);

            BombResult result = BombRules.Detonate(_grid.Map, center, AllCombatants(), damage);
            InteractionFeedback?.Invoke($"BOOM · {result.Damaged.Count} HIT");
            Debug.Log($"[Bomb] {center} 폭발: {result.Damaged.Count}명 피해, " +
                      $"약한 바닥 {result.CollapsedWeakFloors.Count}칸 붕괴");

            foreach (CombatantState damaged in result.Damaged)
            {
                if (damaged == _playerState)
                {
                    yield return ShowPlayerHit(damage, "Bomb");
                    continue;
                }
                EnemyAgent agent = FindAgentByState(damaged);
                if (agent != null) yield return ShowEnemyHit(agent, damage, "Bomb");
            }

            if (result.CollapsedWeakFloors.Count > 0)
                InteractionFeedback?.Invoke($"WEAK FLOOR COLLAPSED ×{result.CollapsedWeakFloors.Count}");

            // 넉백: 맞고 살아남은 전원을 중심 반대쪽으로 민다. 플레이어도 예외 없음. (GDD §5.3)
            foreach (CombatantState survivor in result.Damaged)
            {
                if (!survivor.IsAlive) continue;
                yield return KnockbackCombatant(center, survivor);
                if (!_playerState.IsAlive) yield break; // 넉백 낙하로 사망 시 중단
            }

            if (!_barrelExploded && _barrel != null && BombRules.InBlast(center, _barrelPos))
            {
                _barrelExploded = true;
                SetSpriteHierarchyVisible(_barrel, false);
                InteractionFeedback?.Invoke("BARREL CHAIN EXPLOSION!");
                Debug.Log($"[Bomb] 폭발통 유폭 {_barrelPos}");
                yield return ResolveExplosion(_barrelPos, damage);
            }

            RefreshFloorVisibility();
        }

        private IEnumerator KnockbackCombatant(GridPos center, CombatantState target)
        {
            KnockbackOutcome outcome = KnockbackRules.Resolve(
                _grid.Map, center, target.Position,
                pos => IsPositionOccupiedExcept(pos, target),
                out GridPos destination);
            if (outcome == KnockbackOutcome.None) yield break;

            if (target == _playerState)
            {
                if (outcome == KnockbackOutcome.Pushed)
                {
                    yield return ShiftPlayerTo(destination);
                    if (_grid.Map.Get(destination)?.kind == TileKind.WeakFloor)
                        yield return CollapseUnderPlayer(destination); // 충격 → 붕괴
                }
                else
                {
                    InteractionFeedback?.Invoke("KNOCKED INTO THE PIT!");
                    yield return FallPlayer(destination, "KNOCKBACK");
                }
                yield break;
            }

            EnemyAgent agent = FindAgentByState(target);
            if (agent == null) yield break;

            if (outcome == KnockbackOutcome.Pushed)
            {
                target.MoveTo(destination);
                ApplyEnemyVisuals(agent);
                if (_grid.Map.Get(destination)?.kind == TileKind.WeakFloor)
                    yield return CollapseUnderEnemy(agent, destination);
            }
            else
            {
                yield return FallEnemy(agent, destination, "넉백");
            }
        }

        // ── 폭발통 밀기 (오브젝트 상호작용) ─────────────────────────

        private IEnumerator ApproachAndPushBarrel(IReadOnlyList<GridPos> path)
        {
            _resolvingAction = true;
            yield return MovePlayerPath(path);

            if (_playerState.IsAlive && !_barrelExploded &&
                _playerPos.elevation == _barrelPos.elevation && _playerPos.ManhattanTo(_barrelPos) == 1)
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

            _resolvingAction = false;
            _moveRoutine = null;
        }
    }
}
