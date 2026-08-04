using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public partial class IsoPrototypeDemo
    {
        private const float StaticWalkBobHeight = 0.065f;
        private const int MinimumWalkVisualFrames = 5;
        private const int MinimumTransitionVisualFrames = 4;

        internal readonly struct StaticWalkPose
        {
            public Vector2 Offset { get; }
            public float RotationDegrees { get; }
            public Vector2 Scale { get; }

            public StaticWalkPose(
                Vector2 offset,
                float rotationDegrees,
                Vector2 scale)
            {
                Offset = offset;
                RotationDegrees = rotationDegrees;
                Scale = scale;
            }
        }

        /// <summary>
        /// 정식 walk 클립이 없는 단일 프레임 원정자의 한 스텝 높이. 시작/끝은 반드시 0이고
        /// 중간에만 올라가므로 경로 좌표·접촉 그림자·발판 표식은 그대로 둔 채 미끄러짐을 줄인다.
        /// </summary>
        internal static float StaticWalkArtOffset(float normalizedStep)
        {
            return StaticWalkPoseAt(normalizedStep, 0f).Offset.y;
        }

        /// <summary>
        /// 단일 Frame_0 원정자도 미끄러지지 않게 읽히는 절차형 한 걸음 자세다.
        /// 이동 방향으로 몸을 기울이고, 두 발 교대 리듬의 좌우 흔들림과 squash/stretch를
        /// 함께 써 시작·끝 자세는 정확히 원상 복구한다.
        /// </summary>
        internal static StaticWalkPose StaticWalkPoseAt(
            float normalizedStep,
            float horizontalDirection)
        {
            float t = Mathf.Clamp01(normalizedStep);
            float lift = Mathf.Sin(t * Mathf.PI);
            float gait = Mathf.Sin(t * Mathf.PI * 2f);
            float direction = Mathf.Clamp(horizontalDirection, -1f, 1f);
            return new StaticWalkPose(
                new Vector2(gait * 0.018f, lift * StaticWalkBobHeight),
                -direction * lift * 5f + gait * 1.75f,
                new Vector2(1f - lift * 0.035f, 1f + lift * 0.055f));
        }

        /// <summary>
        /// 느린 Editor 프레임에서도 한 걸음이 한 프레임 스냅으로 소실되지 않게 최소
        /// 표시 프레임을 보장한다. 정상 프레임률에서는 실제 경과 시간이 그대로 상한을 갖는다.
        /// </summary>
        internal static float VisualAnimationProgress(
            float elapsed,
            float duration,
            int renderedFrames,
            int minimumFrames)
        {
            if (duration <= 0f) return 1f;
            float timeProgress = Mathf.Clamp01(elapsed / duration);
            float frameProgress = Mathf.Clamp01(
                renderedFrames / (float)Mathf.Max(1, minimumFrames));
            return Mathf.Min(timeProgress, frameProgress);
        }

        private IEnumerator ApproachAndToggleDoor(ApproachPlan approach, GridPos door)
        {
            yield return MovePlayerPath(approach.Path);
            if (!CanPerformFollowUpAction(approach))
                yield break;

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

        /// <summary>
        /// 갇힌 동료 옆까지 걸어간 뒤 구출한다. 이동 중 죽거나 동료가 이미 사라졌으면 조용히 끝낸다.
        /// </summary>
        private IEnumerator ApproachAndRescue(ApproachPlan approach, GridPos npcPos)
        {
            yield return MovePlayerPath(approach.Path);
            if (!CanPerformFollowUpAction(approach))
                yield break;

            if (!_playerState.IsAlive ||
                !IsPlayerAdjacentTo(npcPos) ||
                !IsRescueNpcAt(npcPos))
                yield break;

            if (!TryRescueNpcAt(npcPos))
                yield break;
            RefreshFloorVisibility();
            yield return ResolveEnemyPhase();
        }

        private IEnumerator ApproachAndRevealSecretDoor(
            ApproachPlan approach,
            GridPos secretDoor)
        {
            yield return MovePlayerPath(approach.Path);
            if (!CanPerformFollowUpAction(approach))
                yield break;

            TileData tile = _grid.Map.Get(secretDoor);
            if (!_playerState.IsAlive ||
                !SecretRoomRules.CanInvestigate(_playerPos, secretDoor) ||
                !SecretRoomRules.IsSecretDoor(tile))
                yield break;

            yield return SetDoorState(secretDoor, TileKind.SecretPassage);
            RevealMappedSecretRoom(secretDoor);
            _runTelemetry?.RecordSecretRoomFound(GlobalFloorIndex(_activeFloorIndex));
            RefreshFloorVisibility();
            FloatingText?.Show(_player.transform.position, "!", FloatingTextKind.Alert);
            InteractionFeedback?.Invoke("숨은 통로 발견 — 안쪽에서 희귀한 기운이 느껴진다");
            Debug.Log($"[SecretRoom] {FloorLabel(_activeFloorIndex)} 비밀문 발견 {secretDoor}");
            yield return ResolveEnemyPhase();
        }

        private IEnumerator ApproachAndDrop(ApproachPlan approach, GridPos hole)
        {
            yield return MovePlayerPath(approach.Path);
            if (!CanPerformFollowUpAction(approach))
                yield break;

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
            GridPos startPos = _playerPos;
            Vector3 start = _player.transform.position;
            Vector3 holeWorld = _grid.GridToWorld(hole);
            Vector3 landingWorld = _grid.GridToWorld(landing);
            Color original = _playerRenderer.color;
            Camera dropCamera = configureMainCamera
                ? (_configuredCamera != null ? _configuredCamera : Camera.main)
                : null;
            Vector3 cameraStart = dropCamera != null
                ? dropCamera.transform.position
                : Vector3.zero;
            Vector3 cameraLanding = new Vector3(landingWorld.x, landingWorld.y, cameraStart.z);

            float elapsed = 0f;
            const float hopDuration = 0.14f;
            while (elapsed < hopDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / hopDuration);
                _player.transform.position = Vector3.Lerp(start, holeWorld, t) +
                                             Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.18f;
                ApplyMovingActorVisualSorting(_playerRenderer, startPos, hole, t);
                yield return null;
            }

            elapsed = 0f;
            const float fallDuration = 0.34f;
            while (elapsed < fallDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                float eased = SmoothStep(t);
                _player.transform.position = Vector3.Lerp(holeWorld, landingWorld, eased);
                ApplyMovingActorVisualSorting(_playerRenderer, hole, landing, eased);
                _player.transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, Mathf.Sin(t * Mathf.PI));
                _playerRenderer.color = new Color(original.r, original.g, original.b, Mathf.Lerp(1f, 0.35f, Mathf.Sin(t * Mathf.PI)));
                if (dropCamera != null)
                    dropCamera.transform.position = Vector3.Lerp(cameraStart, cameraLanding, eased);
                yield return null;
            }

            _player.transform.position = landingWorld;
            ApplyPlayerVisualSorting(landing);
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
            renderer.sprite = ActorSprites.GetDoorInteractionSprite(opening);
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
            // walk 루프는 이동 코루틴의 수명과 정확히 같아야 한다 — try/finally 이므로
            // yield break·인터럽트·StopCoroutine(Dispose) 어느 경로로 끝나도 idle로 복귀한다.
            _playerAnimator?.PlayLoopForDuration(SpriteClipTags.Walk, secondsPerStep);
            try
            {
                yield return MovePlayerPathSteps(path);
            }
            finally
            {
                _playerAnimator?.StopToIdle();
                ResetStaticWalkArtOffset();
            }
        }

        private IEnumerator MovePlayerPathSteps(IReadOnlyList<GridPos> path)
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

                GridPos current = _playerState.Position;
                Vector3 start = _player.transform.position;
                Vector3 end = _grid.GridToWorld(next);
                FacePlayerTowards(next);

                bool changesDungeonFloor = !_dungeon.Height.SameFloor(current, next);
                if (changesDungeonFloor)
                {
                    yield return AnimateFloorTransition(end, next);
                }
                else
                {
                    float elapsed = 0f;
                    int renderedFrames = 0;
                    float t = 0f;
                    float visualDirection = Mathf.Sign(end.x - start.x);
                    Camera movingCamera = Camera.main;
                    Vector3 cameraStart = movingCamera != null
                        ? movingCamera.transform.position
                        : Vector3.zero;
                    float cameraStartSize = movingCamera != null
                        ? movingCamera.orthographicSize
                        : 0f;
                    bool animateCamera = TryGetPlayerCameraFrame(
                        movingCamera,
                        next,
                        out OrthographicCameraFrame cameraDestination);
                    Vector3 cameraEnd = animateCamera
                        ? new Vector3(
                            cameraDestination.Center.x,
                            cameraDestination.Center.y,
                            -10f)
                        : cameraStart;

                    while (t < 1f)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        renderedFrames++;
                        t = VisualAnimationProgress(
                            elapsed,
                            secondsPerStep,
                            renderedFrames,
                            MinimumWalkVisualFrames);
                        float eased = SmoothStep(t);
                        _player.transform.position = Vector3.LerpUnclamped(start, end, eased);
                        SetStaticWalkArtPose(t, visualDirection);
                        ApplyMovingActorVisualSorting(_playerRenderer, current, next, eased);
                        if (animateCamera && movingCamera != null)
                        {
                            movingCamera.transform.position = Vector3.LerpUnclamped(
                                cameraStart,
                                cameraEnd,
                                eased);
                            movingCamera.orthographicSize = Mathf.LerpUnclamped(
                                cameraStartSize,
                                cameraDestination.Size,
                                eased);
                            SyncDungeonAtmosphereBackdropCenter(movingCamera);
                        }
                        yield return null;
                    }
                    ResetStaticWalkArtOffset();
                    ApplyPlayerVisualSorting(next);
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
                    yield return AnimateFloorTransition(
                        _grid.GridToWorld(floorDestination),
                        floorDestination);

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
                    int previousFloor = _activeFloorIndex;
                    _activeFloorIndex = nextFloor;
                    _runTelemetry?.RecordFloorEntered(
                        GlobalFloorIndex(_activeFloorIndex),
                        GlobalDepth(_activeFloorIndex),
                        FloorLabel(_activeFloorIndex));
                    AnnounceBossApproachIfNeeded();
                    AnnounceSurfaceCrossingIfNeeded(previousFloor);
                    UpdateInputFloorRange();
                    RefreshFloorVisibility();
                    PositionSelection(next);
                    ActiveFloorChanged?.Invoke(_activeFloorIndex);
                    Debug.Log($"[Dungeon] 층 이동: {FloorLabel(_activeFloorIndex)} / " +
                              $"층 내부 높이 {_dungeon.Height.LocalHeight(next.elevation)}");
                    _runSummary.RecordFloor(
                        GlobalFloorIndex(_activeFloorIndex),
                        GlobalDepth(_activeFloorIndex));
                    TryDeclareVictory();
                    if (_runSummary.Ended) yield break;
                    SaveCheckpoint();
                }
                else
                {
                    RefreshFloorVisibility();
                }

                PreserveNewTravelEnemySighted();
                yield return ResolveEnemyPhase();
                if (!_playerState.IsAlive)
                    yield break;

                if (i >= path.Count - 1) continue; // 마지막 스텝 뒤엔 멈출 이동이 없다

                if (_travelCancelRequested)
                {
                    InteractionFeedback?.Invoke("MOVE CANCELED");
                    yield break;
                }

                TravelInterrupt interrupt = EvaluateTravelInterruptAfterAction(
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
        private bool _travelEnemySightedDuringAction;

        private void SnapshotTravelSight()
        {
            _travelEnemySightedDuringAction = false;
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

        /// <summary>
        /// 플레이어 행동 직후 보였던 적이 자기 턴에 이동하거나 죽어도 발견 사건을 잃지 않는다.
        /// 최종 FOV만 비교하면 문을 여는 찰나의 위협이 자동 이동 인터럽트에서 사라진다.
        /// </summary>
        private void PreserveNewTravelEnemySighted()
        {
            if (_travelEnemySightedDuringAction) return;
            _travelEnemySightedDuringAction = TravelRules.Evaluate(
                _travelVisibleEnemyIds,
                EnemySightStates(),
                newItemSighted: false,
                tookDamage: false) == TravelInterrupt.EnemySighted;
        }

        private TravelInterrupt EvaluateTravelInterruptAfterAction(bool tookDamage)
        {
            return TravelRules.Evaluate(
                _travelVisibleEnemyIds,
                EnemySightStates(),
                AnyNewVisibleItem(),
                tookDamage,
                enemySightedDuringAction: _travelEnemySightedDuringAction);
        }

        private IEnumerable<(string, bool, bool)> EnemySightStates()
        {
            foreach (EnemyAgent enemy in _enemies)
                yield return (
                    enemy.State.Id,
                    _visibleTiles.Contains(enemy.State.Position),
                    enemy.State.IsAlive);
        }

        /// <summary>보이는 적 때문에 자동 이동이 입력당 한 행동으로 제한되는 상태.</summary>
        public bool IsTravelSingleActionMode => AnyEnemyVisible();

        private bool AnyEnemyVisible()
        {
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.State.IsAlive && _visibleTiles.Contains(enemy.State.Position))
                    return true;
            }
            return false;
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

        private IEnumerator AnimateFloorTransition(Vector3 destination, GridPos destinationPos)
        {
            SpriteRenderer[] transitionRenderers = _player != null
                ? _player.GetComponentsInChildren<SpriteRenderer>(includeInactive: true)
                : System.Array.Empty<SpriteRenderer>();
            var originalColors = new Color[transitionRenderers.Length];
            for (int i = 0; i < transitionRenderers.Length; i++)
                originalColors[i] = transitionRenderers[i].color;
            const float halfDuration = 0.12f;
            float visualDirection = _player != null
                ? Mathf.Sign(destination.x - _player.transform.position.x)
                : 0f;
            try
            {
                float elapsed = 0f;
                int renderedFrames = 0;
                float t = 0f;
                while (t < 1f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    renderedFrames++;
                    t = VisualAnimationProgress(
                        elapsed,
                        halfDuration,
                        renderedFrames,
                        MinimumTransitionVisualFrames);
                    SetTransitionAlpha(
                        transitionRenderers,
                        originalColors,
                        1f - SmoothStep(t));
                    SetStaticWalkArtPose(t * 0.5f, visualDirection);
                    yield return null;
                }

                // 완전히 가려진 한 프레임에서 링크 반대편으로 옮긴다. 서로 먼 두 층 좌표를
                // 화면 위로 날아가는 보간은 계단이 아니라 순간이동처럼 보인다.
                SetTransitionAlpha(transitionRenderers, originalColors, 0f);
                _player.transform.position = destination;
                ApplyPlayerVisualSorting(destinationPos);
                Camera transitionCamera = Camera.main;
                if (TryGetPlayerCameraFrame(
                        transitionCamera,
                        destinationPos,
                        out OrthographicCameraFrame destinationFrame))
                    ApplyCameraFrame(transitionCamera, destinationFrame);

                elapsed = 0f;
                renderedFrames = 0;
                t = 0f;
                while (t < 1f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    renderedFrames++;
                    t = VisualAnimationProgress(
                        elapsed,
                        halfDuration,
                        renderedFrames,
                        MinimumTransitionVisualFrames);
                    SetTransitionAlpha(
                        transitionRenderers,
                        originalColors,
                        SmoothStep(t));
                    SetStaticWalkArtPose(0.5f + t * 0.5f, visualDirection);
                    yield return null;
                }
            }
            finally
            {
                RestoreTransitionColors(transitionRenderers, originalColors);
                ResetStaticWalkArtOffset();
            }
        }

        private static void SetTransitionAlpha(
            SpriteRenderer[] renderers,
            Color[] originalColors,
            float alphaFactor)
        {
            float factor = Mathf.Clamp01(alphaFactor);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null) continue;
                Color original = originalColors[i];
                renderer.color = new Color(
                    original.r,
                    original.g,
                    original.b,
                    original.a * factor);
            }
        }

        private static void RestoreTransitionColors(
            SpriteRenderer[] renderers,
            Color[] originalColors)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].color = originalColors[i];
        }

        private void SetStaticWalkArtPose(
            float normalizedStep,
            float horizontalDirection)
        {
            if (_playerAnimator != null || _playerRenderer == null) return;
            StaticWalkPose pose = StaticWalkPoseAt(
                normalizedStep,
                horizontalDirection);
            StaticFacingPose facing = StaticFacingPoseFor(ViewFacing(_playerWorldFacing));
            Transform art = _playerRenderer.transform;
            art.localPosition = new Vector3(
                facing.Offset.x + pose.Offset.x,
                facing.Offset.y + pose.Offset.y,
                0f);
            art.localRotation = Quaternion.Euler(0f, 0f, pose.RotationDegrees);
            art.localScale = new Vector3(
                playerVisualScale * facing.Scale.x * pose.Scale.x,
                playerVisualScale * facing.Scale.y * pose.Scale.y,
                playerVisualScale);
        }

        private void ResetStaticWalkArtOffset()
        {
            ApplyPlayerFacing();
        }
        /// <summary>
        /// 지하에서 지상으로 처음 올라선 순간(B1 → 1F)을 한 판에 한 번 알린다.
        /// 상승 던전이 공짜로 주는 유일한 서사 전환점이라, 여기서 한 번 짚어 주면
        /// "건물을 타고 오른다"는 구조가 설명 없이 읽힌다. 판정은 Core 가 소유한다.
        /// </summary>
        private void AnnounceSurfaceCrossingIfNeeded(int previousFloorIndex)
        {
            if (hubMode || _dungeon == null || _surfaceCrossingAnnounced) return;

            if (!DungeonDirectionRules.CrossesIntoAboveGround(
                    _dungeon.Direction,
                    _dungeon.FirstBuildingFloor,
                    _dungeon.ProgressIndexFor(previousFloorIndex),
                    _dungeon.ProgressIndexFor(_activeFloorIndex)))
                return;

            _surfaceCrossingAnnounced = true;
            InteractionFeedback?.Invoke("지상층이다 — 깨진 외벽 너머로 바깥이 보인다");
            Debug.Log("[Dungeon] 지상 진입: " + FloorLabel(_activeFloorIndex));
        }

    }
}
