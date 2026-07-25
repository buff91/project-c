using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    public partial class IsoPrototypeDemo
    {

        public void RotateView(int direction)
        {
            if (_grid == null || _dungeon == null || _resolvingAction || direction == 0)
                return;

            _grid.iso.RotateView(direction);
            ApplyViewToVisuals();
            ConfigureCamera(Camera.main);
            ViewRotationChanged?.Invoke(_grid.iso.viewQuarterTurns);
            Debug.Log($"[View] 아이소 시점 회전: {_grid.iso.viewQuarterTurns * 90}°");
        }

        public void ToggleViewMode()
        {
            viewMode = viewMode == DungeonViewMode.Play
                ? DungeonViewMode.DebugAll
                : DungeonViewMode.Play;
            ApplyViewToVisuals();
            ConfigureCamera(Camera.main);
            ViewModeChanged?.Invoke(viewMode);
            Debug.Log($"[View] 던전 표시 모드: {viewMode}");
        }

        public void ToggleCombatMode()
        {
            combatMode = combatMode == CombatActionMode.Melee
                ? CombatActionMode.Ranged
                : CombatActionMode.Melee;
            CombatModeChanged?.Invoke(combatMode);
            InteractionFeedback?.Invoke(combatMode == CombatActionMode.Melee
                ? "MELEE: 적을 탭해 접근 공격"
                : $"RANGED: 사거리 {rangedAttackRange}, 문/벽에 차단");
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

            foreach (KeyValuePair<SpriteRenderer, GridPos> pair in _hubPropPositions)
            {
                if (pair.Key == null) continue;
                pair.Key.transform.position = VisualPosition(pair.Value);
                pair.Key.sortingOrder = _grid.iso.SortingOrder(pair.Value, 1);
            }
            foreach (KeyValuePair<SpriteRenderer, GridPos> pair in _hubLightPositions)
            {
                if (pair.Key == null) continue;
                pair.Key.transform.position = VisualPosition(pair.Value);
                pair.Key.sortingOrder = _grid.iso.SortingOrder(pair.Value, -1);
            }

            RefreshFloorVisibility();
        }

        private void ConfigureCamera(Camera camera)
        {
            if (camera == null) return;

            _configuredCamera = camera;
            camera.orthographic = true;
            if (hubMode && TryGetHubCameraFrame(camera.aspect, out OrthographicCameraFrame hubFrame))
            {
                camera.orthographicSize = hubFrame.Size;
                camera.transform.position = new Vector3(hubFrame.Center.x, hubFrame.Center.y, -10f);
            }
            else
            {
                camera.orthographicSize = viewMode == DungeonViewMode.DebugAll
                    ? debugCameraSize
                    : playCameraSize;
                if (viewMode == DungeonViewMode.Play && _playerState != null)
                {
                    Vector3 playerWorld = _grid.GridToWorld(_playerState.Position);
                    camera.transform.position = new Vector3(playerWorld.x, playerWorld.y, -10f);
                }
                else
                {
                    camera.transform.position = new Vector3(0f, -1.65f, -10f);
                }
            }
            _lastCameraAspect = camera.aspect;
            camera.backgroundColor = hubMode
                ? new Color32(9, 7, 14, 255)
                : Palette.Void;
            camera.clearFlags = CameraClearFlags.SolidColor;
        }

        private bool TryGetHubCameraFrame(float aspect, out OrthographicCameraFrame frame)
        {
            frame = default;
            if (_grid == null || _grid.Map.Count == 0 || aspect <= 0f) return false;

            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;
            foreach (KeyValuePair<GridPos, TileData> pair in _grid.Map.All())
            {
                Vector2 world = _grid.iso.GridToWorld(pair.Key);
                minX = Mathf.Min(minX, world.x);
                maxX = Mathf.Max(maxX, world.x);
                minY = Mathf.Min(minY, world.y);
                maxY = Mathf.Max(maxY, world.y);
            }

            // 최소 크기는 **던전 카메라 크기**다 — 값을 따로 들면 또 흘러내린다.
            // 예전엔 전용 필드(2.55)를 썼고, 그래서 허브가 던전보다 1.4배 확대돼 보였다.
            // 플로우를 오갈 때마다 배율이 바뀌면 "같은 세계"로 안 읽힌다.
            //
            // Fit 이 max(minimumSize, halfHeight, halfWidth/aspect) 이므로 결과는 자동으로
            // "던전과 같은 크기, 다만 캠프가 화면 밖으로 나갈 때만 그만큼 물러남"이 된다.
            // PC 가로(13×9 캠프)에서는 최소값이 지배하므로 던전과 정확히 같다.
            frame = OrthographicCameraFraming.Fit(
                minX, maxX, minY, maxY,
                aspect,
                playCameraSize,
                hubCameraHorizontalPadding,
                hubCameraVerticalPadding);
            return true;
        }
    }
}
