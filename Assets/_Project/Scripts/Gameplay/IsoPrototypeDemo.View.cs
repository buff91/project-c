using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public partial class IsoPrototypeDemo
    {
        // PC field-deck에서 우측 instrument/floor stack과 하단 dock을 제외한 실제 플레이 영역.
        private static readonly Rect B2RoomHudSafeViewport =
            new Rect(0.03f, 0.10f, 0.69f, 0.85f);
        // 셀 중심 경계 밖으로 솟는 서비스 벽과 낮은 프롭의 실루엣 여백.
        private static readonly Vector2 B2RoomCameraPadding = new Vector2(0.70f, 1.15f);

        public void RotateView(int direction)
        {
            if (_grid == null || _dungeon == null || _resolvingAction || direction == 0)
                return;

            ExitCameraLook(announce: false, applyCamera: false);
            ClearDropFocus(restoreSelection: true);
            _grid.iso.RotateView(direction);
            ApplyViewToVisuals();
            ConfigureCamera(Camera.main);
            _input?.InvalidateHover();
            ViewRotationChanged?.Invoke(_grid.iso.viewQuarterTurns);
            Debug.Log($"[View] 아이소 시점 회전: {_grid.iso.viewQuarterTurns * 90}°");
        }

        public void ToggleViewMode()
        {
            ExitCameraLook(announce: false, applyCamera: false);
            ExitVerticalLook(announce: false, refreshPresentation: false);
            ClearDropFocus(restoreSelection: true);
            viewMode = viewMode == DungeonViewMode.Play
                ? DungeonViewMode.DebugAll
                : DungeonViewMode.Play;
            ApplyViewToVisuals();
            ConfigureCamera(Camera.main);
            UpdateWorldHoverTracking();
            _input?.InvalidateHover();
            ViewModeChanged?.Invoke(viewMode);
            Debug.Log($"[View] 던전 표시 모드: {viewMode}");
        }

        public void ToggleCombatMode()
        {
            if (RejectWorldActionWhileVerticalLooking()) return;
            if (combatMode == CombatActionMode.Melee && !_playerLoadout.HasRanged)
            {
                InteractionFeedback?.Invoke("원거리 장비가 없다");
                return;
            }

            combatMode = combatMode == CombatActionMode.Melee
                ? CombatActionMode.Ranged
                : CombatActionMode.Melee;
            CombatModeChanged?.Invoke(combatMode);
            InteractionFeedback?.Invoke(combatMode == CombatActionMode.Melee
                ? "MELEE: 적을 탭해 접근 공격"
                : $"RANGED: 사거리 {_playerLoadout.RangedRange} · 충전 {RangedCharges}/{RangedChargeCapacity}");
        }

        public void ApplyVisualSettings()
        {
            exploredAlpha = Mathf.Clamp(exploredAlpha, 0.05f, 0.4f);
            verticalPreviewAlpha = Mathf.Clamp(verticalPreviewAlpha, 0.1f, 0.8f);
            playerOccluderAlpha = Mathf.Clamp(playerOccluderAlpha, 0.12f, 0.7f);

            // 지하 어둠: 깊은 층 앰비언트가 얕은 층보다 밝아지지 않도록 상한을 건다.
            surfaceLightLevel = Mathf.Clamp(surfaceLightLevel, 0.3f, 1f);
            deepLightLevel = Mathf.Clamp(deepLightLevel, 0.02f, surfaceLightLevel);
            darknessFloor = Mathf.Clamp(darknessFloor, 0.03f, 0.4f);
            contactShadowStrength = Mathf.Clamp(contactShadowStrength, 0.1f, 0.9f);
            hubFogEdgeLevel = Mathf.Clamp(hubFogEdgeLevel, 0.4f, 1f);
            lightHueStrength = Mathf.Clamp01(lightHueStrength);
            carriedWarmth = Mathf.Clamp01(carriedWarmth);
            directionalShadowStrength = Mathf.Clamp(directionalShadowStrength, 0.4f, 1f);
            // 광원 파라미터가 바뀌면 정적 광량 필드를 다시 계산한다.
            MarkStaticLightDirty();

            if (_dungeon == null) return;
            RefreshFloorVisibility();
            UpdatePlayerOccluders(0f, instant: true);
        }

        private void ApplyViewToVisuals()
        {
            foreach (var pair in _tileRenderers)
            {
                pair.Value.transform.position = VisualPosition(pair.Key);
                TileKind kind = _grid.Map.Get(pair.Key).kind;
                pair.Value.sortingOrder = _grid.iso.SortingOrder(
                    TileVisualSortingPos(pair.Key, kind),
                    TileSortOffset(kind));
                pair.Value.sprite = GetTileSprite(kind, pair.Key);
            }

            if (_playerSorting != null)
            {
                _playerSorting.Apply();
                ApplyPlayerVisualSorting(_playerSorting.Pos);
            }
            foreach (EnemyAgent enemy in _enemies)
                ApplyEnemyVisuals(enemy);
            foreach (ItemAgent item in _items)
            {
                if (item.Root == null) continue;
                item.Root.transform.position = VisualPosition(item.Spawn.Position);
                item.Renderer.sortingOrder = _grid.iso.SortingOrder(item.Spawn.Position, 0);
            }
            ApplyRestSiteView();
            ApplyBossAltarView();
            ApplyRescueNpcView();
            ApplyExtractionPointView();
            if (_barrelRenderer != null)
            {
                // RefreshFloorVisibility 도 같은 값을 넣는다 — 예전엔 여기만 GridToWorld 라
                // 회전과 가시성 갱신 중 뭐가 마지막이냐에 따라 통이 튀었다.
                _barrel.transform.position = VisualPosition(_barrelPos);
                _barrelRenderer.sortingOrder = _grid.iso.SortingOrder(_barrelPos, 1);
            }
            if (_selection != null)
                PositionSelection(_selectionPos);
            ApplyThrowRangePreviewView();
            _hubWorld.ApplyView(_grid.iso, VisualPosition);

            RefreshFloorVisibility();
        }

        private void ConfigureCamera(Camera camera)
        {
            if (camera == null) return;

            _configuredCamera = camera;
            camera.orthographic = true;
            OrthographicCameraFrame frame;
            if (TryGetVerticalLookCameraFrame(
                    camera.aspect,
                    out Vector2 verticalCenter,
                    out float verticalSize))
            {
                frame = OrthographicCameraFraming.Follow(
                    verticalCenter,
                    hubMode,
                    viewMode,
                    verticalSize,
                    debugCameraSize);
            }
            else if (!TryGetB2HeroRoomCameraFrame(camera.aspect, out frame))
            {
                Vector2 center = new Vector2(0f, -1.65f);
                if (_playerState != null && (hubMode || viewMode == DungeonViewMode.Play))
                {
                    Vector3 playerWorld = _grid.GridToWorld(_playerState.Position);
                    center = new Vector2(playerWorld.x, playerWorld.y);
                }

                frame = OrthographicCameraFraming.Follow(
                    center,
                    hubMode,
                    viewMode,
                    playCameraSize,
                    debugCameraSize);
            }

            frame = ApplyCameraLook(frame);

            camera.orthographicSize = frame.Size;
            camera.transform.position = new Vector3(frame.Center.x, frame.Center.y, -10f);
            SyncDungeonAtmosphereBackdropCenter(camera);
            _lastCameraAspect = camera.aspect;
            camera.backgroundColor = hubMode
                ? new Color32(9, 7, 14, 255)
                : Palette.Void;
            camera.clearFlags = CameraClearFlags.SolidColor;
        }

        private bool TryGetB2HeroRoomCameraFrame(
            float aspect,
            out OrthographicCameraFrame frame)
        {
            frame = default;
            if (hubMode ||
                viewMode != DungeonViewMode.Play ||
                _b2HeroRoomLayout == null ||
                _playerState == null ||
                _grid == null ||
                _dungeon == null ||
                !_b2HeroRoomLayout.ContainsRoomCell(_playerState.Position))
                return false;

            var projectedCenters = new List<Vector2>(_b2HeroRoomLayout.RoomCells.Count + 1);
            foreach (GridPos roomCell in _b2HeroRoomLayout.RoomCells)
            {
                if (_dungeon.Height.FloorIndex(roomCell.elevation) != _activeFloorIndex)
                    continue;

                Vector3 world = _grid.GridToWorld(roomCell);
                projectedCenters.Add(new Vector2(world.x, world.y));
            }

            if (TryFindClosestB2RoomDoor(out GridPos door))
            {
                Vector3 world = _grid.GridToWorld(door);
                projectedCenters.Add(new Vector2(world.x, world.y));
            }

            if (projectedCenters.Count == 0) return false;
            frame = OrthographicCameraFraming.FitProjectedBounds(
                projectedCenters,
                aspect,
                B2RoomHudSafeViewport,
                B2RoomCameraPadding,
                playCameraSize);
            return true;
        }

        private bool TryFindClosestB2RoomDoor(out GridPos closestDoor)
        {
            closestDoor = default;
            bool found = false;
            int closestDistance = int.MaxValue;
            foreach (KeyValuePair<GridPos, TileData> pair in _grid.Map.All())
            {
                TileKind kind = pair.Value.kind;
                if ((kind != TileKind.DoorClosed && kind != TileKind.DoorOpen) ||
                    _dungeon.Height.FloorIndex(pair.Key.elevation) != _activeFloorIndex)
                    continue;

                int distance = int.MaxValue;
                foreach (GridPos roomCell in _b2HeroRoomLayout.RoomCells)
                    distance = Mathf.Min(distance, pair.Key.ManhattanTo(roomCell));

                if (found &&
                    (distance > closestDistance ||
                     (distance == closestDistance && !ComesBefore(pair.Key, closestDoor))))
                    continue;

                found = true;
                closestDistance = distance;
                closestDoor = pair.Key;
            }

            return found;
        }

        private static bool ComesBefore(GridPos candidate, GridPos current)
        {
            if (candidate.x != current.x) return candidate.x < current.x;
            if (candidate.y != current.y) return candidate.y < current.y;
            return candidate.elevation < current.elevation;
        }
    }
}
