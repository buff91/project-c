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

            SyncPlayerView(fall.FinalPosition, floorChanged: true);
            InteractionFeedback?.Invoke($"LANDED · {LocationLabel}");

            if (fall.Damage > 0)
            {
                InteractionFeedback?.Invoke($"FALL DAMAGE -{fall.Damage} HP");
                yield return ShowPlayerHit(fall.Damage, "Fall");
            }
            yield return ShowFallImpact(fall);
            if (_playerState.IsAlive)
                TryCollectItemAt(fall.FinalPosition);

            // 낙하로 최심층에 닿아도 승리 — 단, 낙뎀 사망이 먼저면 패배가 유지된다.
            TryDeclareVictory();
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
                _runSummary.RecordFloor(GlobalFloorIndex(_activeFloorIndex));
                UpdateInputFloorRange();
                SaveCheckpoint();
            }
            RefreshFloorVisibility();
            PositionSelection(position);
            ConfigureCamera(Camera.main);
            if (floorChanged) ActiveFloorChanged?.Invoke(_activeFloorIndex);
            PlayerPositionChanged?.Invoke();
        }

        // ── 몬스터 낙하 ──────────────────────────────────────────

        private IEnumerator FallEnemy(EnemyAgent enemy, GridPos from, string cause)
        {
            bool witnessed = IsPositionVisibleToPlayer(from);
            FallResult fall = FallRules.TryFall(
                _grid.Map, _dungeon.Height, enemy.State, from, BottomElevation, AllCombatants());
            if (fall == null) yield break;

            if (witnessed)
                InteractionFeedback?.Invoke($"{RunSummary.FormatCause(enemy.State.Id)} FELL!");
            Debug.Log($"[Fall] {enemy.State.Id} {cause} 낙하 → {fall.FinalPosition} (-{fall.Damage} HP)");
            enemy.Brain?.Rehome(fall.FinalPosition); // 새 층에서 순찰하도록 홈 이동
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

        private const int StatusTurnsApplied = 2; // 폭발이 부여하는 화상/빙결 지속 턴

        /// <summary>
        /// 폭발 한 번의 전체 처리. 넉백으로 구멍/허공에 밀리면 TryFall 로 이어지고,
        /// 불 폭발(fiery)은 화상을 부여하며 반경 안의 폭발통을 유폭시킨다.
        /// 냉기 폭발은 빙결을 부여하고 폭발통은 건드리지 않는다.
        /// </summary>
        private IEnumerator ResolveExplosion(GridPos center, int damage, bool fiery = true)
        {
            yield return AnimateBlast(center, fiery);

            BombResult result = BombRules.Detonate(_grid.Map, center, AllCombatants(), damage);
            int visibleHitCount = 0;
            foreach (CombatantState damaged in result.Damaged)
            {
                if (damaged == _playerState)
                {
                    visibleHitCount++;
                    continue;
                }
                EnemyAgent visibleAgent = FindAgentByState(damaged);
                if (visibleAgent != null && IsEnemyVisibleToPlayer(visibleAgent))
                    visibleHitCount++;
            }
            InteractionFeedback?.Invoke(
                visibleHitCount > 0 ? $"BOOM · {visibleHitCount} HIT" : "BOOM");
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

            // 상태 부여: 불 폭발은 화상, 냉기 폭발은 빙결. (GDD §5.5)
            bool anyVisibleAffected = false;
            foreach (CombatantState survivor in result.Damaged)
            {
                if (!survivor.IsAlive) continue;
                survivor.Statuses.Apply(fiery ? StatusKind.Burn : StatusKind.Freeze, StatusTurnsApplied);
                EnemyAgent affectedAgent = FindAgentByState(survivor);
                if (affectedAgent != null) ApplyEnemyVisuals(affectedAgent); // 틴트 즉시 반영
                if (survivor == _playerState ||
                    affectedAgent != null && IsEnemyVisibleToPlayer(affectedAgent))
                    anyVisibleAffected = true;
            }
            if (anyVisibleAffected) InteractionFeedback?.Invoke(fiery ? "BURNING!" : "FROZEN!");

            // 요소 반응: 불 폭발이 기름 타일에 닿으면 발화 — 그 위의 전원이 불탄다. (GDD §5.5)
            if (fiery)
            {
                List<GridPos> ignited = OilRules.Ignite(_grid.Map, center);
                if (ignited.Count > 0)
                {
                    InteractionFeedback?.Invoke($"OIL IGNITED ×{ignited.Count}!");
                    Debug.Log($"[Oil] 기름 발화 {center}: {ignited.Count}칸");
                    foreach (CombatantState combatant in AllCombatants())
                    {
                        if (!combatant.IsAlive || !ignited.Contains(combatant.Position)) continue;
                        combatant.Statuses.Apply(StatusKind.Burn, StatusTurnsApplied);
                        EnemyAgent burning = FindAgentByState(combatant);
                        if (burning != null) ApplyEnemyVisuals(burning);
                    }
                }

                List<GridPos> dried = WaterRules.Evaporate(_grid.Map, center);
                if (dried.Count > 0) Debug.Log($"[Water] 증발 {center}: {dried.Count}칸");
            }
            else
            {
                // 요소 반응: 냉기가 웅덩이에 닿으면 이어진 웅덩이 전체로 결빙 전파. (GDD §5.5)
                List<GridPos> frozenTiles = WaterRules.ChainFreeze(_grid.Map, center);
                if (frozenTiles.Count > 0)
                {
                    InteractionFeedback?.Invoke($"PUDDLE FROZEN ×{frozenTiles.Count}!");
                    Debug.Log($"[Water] 웅덩이 결빙 {center}: {frozenTiles.Count}칸");
                    foreach (CombatantState combatant in AllCombatants())
                    {
                        if (!combatant.IsAlive || !frozenTiles.Contains(combatant.Position)) continue;
                        combatant.Statuses.Apply(StatusKind.Freeze, StatusTurnsApplied);
                        EnemyAgent chilled = FindAgentByState(combatant);
                        if (chilled != null) ApplyEnemyVisuals(chilled);
                    }
                }
            }

            // 넉백: 맞고 살아남은 전원을 중심 반대쪽으로 민다. 플레이어도 예외 없음. (GDD §5.3)
            foreach (CombatantState survivor in result.Damaged)
            {
                if (!survivor.IsAlive) continue;
                yield return KnockbackCombatant(center, survivor);
                if (!_playerState.IsAlive) break; // 사망 — 남은 넉백만 생략, 지형 갱신은 계속
            }

            if (_playerState.IsAlive &&
                fiery && !_barrelExploded && _barrel != null && BombRules.InBlast(center, _barrelPos))
            {
                _barrelExploded = true;
                SetSpriteHierarchyVisible(_barrel, false);
                InteractionFeedback?.Invoke("BARREL CHAIN EXPLOSION!");
                Debug.Log($"[Bomb] 폭발통 유폭 {_barrelPos}");
                yield return ResolveExplosion(_barrelPos, bombDamage, fiery: true);
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
