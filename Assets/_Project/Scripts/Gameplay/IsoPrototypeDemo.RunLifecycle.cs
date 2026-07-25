using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public partial class IsoPrototypeDemo
    {

        /// <summary>이어하기 데이터의 HP·인벤토리·전적을 새로 만든 판에 덧입힌다.</summary>
        private void ApplyContinueData(RunSaveData data)
        {
            ApplyCarriedState(data, $"이어하기 — {FloorLabel(_activeFloorIndex)} 입구에서 재개");
            RefreshBossExitSeal();
            BossStateChanged?.Invoke();
            Debug.Log($"[Save] 이어하기: {StageLabel} {FloorLabel(_activeFloorIndex)}, " +
                      $"HP {_playerState.Hp}, 처치 {data.kills}");
        }

        /// <summary>이어하기와 던전 전환이 공유하는 상태 이월(HP·인벤토리·전적).</summary>
        private void ApplyCarriedState(RunSaveData data, string feedback)
        {
            int hp = Mathf.Clamp(data.hp, 1, _playerState.MaxHp);
            if (hp < _playerState.MaxHp)
                _playerState.TakeDamage(_playerState.MaxHp - hp);
            UpdateHealthBar(_playerHpFill, _playerState);
            PlayerHpChanged?.Invoke();

            if (data.potions > 0) _inventory.Add(ItemKind.Potion, data.potions);
            if (data.bombs > 0) _inventory.Add(ItemKind.Bomb, data.bombs);
            if (data.frostBombs > 0) _inventory.Add(ItemKind.FrostBomb, data.frostBombs);
            if (data.oilFlasks > 0) _inventory.Add(ItemKind.OilFlask, data.oilFlasks);
            if (data.knives > 0) _inventory.Add(ItemKind.ThrowingKnife, data.knives);
            if (data.scrolls > 0) _inventory.Add(ItemKind.RecallScroll, data.scrolls);
            if (data.coinPouches > 0) _inventory.Add(ItemKind.CoinPouch, data.coinPouches);
            if (data.gemstones > 0) _inventory.Add(ItemKind.Gemstone, data.gemstones);
            if (data.relics > 0) _inventory.Add(ItemKind.Relic, data.relics);
            if (data.herbs > 0) _inventory.Add(ItemKind.Herb, data.herbs);
            if (data.powders > 0) _inventory.Add(ItemKind.BlastPowder, data.powders);
            if (data.frostShards > 0) _inventory.Add(ItemKind.FrostShard, data.frostShards);
            InventoryChanged?.Invoke();

            _runSummary = new RunSummary(data.deepestFloorIndex, data.kills);
            _runSummary.RecordFloor(GlobalFloorIndex(_activeFloorIndex));
            if (data.telemetry != null)
            {
                _runTelemetry = data.telemetry;
                _runTelemetry.schemaVersion = RunTelemetry.CurrentSchemaVersion;
            }
            RestoreUsedRestSites(data.usedRestFloorIndices);
            InteractionFeedback?.Invoke(feedback);
        }

        /// <summary>층 도착 시점의 체크포인트 저장. 판이 끝났으면 저장하지 않는다.</summary>
        private void SaveCheckpoint()
        {
            if (!Application.isPlaying || hubMode || _runSummary.Ended ||
                _playerState == null || !_playerState.IsAlive)
                return;

            RunSaveStore.Save(new RunSaveData
            {
                heroId = _hero != null ? _hero.Id : null,
                dungeonId = DungeonSelection.Selected.Id,
                seed = dungeonSeed,
                roomSize = roomSize,
                floorCount = floorCount,
                elevationsPerFloor = elevationsPerFloor,
                stageCount = stageCount,
                stageIndex = _stageIndex,
                currentFloorIndex = _activeFloorIndex,
                bossDefeated = _bossDefeated,
                hp = _playerState.Hp,
                potions = PotionCount,
                bombs = BombCount,
                frostBombs = FrostBombCount,
                oilFlasks = ItemCount(ItemKind.OilFlask),
                knives = ItemCount(ItemKind.ThrowingKnife),
                scrolls = ItemCount(ItemKind.RecallScroll),
                coinPouches = ItemCount(ItemKind.CoinPouch),
                gemstones = ItemCount(ItemKind.Gemstone),
                relics = ItemCount(ItemKind.Relic),
                herbs = ItemCount(ItemKind.Herb),
                powders = ItemCount(ItemKind.BlastPowder),
                frostShards = ItemCount(ItemKind.FrostShard),
                kills = _runSummary.Kills,
                deepestFloorIndex = _runSummary.DeepestFloorIndex,
                usedRestFloorIndices = SnapshotUsedRestSites(),
                telemetry = _runTelemetry
            });
        }

        /// <summary>던전 선택 확인 — 허브에서 새 판을 시작한다.</summary>
        public void BeginSelectedDungeon()
        {
            if (!Application.isPlaying || !hubMode) return;
            RunSaveStore.Clear();
            RunSaveStore.ContinueRequested = false;
            InteractionFeedback?.Invoke($"{DungeonSelection.Selected.DisplayName}(으)로 출발");
            UnityEngine.SceneManagement.SceneManager.LoadScene(FrontEndFlow.DungeonScene);
        }

        /// <summary>던전 체인 좌표계: 스테이지 누적 깊이(몬스터 혼합용, 0부터 증가).</summary>
        private int GlobalDepth(int floorIndex) => (_stageIndex - 1) * floorCount - floorIndex;

        /// <summary>스테이지 누적 층 인덱스(기록/표시용, 아래로 갈수록 음수).</summary>
        private int GlobalFloorIndex(int floorIndex) => floorIndex - (_stageIndex - 1) * floorCount;

        /// <summary>최심층 도착은 보스와 봉인 출구를 안내할 뿐, 즉시 승리시키지 않는다.</summary>
        private void TryDeclareVictory()
        {
            if (hubMode || _runSummary.Ended || _playerState == null || !_playerState.IsAlive) return;
            if (_activeFloorIndex != _dungeon.BottomFloorIndex) return;

            InteractionFeedback?.Invoke(
                BossExitUnlocked
                    ? "최심층 출구의 봉인이 풀렸다 — 출구(▼)로 향하라"
                    : $"최심층 도달 — {BossName}를 쓰러뜨려 출구를 열어라");
            BossStateChanged?.Invoke();
        }

        private bool IsBottomExit(GridPos pos) =>
            _dungeon != null &&
            _activeFloorIndex == _dungeon.BottomFloorIndex &&
            _dungeon.TryGetFloor(_dungeon.BottomFloorIndex, out DungeonFloorInfo floor) &&
            floor.DownStairs.HasValue &&
            floor.DownStairs.Value == pos;

        private bool TryRequestExitChoice()
        {
            if (!IsBottomExit(_playerPos)) return false;
            if (!BossExitUnlocked)
            {
                InteractionFeedback?.Invoke($"{BossName}의 봉인이 출구를 막고 있다");
                return false;
            }

            InteractionFeedback?.Invoke(
                HasNextStage ? "다음 던전으로 향할 수 있다" : "정복한 던전을 떠날 시간이다");
            ExitChoiceRequested?.Invoke();
            return true;
        }

        /// <summary>출구 계단까지 걸어간 뒤 "정복/다음 던전 vs 생환" 선택지를 띄운다.</summary>
        private IEnumerator MoveAndAdvanceStage(IReadOnlyList<GridPos> path, GridPos exit)
        {
            yield return MovePlayerPath(path);
            if (_playerState.IsAlive && _playerPos == exit)
                TryRequestExitChoice();
        }

        /// <summary>출구 선택지 — 다음 던전으로. (HUD 버튼이 호출)</summary>
        public void ConfirmAdvanceStage()
        {
            if (!Application.isPlaying || _resolvingAction || _runSummary.Ended ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (!BossExitUnlocked) return;

            if (HasNextStage)
            {
                AdvanceToNextStage();
                return;
            }

            int victoryGold = BankInventoryToStash();
            RunSaveStore.Clear();
            _runSummary.EndInVictory(victoryGold);
            FinishRunTelemetry(RunTelemetryOutcome.Victory, "");
            InteractionFeedback?.Invoke("DUNGEON CONQUERED!");
            Debug.Log(
                $"[Run] {DungeonSelection.Selected.DisplayName} 정복 — " +
                $"{FloorLabel(GlobalFloorIndex(_activeFloorIndex))}, " +
                $"+{ItemCatalog.FormatGold(victoryGold)}");
            RunEnded?.Invoke(_runSummary);
        }

        /// <summary>출구 선택지 — 생환. 전리품 환산 + 소모품 창고 보관 후 판 종료.</summary>
        public void ExtractRun()
        {
            if (!Application.isPlaying || _resolvingAction || _runSummary.Ended ||
                _playerState == null || !_playerState.IsAlive)
                return;

            int gold = BankInventoryToStash();
            RunSaveStore.Clear();
            _runSummary.EndInExtraction(gold);
            FinishRunTelemetry(RunTelemetryOutcome.Extraction, "");
            InteractionFeedback?.Invoke($"생환 — +{ItemCatalog.FormatGold(gold)} 적립");
            Debug.Log(
                $"[Run] 생환: +{ItemCatalog.FormatGold(gold)}, " +
                $"최심층 {FloorLabel(_runSummary.DeepestFloorIndex)}");
            RunEnded?.Invoke(_runSummary);
        }

        private void FinishRunTelemetry(RunTelemetryOutcome outcome, string cause)
        {
            if (_runTelemetry == null || _runTelemetry.Ended) return;

            _runTelemetry.End(outcome, cause, System.DateTime.UtcNow);
            string path = RunTelemetryStore.Save(_runTelemetry);
            Debug.Log(
                string.IsNullOrEmpty(path)
                    ? $"[Telemetry] {outcome} · 개발 리포트 저장 생략"
                    : $"[Telemetry] {outcome} 리포트 저장: {path}");
        }

        /// <summary>
        /// 정산: 전리품은 골드로 환산, 소모품은 창고에 보관한다.
        /// 살아 나갈 때(생환/승리)만 불린다 — 사망은 전부 소실. (extraction 규칙)
        /// </summary>
        private int BankInventoryToStash()
        {
            MetaSaveData meta = MetaStore.LoadOrNew();
            int gold = 0;
            foreach (ItemKind kind in ItemCatalog.AllKinds)
            {
                int count = _inventory.Count(kind);
                if (count <= 0) continue;
                if (ItemCatalog.IsTreasure(kind)) gold += ItemCatalog.GoldValue(kind) * count;
                else meta.AddCount(kind, count);
            }
            meta.gold += gold;

            // 의뢰 정산: 무사 귀환한 계약의 완료분 보상을 지급한다 (활성 의뢰가 없으면 무동작).
            BountyClaimResult bounties = BountyRules.Settle(meta, _runTelemetry);
            MetaStore.Save(meta);
            _inventory.Clear();
            InventoryChanged?.Invoke();
            if (bounties.CompletedCount > 0)
                Debug.Log(
                    $"[Bounty] 의뢰 완료 {bounties.CompletedCount}건 · " +
                    $"+{ItemCatalog.FormatGold(bounties.TotalReward)}");
            return gold + bounties.TotalReward;
        }

        /// <summary>
        /// 다음 던전 진입: HP·인벤토리·전적을 들고 새 seed 던전을 생성한다.
        /// 던전 간 이동은 층 이동과 달리 씬 상태를 전부 리빌드한다.
        /// </summary>
        private void AdvanceToNextStage()
        {
            // 모닥불: 던전 사이에서 잃은 HP의 절반을 회복한다.
            // (밸런스 시뮬: 회복 0%면 완주 18%, 50%면 49% — 체인 소모전 보정)
            int restedHp = Mathf.Min(
                _playerState.MaxHp,
                _playerState.Hp + Mathf.CeilToInt((_playerState.MaxHp - _playerState.Hp) * 0.5f));

            var carry = new RunSaveData
            {
                heroId = _hero != null ? _hero.Id : null,
                hp = restedHp,
                potions = PotionCount,
                bombs = BombCount,
                frostBombs = FrostBombCount,
                oilFlasks = ItemCount(ItemKind.OilFlask),
                knives = ItemCount(ItemKind.ThrowingKnife),
                scrolls = ItemCount(ItemKind.RecallScroll),
                coinPouches = ItemCount(ItemKind.CoinPouch),
                gemstones = ItemCount(ItemKind.Gemstone),
                relics = ItemCount(ItemKind.Relic),
                herbs = ItemCount(ItemKind.Herb),
                powders = ItemCount(ItemKind.BlastPowder),
                frostShards = ItemCount(ItemKind.FrostShard),
                kills = _runSummary.Kills,
                deepestFloorIndex = _runSummary.DeepestFloorIndex,
                usedRestFloorIndices = SnapshotUsedRestSites(),
                telemetry = _runTelemetry
            };

            _stageIndex++;
            dungeonSeed = dungeonSeed * 31 + 7; // 결정론적 체인 — 같은 시작 seed 면 같은 여정
            previewStartDepth = 0;
            Debug.Log($"[Stage] {StageLabel} 진입 (seed {dungeonSeed})");

            BuildPrototype();
            ApplyCarriedState(carry, $"{StageLabel} 진입 — 모닥불에서 상처를 돌봤다");
            SaveCheckpoint();
        }
    }
}
