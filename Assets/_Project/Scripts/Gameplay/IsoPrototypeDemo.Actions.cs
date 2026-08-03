using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public partial class IsoPrototypeDemo
    {

        /// <summary>응급 키트를 사용해 HP를 회복한다. 행동 1회를 소비한다.</summary>
        public void UsePotion()
        {
            if (!Application.isPlaying || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (RejectWorldActionWhileVerticalLooking()) return;
            if (PotionCount <= 0)
            {
                InteractionFeedback?.Invoke("NO MEDKITS");
                return;
            }
            if (_playerState.Hp >= _playerState.MaxHp)
            {
                InteractionFeedback?.Invoke("HP FULL");
                return;
            }

            SetBombAiming(false);
            _moveRoutine = StartCoroutine(RunPlayerAction(DrinkPotion()));
        }

        /// <summary>
        /// 컨텍스트 상호작용 버튼 라벨 (M4~M5 이월분). 인접한 문/폭발통이 있으면
        /// "OPEN"/"CLOSE"/"PUSH", 없으면 null — HUD 가 매 프레임 폴링해 버튼을 숨긴다.
        /// </summary>
        public string ContextInteractionLabel =>
            !Application.isPlaying || _resolvingAction || IsVerticalLookActive ||
            _playerState == null || !_playerState.IsAlive || _runSummary.Ended
                ? null
                : DropContextInteractionLabel ??
                  (TryGetCurrentConnectorInteraction(out string connectorLabel)
                      ? connectorLabel
                      : TryFindAdjacentInteraction(out _, out string label) ? label : null);

        /// <summary>상호작용 버튼 실행 — 스페이스바/액션 휠과 같은 경로.</summary>
        public void PerformContextInteraction() => InteractAdjacent();

        /// <summary>폭탄/냉기 폭탄 조준 모드. 켠 상태에서 타일을 탭하면 투척한다.</summary>
        public void ToggleBombAim() => ToggleThrowAim(ItemKind.Bomb);

        public void ToggleFrostBombAim() => ToggleThrowAim(ItemKind.FrostBomb);

        /// <summary>투척류 아이템 공통 조준 진입점 (인벤토리 화면이 호출).</summary>
        public void ToggleAim(ItemKind kind)
        {
            if (kind == ItemKind.Bomb || kind == ItemKind.FrostBomb ||
                kind == ItemKind.OilFlask || kind == ItemKind.ThrowingKnife)
                ToggleThrowAim(kind);
        }

        private void ToggleThrowAim(ItemKind kind)
        {
            if (!Application.isPlaying || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;

            if (IsVerticalLookActive && !VerticalThrowRules.Supports(kind))
            {
                InteractionFeedback?.Invoke("다른 층에는 폭탄·냉각재·기름만 던질 수 있다");
                return;
            }

            bool alreadyAimingThis = _bombAiming && _bombAimKind == kind;
            if (!alreadyAimingThis && _inventory.Count(kind) <= 0)
            {
                InteractionFeedback?.Invoke($"NO {ItemCatalog.ShortLabel(kind)}S");
                return;
            }

            _bombAimKind = kind;
            SetBombAiming(!alreadyAimingThis);
            string aimHint = kind == ItemKind.ThrowingKnife
                ? $"KNIFE: 적을 탭 · 사거리 {rangedAttackRange} · 피해 {knifeDamage}"
                : $"{ItemCatalog.ShortLabel(kind)}: 목표 타일 탭 · 사거리 {bombThrowRange} · 3×3";
            InteractionFeedback?.Invoke(_bombAiming ? aimHint : "AIM CANCELED");
        }

        private void SetBombAiming(bool aiming)
        {
            if (aiming)
                ExitCameraLook(announce: false, applyCamera: true);
            _bombAiming = aiming;
            if (aiming) ClearDropFocus(restoreSelection: true);
            if (!aiming) _aimHoverCell = null;
            // 폭탄 조준 또는 화면에 보이는 Hole이 있을 때만 픽 비용을 낸다.
            UpdateWorldHoverTracking();
            _input?.InvalidateHover();
            RefreshThrowRangePreview();
            // 조준 종류 전환도 HUD 하이라이트에 반영돼야 하므로 상태가 같아도 알린다.
            BombAimingChanged?.Invoke(aiming);
        }

        /// <summary>HUD Escape가 메뉴보다 먼저 투척 조준 한 단계만 취소할 때 사용한다.</summary>
        public bool CancelThrowAim()
        {
            if (!_bombAiming) return false;
            SetBombAiming(false);
            InteractionFeedback?.Invoke("AIM CANCELED");
            return true;
        }

        /// <summary>선택 마커를 옮기고 행동 코루틴을 잠금 래퍼로 시작한다. (탭 분기 공통 꼬리)</summary>
        private void StartPlayerAction(
            GridPos target,
            IEnumerator action,
            bool preserveVerticalLook = false)
        {
            PositionSelection(target);
            _moveRoutine = StartCoroutine(RunPlayerAction(action, preserveVerticalLook));
        }

        private bool TryFindApproach(GridPos target, out List<GridPos> path)
        {
            path = FindPathToAdjacent(target);
            return path.Count > 0;
        }

        /// <summary>
        /// 플레이어 행동 코루틴 공통 래퍼. 진행 중 입력 잠금(_resolvingAction)과
        /// 핸들 해제를 한 곳에서 처리해, 개별 행동이 잠금 해제를 잊을 수 없게 한다.
        /// </summary>
        private IEnumerator RunPlayerAction(
            IEnumerator action,
            bool preserveVerticalLook = false)
        {
            // 카메라 관찰은 무턴이지만 실제 행동이 수락되면 플레이어 추적을 먼저 되찾는다.
            // 거절된 클릭은 이 공통 래퍼에 도달하지 않으므로 카메라를 뜻밖에 되돌리지 않는다.
            ExitCameraLook(announce: false, applyCamera: true);
            _resolvingAction = true;
            _travelCancelRequested = false;
            ClearDropFocus(restoreSelection: false);
            if (!preserveVerticalLook)
                ExitVerticalLook(announce: false);
            UpdateWorldHoverTracking();
            yield return action;
            _resolvingAction = false;
            _moveRoutine = null;
            UpdateWorldHoverTracking();
        }

        private IEnumerator AutoDescend()
        {
            yield return null;
            _dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo floor);
            if (_dungeon.OnwardStairOf(floor) is GridPos onward)
                HandleTileTapped(onward, tileExists: true);
        }

        private IEnumerator ApproachAndAttack(IReadOnlyList<GridPos> path, EnemyAgent enemy)
        {
            yield return MovePlayerPath(path);

            if (_playerState.IsAlive && enemy.State.IsAlive &&
                CombatRules.CanMelee(_grid.Map, _playerState, enemy.State, _playerLoadout.MeleeReach))
            {
                FacePlayerTowards(enemy.State.Position);
                _playerAnimator?.PlayOnce(SpriteClipTags.Attack);
                yield return AnimateMeleeLunge(
                    _player.transform,
                    _playerRenderer,
                    enemy.Root != null
                        ? enemy.Root.transform.position
                        : _grid.GridToWorld(enemy.State.Position));
                if (CombatRules.TryMelee(
                        _playerState, enemy.State, out int damage,
                        _grid.Map, _playerLoadout.MeleeReach))
                {
                    if (_runTelemetry != null) _runTelemetry.meleeAttacks++;
                    yield return ShowEnemyHit(enemy, damage, "Melee", _playerPos);
                    // 둔기 장비: 때린 대상을 밀어낸다 — 구멍·창문 앞이면 그대로 낙하로 이어진다.
                    if (_playerLoadout.KnockbackOnHit && enemy.State.IsAlive)
                        yield return KnockbackCombatant(_playerState.Position, enemy.State);
                    yield return ResolveEnemyPhase();
                }
            }
        }

        /// <summary>
        /// 아크 캐스터 사격. 장비·탄약 게이트는 <see cref="RangedWeaponRules"/>가 소유하고
        /// 여기서는 접근·연출만 한다 — 판정과 셀 소비가 갈라지지 않게(M4).
        /// </summary>
        private IEnumerator RangedAttack(EnemyAgent enemy)
        {
            RangedFireBlock gate = RangedWeaponRules.Diagnose(_playerLoadout, _rangedCharges);
            if (gate != RangedFireBlock.None)
            {
                InteractionFeedback?.Invoke(gate == RangedFireBlock.NoWeapon
                    ? "원거리 장비가 없다"
                    : $"충전 없음 — {_playerLoadout.RangedRechargeTurns}턴마다 1칸 찬다 (에너지 셀로 즉시 충전)");
                yield break;
            }

            int range = _playerLoadout.RangedRange;
            if (RangedWeaponRules.TryFire(
                    _playerState, enemy.State, _grid.Map, _playerLoadout, _rangedCharges,
                    out int damage, out _, rangedAttackDamage))
            {
                RangedChargesChanged?.Invoke();
                yield return FireRanged(enemy, damage);
            }
            else
            {
                // 쏠 수 없으면 사격 가능 위치까지 접근한다 (SPD식). 탭당 1스텝 규칙 유지.
                // 셀은 아직 안 썼다 — 접근만으로 탄이 줄면 이동이 곧 손해가 된다.
                RangedBlockReason reason = CombatRules.DiagnoseRanged(
                    _grid.Map, _playerPos, enemy.State.Position, range);
                if (!CombatRules.FindFiringPosition(
                        _grid.Map, _playerPos, enemy.State.Position, range,
                        out List<GridPos> firingPath,
                        pos => pos != _playerPos &&
                               (IsLivingEnemyAt(pos) || _grid.Map.Get(pos)?.kind == TileKind.WeakFloor)))
                {
                    InteractionFeedback?.Invoke("사선을 잡을 위치가 없다");
                    yield break;
                }

                InteractionFeedback?.Invoke(reason switch
                {
                    RangedBlockReason.ElevationMismatch => "높이가 다르다 — 계단으로 접근한다",
                    RangedBlockReason.Blocked => "사선이 막혔다 — 접근한다",
                    _ => $"사거리 밖(MAX {range}) — 접근한다"
                });

                int allowedSteps = TravelRules.AllowedSteps(AnyEnemyVisible(), firingPath.Count - 1);
                if (allowedSteps < firingPath.Count - 1)
                    firingPath.RemoveRange(allowedSteps + 1, firingPath.Count - allowedSteps - 1);
                yield return MovePlayerPath(firingPath);

                // 접근이 끝난 그 탭에서 조건이 갖춰졌으면 즉시 발사.
                if (_playerState.IsAlive && enemy.State.IsAlive &&
                    RangedWeaponRules.TryFire(
                        _playerState, enemy.State, _grid.Map, _playerLoadout, _rangedCharges,
                        out int approachDamage, out _, rangedAttackDamage))
                {
                    RangedChargesChanged?.Invoke();
                    yield return FireRanged(enemy, approachDamage);
                }
            }
        }

        /// <summary>원거리 명중 연출·텔레메트리·적 페이즈 해소 — 즉시 발사와 접근 후 발사가 공유한다.</summary>
        private IEnumerator FireRanged(EnemyAgent enemy, int damage)
        {
            if (_runTelemetry != null) _runTelemetry.rangedAttacks++;
            FacePlayerTowards(enemy.State.Position);
            _playerAnimator?.PlayOnce(SpriteClipTags.Attack);
            yield return AnimateRangedRelease(
                _player.transform,
                _playerRenderer,
                ProjectileEndpointWorld(enemy.State.Position, isVerticalEndpoint: false),
                new Color32(61, 225, 232, 255));
            yield return AnimateProjectile(_playerPos, enemy.State.Position);
            InteractionFeedback?.Invoke($"RANGED HIT · {damage} DAMAGE");
            yield return ShowEnemyHit(enemy, damage, "Ranged", _playerPos);
            yield return ResolveEnemyPhase();
        }

        /// <summary>
        /// 배고픔 한 턴. 굶고 있으면 주기마다 HP를 깎고, 단계가 바뀔 때만 알린다 —
        /// 매 턴 경고하면 소음이 되고, 아무 말도 없으면 조용히 죽는다.
        /// </summary>
        /// <summary>
        /// 턴당 사격 충전 회복. 배고픔과 같은 적 페이즈 진입점에서 돈다 — 재충전을
        /// "기다림"으로 느끼게 하려면 시간의 단위가 다른 소모 규칙과 같아야 한다.
        /// </summary>
        private void TickRangedCharges()
        {
            if (hubMode || _rangedCharges == null) return;
            if (_rangedCharges.Tick(_playerLoadout)) RangedChargesChanged?.Invoke();
        }

        private IEnumerator TickHunger()
        {
            if (hubMode || _playerState == null || !_playerState.IsAlive) yield break;

            int damage = _hunger.Tick();
            HungerStage stage = _hunger.Stage;
            if (stage != _lastHungerStage)
            {
                _lastHungerStage = stage;
                if (stage == HungerStage.Hungry)
                    InteractionFeedback?.Invoke("배가 고프다 — 통조림을 찾아야 한다");
                else if (stage == HungerStage.Starving)
                    InteractionFeedback?.Invoke("굶주리고 있다 — 체력이 깎인다");
            }

            if (damage <= 0) yield break;

            _playerState.TakeDamage(damage);
            _runTelemetry?.RecordStarvation(damage);
            yield return ShowPlayerHit(damage, "Starving");
        }

        /// <summary>통조림을 먹어 배고픔을 채운다. 행동 1회를 소비한다.</summary>
        public void EatFood()
        {
            if (!Application.isPlaying || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (RejectWorldActionWhileVerticalLooking()) return;
            if (_inventory.Count(ItemKind.CannedFood) <= 0)
            {
                InteractionFeedback?.Invoke("NO FOOD");
                return;
            }

            SetBombAiming(false);
            _moveRoutine = StartCoroutine(RunPlayerAction(EatFoodAction()));
        }

        /// <summary>에너지 셀로 사격 충전을 즉시 채운다. 행동 1회를 소비한다.</summary>
        public void UseEnergyCell()
        {
            if (!Application.isPlaying || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (RejectWorldActionWhileVerticalLooking()) return;
            if (_inventory.Count(ItemKind.EnergyCell) <= 0)
            {
                InteractionFeedback?.Invoke("에너지 셀이 없다");
                return;
            }
            // 가득이면 쓰지 않는다 — 셀은 사격 횟수가 아니라 기다림을 사는 물건이라
            // 만충일 때 소비하면 아무것도 사지 않고 버리는 셈이 된다.
            if (_rangedCharges.IsFull(_playerLoadout))
            {
                InteractionFeedback?.Invoke("충전이 이미 가득하다");
                return;
            }

            SetBombAiming(false);
            _moveRoutine = StartCoroutine(RunPlayerAction(UseEnergyCellAction()));
        }

        private IEnumerator UseEnergyCellAction()
        {
            _inventory.TryUse(ItemKind.EnergyCell);
            _runTelemetry?.RecordItemUsed(ItemKind.EnergyCell, GlobalFloorIndex(_activeFloorIndex));
            InventoryChanged?.Invoke();

            _rangedCharges.TryRefill(_playerLoadout);
            RangedChargesChanged?.Invoke();
            InteractionFeedback?.Invoke($"급속 충전 — 사격 {_rangedCharges.charges}발");
            Debug.Log($"[Ranged] 에너지 셀 사용 → 충전 {_rangedCharges.charges}");
            yield return FlashColor(_playerRenderer, new Color32(61, 225, 232, 255));

            yield return ResolveEnemyPhase();
        }

        private IEnumerator EatFoodAction()
        {
            _inventory.TryUse(ItemKind.CannedFood);
            _runTelemetry?.RecordItemUsed(ItemKind.CannedFood, GlobalFloorIndex(_activeFloorIndex));
            InventoryChanged?.Invoke();

            int fed = _hunger.Feed(HungerRules.RationSatiation);
            _lastHungerStage = _hunger.Stage;
            InteractionFeedback?.Invoke(
                fed > 0 ? "통조림을 먹었다 — 배가 든든하다" : "배가 이미 부르다");
            Debug.Log($"[Hunger] 통조림 섭취 +{fed} → {_hunger.satiation}");
            yield return FlashColor(_playerRenderer, new Color32(196, 168, 96, 255));

            yield return ResolveEnemyPhase();
        }

        private IEnumerator DrinkPotion()
        {
            _inventory.TryUse(ItemKind.Potion);
            _runTelemetry?.RecordItemUsed(ItemKind.Potion, GlobalFloorIndex(_activeFloorIndex));
            InventoryChanged?.Invoke();

            int healed = _playerState.Heal(potionHealAmount);
            UpdateHealthBar(_playerHpFill, _playerState);
            PlayerHpChanged?.Invoke();
            FloatingText?.ShowDamage(_player.transform.position, healed, FloatingTextKind.Heal);
            InteractionFeedback?.Invoke($"MEDKIT +{healed} HP");
            Debug.Log($"[Item] 응급 키트 사용: +{healed} HP → {_playerState.Hp}/{_playerState.MaxHp}");
            yield return FlashColor(_playerRenderer, new Color32(96, 224, 128, 255));

            yield return ResolveEnemyPhase();
        }

        private IEnumerator ThrowOil(
            GridPos target,
            VerticalThrowPath? verticalPath = null)
        {
            SetBombAiming(false);
            _inventory.TryUse(ItemKind.OilFlask);
            _runTelemetry?.RecordItemUsed(ItemKind.OilFlask, GlobalFloorIndex(_activeFloorIndex));
            InventoryChanged?.Invoke();

            GridPos releaseTarget = ProjectileReleaseTarget(
                _playerPos,
                target,
                verticalPath);
            FacePlayerTowards(releaseTarget);
            _playerAnimator?.PlayOnce(SpriteClipTags.Attack);
            yield return AnimateRangedRelease(
                _player.transform,
                _playerRenderer,
                ProjectileReleaseWorldTarget(_playerPos, target, verticalPath),
                new Color32(228, 160, 68, 255));
            yield return AnimateProjectile(
                _playerPos,
                target,
                verticalPath,
                ProjectileVisualKind.OilFlask);
            List<GridPos> splashed = OilRules.Splash(_grid.Map, target);
            InteractionFeedback?.Invoke($"OIL SPLASHED ×{splashed.Count} — 불이 닿으면 발화한다");
            Debug.Log($"[Item] 기름 살포 {target}: {splashed.Count}칸");
            RefreshFloorVisibility();

            if (verticalPath.HasValue)
                ExitVerticalLook(announce: false);

            yield return ResolveEnemyPhase();
        }

        private IEnumerator ThrowKnife(EnemyAgent enemy)
        {
            SetBombAiming(false);
            _inventory.TryUse(ItemKind.ThrowingKnife);
            _runTelemetry?.RecordItemUsed(ItemKind.ThrowingKnife, GlobalFloorIndex(_activeFloorIndex));
            InventoryChanged?.Invoke();

            FacePlayerTowards(enemy.State.Position);
            _playerAnimator?.PlayOnce(SpriteClipTags.Attack);
            yield return AnimateRangedRelease(
                _player.transform,
                _playerRenderer,
                ProjectileEndpointWorld(enemy.State.Position, isVerticalEndpoint: false),
                new Color32(255, 194, 82, 255));
            if (CombatRules.TryRanged(
                    _playerState, enemy.State, _grid.Map, rangedAttackRange,
                    out int damage, knifeDamage))
            {
                yield return AnimateProjectile(
                    _playerPos,
                    enemy.State.Position,
                    visualKind: ProjectileVisualKind.Knife);
                InteractionFeedback?.Invoke($"KNIFE HIT · {damage} DAMAGE");
                yield return ShowEnemyHit(enemy, damage, "Knife", _playerPos);
            }
            else
            {
                // 소모는 이미 됐다 — 빗나간 투척도 손해라는 감각 유지.
                bool blocked = !CombatRules.HasLineOfSight(_grid.Map, _playerPos, enemy.State.Position);
                InteractionFeedback?.Invoke(blocked ? "KNIFE BLOCKED" : $"OUT OF RANGE · MAX {rangedAttackRange}");
            }

            yield return ResolveEnemyPhase();
        }

        /// <summary>조합 실행 (인벤토리 화면이 호출). 행동 1회를 소비한다.</summary>
        public void CraftRecipe(int recipeIndex)
        {
            if (!Application.isPlaying || hubMode || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (RejectWorldActionWhileVerticalLooking()) return;
            if (recipeIndex < 0 || recipeIndex >= CraftingRules.Recipes.Length) return;

            Recipe recipe = CraftingRules.Recipes[recipeIndex];
            if (!CraftingRules.CanCraft(_inventory, recipe))
            {
                InteractionFeedback?.Invoke("재료가 모자라다");
                return;
            }

            SetBombAiming(false);
            _moveRoutine = StartCoroutine(RunPlayerAction(CraftAction(recipe)));
        }

        private IEnumerator CraftAction(Recipe recipe)
        {
            CraftingRules.TryCraft(_inventory, recipe);
            _runTelemetry?.RecordItemCrafted(recipe.Output, GlobalFloorIndex(_activeFloorIndex));
            InventoryChanged?.Invoke();
            InteractionFeedback?.Invoke(
                $"조합: {ItemCatalog.DisplayName(recipe.Output)} 완성!");
            Debug.Log($"[Craft] {recipe} 조합 완료");
            yield return FlashColor(_playerRenderer, new Color32(196, 150, 90, 255));

            yield return ResolveEnemyPhase();
        }

        /// <summary>귀환 비컨: 현재 층 입구로 순간이동. 행동 1회 소비.</summary>
        public void UseRecallScroll()
        {
            if (!Application.isPlaying || _resolvingAction ||
                _playerState == null || !_playerState.IsAlive)
                return;
            if (RejectWorldActionWhileVerticalLooking()) return;
            if (_inventory.Count(ItemKind.RecallScroll) <= 0)
            {
                InteractionFeedback?.Invoke("NO RECALL");
                return;
            }

            SetBombAiming(false);
            _moveRoutine = StartCoroutine(RunPlayerAction(RecallToEntry()));
        }

        private IEnumerator RecallToEntry()
        {
            _inventory.TryUse(ItemKind.RecallScroll);
            _runTelemetry?.RecordItemUsed(ItemKind.RecallScroll, GlobalFloorIndex(_activeFloorIndex));
            InventoryChanged?.Invoke();

            _dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo floor);
            GridPos destination = floor.Entry;
            if (IsLivingEnemyAt(destination))
            {
                // 입구가 막혔으면 걷기 가능한 인접 칸으로 비껴 착지한다.
                foreach (GridPos candidate in new[]
                         { destination.North, destination.East, destination.South, destination.West })
                {
                    if (_grid.Map.IsWalkable(candidate) && !IsLivingEnemyAt(candidate))
                    {
                        destination = candidate;
                        break;
                    }
                }
            }

            InteractionFeedback?.Invoke("RECALL — 층 입구로 귀환");
            Debug.Log($"[Item] 귀환 비컨: {_playerPos} → {destination}");
            yield return AnimateFloorTransition(_grid.GridToWorld(destination), destination);
            _playerState.MoveTo(destination);
            SyncPlayerView(destination, floorChanged: false);

            yield return ResolveEnemyPhase();
        }

        private IEnumerator ThrowBomb(
            GridPos target,
            ItemKind kind,
            VerticalThrowPath? verticalPath = null)
        {
            SetBombAiming(false);
            _inventory.TryUse(kind);
            _runTelemetry?.RecordItemUsed(kind, GlobalFloorIndex(_activeFloorIndex));
            InventoryChanged?.Invoke();

            bool fiery = kind != ItemKind.FrostBomb;
            GridPos releaseTarget = ProjectileReleaseTarget(
                _playerPos,
                target,
                verticalPath);
            FacePlayerTowards(releaseTarget);
            _playerAnimator?.PlayOnce(SpriteClipTags.Attack);
            yield return AnimateRangedRelease(
                _player.transform,
                _playerRenderer,
                ProjectileReleaseWorldTarget(_playerPos, target, verticalPath),
                fiery
                    ? new Color32(255, 127, 54, 255)
                    : new Color32(111, 222, 247, 255));
            yield return AnimateProjectile(
                _playerPos,
                target,
                verticalPath,
                fiery ? ProjectileVisualKind.Bomb : ProjectileVisualKind.FrostBomb);
            yield return ResolveExplosion(target, fiery ? bombDamage : frostBombDamage, fiery);

            if (verticalPath.HasValue)
                ExitVerticalLook(announce: false);

            if (_playerState.IsAlive)
                yield return ResolveEnemyPhase();
        }

    }
}
