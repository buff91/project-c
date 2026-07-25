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
            // 이미 보스를 잡은 세이브라면 전원도 들어온 상태여야 한다 —
            // 링크는 맵에 저장되지 않고 매번 seed 로 재생성되므로 여기서 다시 넣는다.
            PowerElevatorIfUnlocked();
            BossStateChanged?.Invoke();
            Debug.Log($"[Save] 이어하기: {StageLabel} {FloorLabel(_activeFloorIndex)}, " +
                      $"HP {_playerState.Hp}, 처치 {data.kills}");
        }

        /// <summary>
        /// 주운 장비를 빈 슬롯에 바로 낀다(백팩에서 슬롯으로 옮긴다). 슬롯이 차 있으면
        /// 백팩에 그대로 두고 false — 살아 나와야 창고로 들어간다. 낀 장비는 반입 장비와
        /// 같은 운명이다(죽으면 잃는다).
        /// </summary>
        private bool TryAutoEquipPickedUp(ItemKind kind)
        {
            EquipmentDefinition definition = EquipmentCatalog.ForItem(kind);
            if (definition == null) return false;

            bool weaponSlot = definition.Slot == EquipmentSlot.Weapon;
            string current = weaponSlot ? _carriedWeaponId : _carriedGearId;
            if (!EquipmentRules.ShouldAutoEquip(current))
            {
                InteractionFeedback?.Invoke(
                    $"{definition.DisplayName} 획득 — 슬롯이 차 있어 백팩에 넣었다");
                return true;
            }

            if (!_inventory.TryUse(kind)) return false; // 방금 넣었으므로 실패할 일은 없다
            if (weaponSlot) _carriedWeaponId = definition.Id;
            else _carriedGearId = definition.Id;
            _playerLoadout = EquipmentRules.LoadoutFor(_carriedWeaponId, _carriedGearId);
            InventoryChanged?.Invoke();

            InteractionFeedback?.Invoke($"{definition.DisplayName} 장착 — {definition.Description}");
            Debug.Log($"[Equip] 주운 장비 장착: {definition.Id}");
            return true;
        }

        /// <summary>
        /// 사망·포기: 반입한 장비를 잃는다. 창고에서 이미 꺼냈으므로 되돌리지 않고 슬롯만 비운다 —
        /// 창고에 남겨둔 예비 장비는 안전하다(익스트랙션: 들고 나간 것만 위험하다).
        /// </summary>
        private void LoseCarriedEquipment()
        {
            if (string.IsNullOrEmpty(_carriedWeaponId) && string.IsNullOrEmpty(_carriedGearId))
                return;

            MetaSaveData meta = MetaStore.LoadOrNew();
            ForgeRules.LoseExpeditionEquipment(meta, _carriedWeaponId, _carriedGearId);
            MetaStore.Save(meta);
            Debug.Log($"[Run] 반입 장비 소실: {_carriedWeaponId} / {_carriedGearId}");
            _carriedWeaponId = "";
            _carriedGearId = "";
            _playerLoadout = CombatLoadout.Unarmed;
        }

        /// <summary>이어하기와 던전 전환이 공유하는 상태 이월(HP·인벤토리·전적).</summary>
        private void ApplyCarriedState(RunSaveData data, string feedback)
        {
            int hp = Mathf.Clamp(data.hp, 1, _playerState.MaxHp);
            if (hp < _playerState.MaxHp)
                _playerState.TakeDamage(_playerState.MaxHp - hp);
            UpdateHealthBar(_playerHpFill, _playerState);
            PlayerHpChanged?.Invoke();

            data.AddItemsTo(_inventory);
            InventoryChanged?.Invoke();

            // 반입 장비는 인벤토리와 같은 이월 경로를 탄다(이어하기·던전 체인 공용).
            _carriedWeaponId = data.carriedWeaponId ?? "";
            _carriedGearId = data.carriedGearId ?? "";
            _playerLoadout = EquipmentRules.LoadoutFor(_carriedWeaponId, _carriedGearId);

            // 배고픔도 이월된다 — 모닥불에서 쉬어도 배는 채워지지 않는다.
            _hunger = data.hunger ?? new HungerState();
            _lastHungerStage = _hunger.Stage;

            _runSummary = new RunSummary(
                data.deepestFloorIndex,
                data.kills,
                data.deepestProgressIndex);
            _runSummary.RecordFloor(
                GlobalFloorIndex(_activeFloorIndex),
                GlobalDepth(_activeFloorIndex));
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

            var data = new RunSaveData
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
                currentProgressIndex =
                    _dungeon != null ? _dungeon.ProgressIndexFor(_activeFloorIndex) : 0,
                bossDefeated = _bossDefeated,
                hp = _playerState.Hp,
                kills = _runSummary.Kills,
                deepestFloorIndex = _runSummary.DeepestFloorIndex,
                deepestProgressIndex = _runSummary.FurthestProgressIndex,
                usedRestFloorIndices = SnapshotUsedRestSites(),
                carriedWeaponId = _carriedWeaponId,
                carriedGearId = _carriedGearId,
                hunger = _hunger.Clone(),
                telemetry = _runTelemetry
            };
            data.WriteItems(_inventory);
            RunSaveStore.Save(data);
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

        /// <summary>
        /// 던전 체인 좌표계: 스테이지 누적 진행 지수(몬스터 혼합·구간 판정용, 0부터 증가).
        /// 층 안 진행 지수는 레이아웃이 소유하며 elevation 으로 역산하지 않는다(GDD §5.1).
        /// </summary>
        private int GlobalDepth(int floorIndex) =>
            (_stageIndex - 1) * floorCount +
            (_dungeon != null ? _dungeon.ProgressIndexFor(floorIndex) : 0);

        /// <summary>스테이지 누적 층 인덱스(기록/표시용, 아래로 갈수록 음수).</summary>
        private int GlobalFloorIndex(int floorIndex) => floorIndex - (_stageIndex - 1) * floorCount;

        /// <summary>최심층 도착은 보스와 봉인 출구를 안내할 뿐, 즉시 승리시키지 않는다.</summary>
        private void TryDeclareVictory()
        {
            if (hubMode || _runSummary.Ended || _playerState == null || !_playerState.IsAlive) return;
            if (_activeFloorIndex != _dungeon.FinalFloorIndex) return;

            InteractionFeedback?.Invoke(
                BossExitUnlocked
                    ? "최심층 출구의 봉인이 풀렸다 — 출구(▼)로 향하라"
                    : $"최심층 도달 — {BossName}를 쓰러뜨려 출구를 열어라");
            BossStateChanged?.Invoke();
        }

        /// <summary>
        /// 이 칸이 <b>던전 출구</b>인가 — 진행 최종 층의 링크 없는 진출 계단.
        /// 타일 종류(StairsUp/Down)로 판정하면 안 된다: 종류는 공간 이름이라 방향을 탄다.
        /// 활성 층 조건이 없으므로 다른 층에서 표지를 그릴 때도 쓸 수 있다.
        /// </summary>
        private bool IsDungeonExitTile(GridPos pos) =>
            _dungeon != null &&
            _dungeon.TryGetFloor(_dungeon.FinalFloorIndex, out DungeonFloorInfo floor) &&
            _dungeon.OnwardStairOf(floor) is GridPos onward &&
            onward == pos;

        /// <summary>플레이어가 지금 출구를 밟고 있는가(활성 층까지 일치).</summary>
        private bool IsBottomExit(GridPos pos) =>
            _dungeon != null &&
            _activeFloorIndex == _dungeon.FinalFloorIndex &&
            IsDungeonExitTile(pos);

        private bool TryRequestExitChoice()
        {
            if (!IsBottomExit(_playerPos)) return false;
            if (!BossExitUnlocked)
            {
                InteractionFeedback?.Invoke($"{BossName}의 봉인이 출구를 막고 있다");
                return false;
            }

            AtExtractionPoint = false;
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
            // 살아 나왔으니 반입 장비도 창고로 돌아온다(장착 상태 유지).
            ForgeRules.ReturnFromExpedition(meta, _carriedWeaponId, _carriedGearId);
            _carriedWeaponId = "";
            _carriedGearId = "";
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
                kills = _runSummary.Kills,
                deepestFloorIndex = _runSummary.DeepestFloorIndex,
                deepestProgressIndex = _runSummary.FurthestProgressIndex,
                usedRestFloorIndices = SnapshotUsedRestSites(),
                carriedWeaponId = _carriedWeaponId,
                carriedGearId = _carriedGearId,
                hunger = _hunger.Clone(),
                telemetry = _runTelemetry
            };
            carry.WriteItems(_inventory);

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
