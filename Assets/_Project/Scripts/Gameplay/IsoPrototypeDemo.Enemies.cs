using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// IsoPrototypeDemo의 적 페이즈 처리부. (M2)
    /// 파이프라인: 활성화 판정 → (M4: 상태이상 틱 자리) → Brain.Decide → 의도 실행.
    /// 브레인은 행동 의도만 반환하고, 위치·타일·연출 변경은 전부 여기서 일어난다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private readonly List<EnemyAgent> _enemyTurnOrder = new List<EnemyAgent>();
        private bool _enemyOpenedDoor;

        /// <summary>활성 반경: 시야보다 약간 넓게 잡아 시야 밖에서도 접근을 준비한다. (GDD §5.7 컬링)</summary>
        private int MonsterActiveRadius => fieldOfViewRadius + 2;

        private IEnumerator ResolveEnemyPhase()
        {
            if (!_turns.TryBeginEnemyPhase()) yield break;
            _enemyOpenedDoor = false;

            // 플레이어와 가까운 순으로 순차 decide→execute.
            // 앞 몬스터의 이동이 뒤 몬스터의 점유 판정에 반영돼 겹침/콩가라인 잼이 없다.
            _enemyTurnOrder.Clear();
            _enemyTurnOrder.AddRange(_enemies);
            GridPos playerPos = _playerState.Position;
            _enemyTurnOrder.Sort((a, b) =>
                a.State.Position.ChebyshevTo(playerPos)
                    .CompareTo(b.State.Position.ChebyshevTo(playerPos)));

            foreach (EnemyAgent enemy in _enemyTurnOrder)
            {
                if (!_playerState.IsAlive) break;
                if (!enemy.State.IsAlive || enemy.Brain == null) continue;

                // 활성화/컬링: 반경 밖·다른 층은 사고 자체를 건너뛴다(휴면).
                if (!MonsterActivation.IsActive(
                        _dungeon.Height,
                        _playerState.Position,
                        enemy.State.Position,
                        MonsterActiveRadius))
                    continue;

                MonsterAction action = enemy.Brain.Decide(BuildBrainContext(enemy));
                yield return ExecuteEnemyAction(enemy, action);
            }

            // 몬스터가 문을 열었으면 플레이어 시야도 변한다 — 이때만 전체 갱신.
            if (_enemyOpenedDoor)
                RefreshFloorVisibility();

            _turns.TryCompleteEnemyPhase();
        }

        private MonsterBrainContext BuildBrainContext(EnemyAgent self)
        {
            return new MonsterBrainContext
            {
                Map = _grid.Map,
                Height = _dungeon.Height,
                Self = self.State,
                Player = _playerState,
                SeenByPlayer = pos => _visibleTiles.Contains(pos),
                IsOccupied = pos => IsOccupiedForEnemy(self, pos)
            };
        }

        private bool IsOccupiedForEnemy(EnemyAgent self, GridPos pos)
        {
            if (_playerState != null && _playerState.IsAlive && pos == _playerState.Position)
                return true;
            foreach (EnemyAgent other in _enemies)
            {
                if (other == self || !other.State.IsAlive) continue;
                if (other.State.Position == pos) return true;
            }
            return false;
        }

        private IEnumerator ExecuteEnemyAction(EnemyAgent enemy, MonsterAction action)
        {
            switch (action.Kind)
            {
                case MonsterActionKind.Attack:
                    if (CombatRules.TryMelee(enemy.State, _playerState, out int damage))
                        yield return ShowPlayerHit(damage, enemy.State.Id);
                    break;

                case MonsterActionKind.Step:
                    yield return MoveEnemyStep(enemy, action.Target);
                    break;

                case MonsterActionKind.OpenDoor:
                    TileData door = _grid.Map.Get(action.Target);
                    if (door != null && door.CanOpen)
                    {
                        yield return SetDoorState(action.Target, TileKind.DoorOpen);
                        _enemyOpenedDoor = true;
                        InteractionFeedback?.Invoke($"{enemy.State.Id} OPENED A DOOR!");
                        Debug.Log($"[Door] {enemy.State.Id} 가 {action.Target} 문을 열었다");
                    }
                    break;
            }
        }

        private IEnumerator MoveEnemyStep(EnemyAgent enemy, GridPos next)
        {
            // 의도와 실행 사이에 상태가 변했을 수 있으니 실행 시점에 재검증한다.
            if (!_grid.Map.IsWalkable(next) || IsOccupiedForEnemy(enemy, next)) yield break;

            bool animate = _visibleTiles.Contains(enemy.State.Position) || _visibleTiles.Contains(next);
            Vector3 start = enemy.Root != null ? enemy.Root.transform.position : Vector3.zero;

            enemy.State.MoveTo(next);
            ApplyEnemyVisuals(enemy);

            // 플레이어 시야 밖 이동은 연출 없이 즉시 배치 — 걸음마다 도는 적 페이즈가
            // 활성 몬스터 수만큼 느려지지 않게 한다.
            if (!animate || enemy.Root == null) yield break;

            Vector3 end = enemy.Root.transform.position;
            float duration = secondsPerStep * 0.75f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                enemy.Root.transform.position = Vector3.Lerp(start, end, SmoothStep(t));
                yield return null;
            }
            enemy.Root.transform.position = end;
        }

        /// <summary>몬스터 한 마리의 위치·정렬·가시성만 갱신하는 가벼운 경로. (전체 리빌드 금지)</summary>
        private void ApplyEnemyVisuals(EnemyAgent enemy)
        {
            if (enemy.Root == null) return;
            GridPos pos = enemy.State.Position;
            enemy.Root.transform.position = _grid.GridToWorld(pos);
            enemy.Renderer.sortingOrder = _grid.iso.SortingOrder(SortingAnchor(pos), 1);
            SetSpriteHierarchyVisible(
                enemy.Root,
                _dungeon.Height.FloorIndex(pos.elevation) == _activeFloorIndex &&
                (viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(pos)));
        }
    }
}
