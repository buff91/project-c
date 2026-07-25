using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public partial class IsoPrototypeDemo
    {

        // ── 디버그 창 전용 API (에디터/개발빌드에서 DebugPanelController 가 호출) ──

        public bool DebugGodMode => _godMode;
        public int DebugSeed => dungeonSeed;
        public int DebugTurnNumber => _turns.TurnNumber;
        public string DebugTelemetrySummary =>
            _runTelemetry != null ? _runTelemetry.FormatCompactSummary() : "RUN TELEMETRY --";

        /// <summary>깊이 구간(Shallow/Mid/Deep/Boss)별 체류·피해 비교. 플레이 중 바로 읽는 용도.</summary>
        public string DebugTelemetryBandSummary =>
            _runTelemetry != null ? _runTelemetry.FormatBandSummary() : "구간 데이터 없음";

        public int DebugLivingEnemiesOnFloor()
        {
            int count = 0;
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State.IsAlive &&
                    _dungeon != null &&
                    _dungeon.Height.FloorIndex(enemy.State.Position.elevation) == _activeFloorIndex)
                    count++;
            }
            return count;
        }

        public void DebugToggleGodMode()
        {
            if (!Application.isPlaying) return;
            MarkTelemetryCheat();
            _godMode = !_godMode;
            InteractionFeedback?.Invoke(_godMode ? "CHEAT: GOD MODE ON" : "CHEAT: GOD MODE OFF");
        }

        public void DebugHealFull()
        {
            if (!Application.isPlaying || _playerState == null) return;
            MarkTelemetryCheat();
            _playerState.OverrideHpForDebug(_playerState.MaxHp);
            UpdateHealthBar(_playerHpFill, _playerState);
            PlayerHpChanged?.Invoke();
            InteractionFeedback?.Invoke("CHEAT: HP FULL");
        }

        public void DebugDamageSelf(int amount)
        {
            if (!Application.isPlaying || _playerState == null || !_playerState.IsAlive) return;
            MarkTelemetryCheat();
            int dealt = _playerState.TakeDamage(amount);
            StartCoroutine(ShowPlayerHit(dealt, "Debug"));
        }

        public void DebugApplyStatusToSelf(StatusKind kind)
        {
            if (!Application.isPlaying || _playerState == null || !_playerState.IsAlive) return;
            MarkTelemetryCheat();
            ApplyStatusWithPresentation(_playerState, kind, 3);
            InteractionFeedback?.Invoke(
                kind == StatusKind.Burn
                    ? "CHEAT: BURN FX"
                    : "CHEAT: FREEZE FX");
        }

        public void DebugGiveItem(ItemKind kind)
        {
            if (!Application.isPlaying) return;
            MarkTelemetryCheat();
            if (!_inventory.TryAdd(kind, out int count))
            {
                InteractionFeedback?.Invoke(
                    $"CHEAT: 백팩 공간 부족 · {ItemCatalog.DisplayName(kind)} " +
                    $"{BackpackRules.Footprint(kind)}칸 필요");
                return;
            }
            InventoryChanged?.Invoke();
            InteractionFeedback?.Invoke($"CHEAT: {ItemCatalog.ShortLabel(kind)} +1 (×{count})");
        }

        public void DebugKillAllOnFloor()
        {
            if (!Application.isPlaying || _dungeon == null) return;
            MarkTelemetryCheat();
            int killed = 0;
            foreach (EnemyAgent enemy in _enemies)
            {
                if (!enemy.State.IsAlive ||
                    _dungeon.Height.FloorIndex(enemy.State.Position.elevation) != _activeFloorIndex)
                    continue;
                enemy.State.TakeDamage(9999);
                UpdateHealthBar(enemy.HpFill, enemy.State);
                RecordEnemyDeath(enemy, IsEnemyVisibleToPlayer(enemy));
                ApplyEnemyVisuals(enemy);
                killed++;
            }
            InteractionFeedback?.Invoke($"CHEAT: 몬스터 {killed}마리 제거");
        }

        public void DebugDefeatBoss()
        {
            if (!Application.isPlaying || _boss == null || !_boss.State.IsAlive) return;
            MarkTelemetryCheat();
            _boss.State.TakeDamage(9999);
            UpdateHealthBar(_boss.HpFill, _boss.State);
            RecordEnemyDeath(_boss, IsEnemyVisibleToPlayer(_boss));
            ApplyEnemyVisuals(_boss);
        }

        public void DebugSaveCheckpoint() => SaveCheckpoint();

        public bool DebugRequestBossExit()
        {
            if (!Application.isPlaying || _dungeon == null ||
                !_dungeon.TryGetFloor(_dungeon.FinalFloorIndex, out DungeonFloorInfo floor) ||
                _dungeon.OnwardStairOf(floor) is null)
                return false;

            MarkTelemetryCheat();
            GridPos exit = _dungeon.OnwardStairOf(floor).Value;
            _playerState.MoveTo(exit);
            _player.transform.position = _grid.GridToWorld(exit);
            SyncPlayerView(exit, floorChanged: true);
            return TryRequestExitChoice();
        }

        public void DebugJumpFloor(int delta)
        {
            if (!Application.isPlaying || _dungeon == null || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (!_dungeon.TryGetFloor(_activeFloorIndex + delta, out DungeonFloorInfo floor))
            {
                InteractionFeedback?.Invoke("CHEAT: 그 방향에 층이 없다");
                return;
            }

            MarkTelemetryCheat();
            _playerState.MoveTo(floor.Entry);
            _player.transform.position = _grid.GridToWorld(floor.Entry);
            SyncPlayerView(floor.Entry, floorChanged: true);
            _runSummary.RecordFloor(
                GlobalFloorIndex(_activeFloorIndex),
                GlobalDepth(_activeFloorIndex));
            InteractionFeedback?.Invoke($"CHEAT: {FloorLabel(_activeFloorIndex)} 로 점프");
        }

        public void DebugJumpToSecretRoom()
        {
            if (!Application.isPlaying || hubMode || _dungeon == null || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;

            DungeonFloorInfo secretFloor = null;
            if (_dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo active) &&
                active.SecretDoor.HasValue &&
                SecretRoomRules.IsSecretDoor(_grid.Map.Get(active.SecretDoor.Value)))
            {
                secretFloor = active;
            }
            else
            {
                foreach (DungeonFloorInfo floor in _dungeon.Floors)
                {
                    if (!floor.SecretDoor.HasValue ||
                        !SecretRoomRules.IsSecretDoor(_grid.Map.Get(floor.SecretDoor.Value)))
                        continue;
                    secretFloor = floor;
                    break;
                }
            }

            if (secretFloor == null)
            {
                InteractionFeedback?.Invoke("CHEAT: 남은 비밀문이 없다");
                return;
            }

            GridPos door = secretFloor.SecretDoor.Value;
            GridPos destination = default;
            bool found = false;
            foreach (GridPos candidate in new[] { door.South, door.West, door.East, door.North })
            {
                if (!_grid.Map.IsWalkable(candidate) || IsLivingEnemyAt(candidate)) continue;
                destination = candidate;
                found = true;
                break;
            }
            if (!found)
            {
                InteractionFeedback?.Invoke("CHEAT: 비밀문 앞이 막혀 있다");
                return;
            }

            MarkTelemetryCheat();
            _playerState.MoveTo(destination);
            _player.transform.position = _grid.GridToWorld(destination);
            SyncPlayerView(
                destination,
                floorChanged: secretFloor.FloorIndex != _activeFloorIndex);
            InteractionFeedback?.Invoke(
                $"CHEAT: {FloorLabel(secretFloor.FloorIndex)} 수상한 벽 앞으로 이동");
        }

        public void DebugClearSave()
        {
            if (!Application.isPlaying) return;
            RunSaveStore.Clear();
            InteractionFeedback?.Invoke("CHEAT: 세이브 삭제");
        }

        public void DebugSaveTelemetryReport()
        {
            if (!Application.isPlaying || _runTelemetry == null) return;
            string path = RunTelemetryStore.Save(_runTelemetry);
            InteractionFeedback?.Invoke(
                string.IsNullOrEmpty(path)
                    ? "TELEMETRY: 개발 빌드에서만 저장 가능"
                    : $"TELEMETRY SAVED · {System.IO.Path.GetFileName(path)}");
            if (!string.IsNullOrEmpty(path))
                Debug.Log($"[Telemetry] 리포트 저장: {path}");
        }

        private void MarkTelemetryCheat()
        {
            if (_runTelemetry != null)
                _runTelemetry.cheatsUsed = true;
        }
    }
}
