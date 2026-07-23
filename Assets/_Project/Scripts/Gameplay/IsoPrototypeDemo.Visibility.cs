using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// IsoPrototypeDemo의 표시 상태 갱신부.
    /// PLAY FOV/DEBUG ALL 전환, 수직 포털(계단/구멍) 표시, 후면 벽 재구성,
    /// 플레이어 가림 처리처럼 "무엇을 얼마나 보여줄지"를 담당한다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private void RefreshFloorVisibility()
        {
            if (_dungeon == null) return;

            RecomputeVisibility();
            RefreshDungeonFogBackdrop();

            foreach (var pair in _tileRenderers)
            {
                bool debugVisible = viewMode == DungeonViewMode.DebugAll;
                bool visible = _visibleTiles.Contains(pair.Key);
                bool explored = _exploredTiles.Contains(pair.Key);
                bool vertical = _verticalPreviewTiles.Contains(pair.Key);
                int tileFloor = _dungeon.Height.FloorIndex(pair.Key.elevation);
                TileData tileData = _grid.Map.Get(pair.Key);
                pair.Value.sprite = GetTileSprite(tileData.kind, pair.Key);
                pair.Value.enabled = FloorVisibilityRules.ShouldRenderWorldGeometry(
                    debugVisible,
                    tileFloor,
                    _activeFloorIndex,
                    visible,
                    explored,
                    vertical);
                float alpha = VisibilityAlpha(pair.Key);
                Color tint = ElevationTint(pair.Key);
                // 원소 상태 타일은 색으로 보여준다: 기름=갈색조, 물=청색조. 높이 틴트를 곱한다.
                Color baseColor = tileData.oiled
                    ? new Color(0.74f, 0.64f, 0.36f)
                    : tileData.wet
                        ? new Color(0.55f, 0.72f, 0.95f)
                        : Color.white;
                pair.Value.color = new Color(
                    baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, alpha);
                pair.Value.transform.position = VisualPosition(pair.Key);
            }

            // 가시성과 함께 높이 딤 틴트도 갱신돼야 하므로 개별 갱신 경로를 그대로 태운다.
            foreach (EnemyAgent enemy in _enemies)
            {
                if (enemy.Root == null) continue;
                ApplyEnemyVisuals(enemy);
            }

            foreach (ItemAgent item in _items)
            {
                if (item.Root == null || item.Collected) continue;
                SetSpriteHierarchyVisible(
                    item.Root,
                    _dungeon.Height.FloorIndex(item.Spawn.Position.elevation) == _activeFloorIndex &&
                    (viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(item.Spawn.Position)));
                Color itemTint = ElevationTint(item.Spawn.Position);
                item.Renderer.color = new Color(itemTint.r, itemTint.g, itemTint.b, 1f);
            }

            if (_barrelRenderer != null && _barrelExploded)
            {
                SetSpriteHierarchyVisible(_barrel, false);
            }
            else if (_barrelRenderer != null)
            {
                bool active = _dungeon.Height.FloorIndex(_barrelPos.elevation) == _activeFloorIndex;
                bool visible = _visibleTiles.Contains(_barrelPos) || _verticalPreviewTiles.Contains(_barrelPos);
                _barrelRenderer.enabled = viewMode == DungeonViewMode.DebugAll || visible;
                _barrelRenderer.color = new Color(
                    1f,
                    1f,
                    1f,
                    viewMode == DungeonViewMode.DebugAll
                        ? active ? 1f : debugAdjacentAlpha
                        : _visibleTiles.Contains(_barrelPos) ? 1f : verticalPreviewAlpha);
                _barrel.transform.position = VisualPosition(_barrelPos);
            }

            RebuildRearWalls();
            RebuildVerticalShafts();
            RebuildElevationEdgeMarkers();
            RefreshVerticalLandmarks();
            DetectNewVerticalRoute();
            VerticalContextChanged?.Invoke();
        }

        private sealed class VerticalLandmarkAgent
        {
            public GridPos Anchor;
            public GridPos? Destination;
            public TileKind Kind;
            public GameObject Root;
            public SpriteRenderer Renderer;
            public TextMesh Label;
        }

        /// <summary>
        /// 색 테두리 대신 실루엣으로 읽히는 월드 오브젝트를 만든다.
        /// 계단은 발판, 사다리는 세워진 레일, 층 전환은 어두운 아치, Hole은 깨진 구멍이다.
        /// </summary>
        private void CreateVerticalLandmarks()
        {
            if (hubMode || _visualRoot == null) return;

            foreach (var pair in _grid.Map.All())
            {
                GridPos anchor = pair.Key;
                TileKind kind = pair.Value.kind;
                GridPos? destination = null;
                Sprite sprite;
                float labelHeight;

                switch (kind)
                {
                    case TileKind.Stairs:
                        if (StairTopology.TryGetHigherLanding(_grid.Map, anchor, out GridPos landing))
                            destination = landing;
                        sprite = GetLocalStairLandmarkSprite();
                        labelHeight = 0.48f;
                        break;
                    case TileKind.Ladder:
                    {
                        IReadOnlyList<GridPos> links = _grid.Map.LinksFrom(anchor);
                        if (links.Count == 0 || links[0].elevation < anchor.elevation)
                            continue; // 한 쌍의 낮은 끝에서만 세워진 사다리를 하나 만든다.
                        destination = links[0];
                        sprite = GetLadderLandmarkSprite();
                        labelHeight = 0.58f;
                        break;
                    }
                    case TileKind.StairsUp:
                    case TileKind.StairsDown:
                    {
                        IReadOnlyList<GridPos> links = _grid.Map.LinksFrom(anchor);
                        if (links.Count > 0) destination = links[0];
                        sprite = GetFloorTransitionLandmarkSprite(kind == TileKind.StairsDown);
                        labelHeight = 1.02f;
                        break;
                    }
                    case TileKind.Hole:
                        destination = _grid.Map.FindLandingBelow(anchor, BottomElevation);
                        sprite = GetHoleLandmarkSprite();
                        labelHeight = 0.62f;
                        break;
                    default:
                        continue;
                }

                var root = new GameObject($"Vertical Landmark {kind} {anchor}");
                root.transform.SetParent(_visualRoot, false);
                var art = new GameObject("Route Art");
                art.transform.SetParent(root.transform, false);
                var renderer = art.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = _grid.iso.SortingOrder(anchor, 1);

                var landmark = new VerticalLandmarkAgent
                {
                    Anchor = anchor,
                    Destination = destination,
                    Kind = kind,
                    Root = root,
                    Renderer = renderer,
                    Label = CreateVerticalLandmarkLabel(root.transform, labelHeight)
                };
                _verticalLandmarks.Add(landmark);
                UpdateVerticalLandmarkTransform(landmark);
            }
        }

        private TextMesh CreateVerticalLandmarkLabel(Transform parent, float localHeight)
        {
            var labelObject = new GameObject("Route Label");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = Vector3.up * localHeight;
            var label = labelObject.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font = font;
            label.fontSize = 48;
            label.characterSize = 0.032f;
            label.fontStyle = FontStyle.Bold;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color32(255, 229, 154, 255);
            MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
            renderer.material = font.material;
            renderer.sortingOrder = 31990;
            return label;
        }

        private void UpdateVerticalLandmarkTransform(VerticalLandmarkAgent landmark)
        {
            Vector3 position = VisualPosition(landmark.Anchor);
            landmark.Renderer.transform.localScale = Vector3.one;
            if (landmark.Kind == TileKind.Ladder && landmark.Destination.HasValue)
            {
                Vector3 destination = VisualPosition(landmark.Destination.Value);
                position = Vector3.Lerp(position, destination, 0.5f) + Vector3.up * 0.02f;
                float scaleY = VerticalTraversalRules.LadderScaleY(
                    landmark.Destination.Value.elevation - landmark.Anchor.elevation,
                    _grid.iso.elevationStep,
                    _grid.iso.tileHeight,
                    landmark.Renderer.sprite.bounds.size.y);
                landmark.Renderer.transform.localScale = new Vector3(0.82f, scaleY, 1f);
            }
            else
            {
                position += Vector3.up * 0.03f;
            }

            landmark.Root.transform.position = position;
            landmark.Renderer.sortingOrder = _grid.iso.SortingOrder(landmark.Anchor, 1);
        }

        private void RefreshVerticalLandmarks()
        {
            foreach (VerticalLandmarkAgent landmark in _verticalLandmarks)
            {
                UpdateVerticalLandmarkTransform(landmark);
                bool anchorVisible = _visibleTiles.Contains(landmark.Anchor);
                bool anchorPreview = _verticalPreviewTiles.Contains(landmark.Anchor);
                bool destinationVisible = landmark.Destination.HasValue &&
                                          _visibleTiles.Contains(landmark.Destination.Value);
                bool destinationPreview = landmark.Destination.HasValue &&
                                          _verticalPreviewTiles.Contains(landmark.Destination.Value);
                bool anchorOnActiveFloor =
                    _dungeon.Height.FloorIndex(landmark.Anchor.elevation) == _activeFloorIndex;
                bool destinationOnActiveFloor =
                    landmark.Destination.HasValue &&
                    _dungeon.Height.FloorIndex(landmark.Destination.Value.elevation) ==
                    _activeFloorIndex;
                bool activeRoute = landmark.Kind == TileKind.Hole
                    ? anchorOnActiveFloor || destinationOnActiveFloor
                    : anchorOnActiveFloor;
                bool show = viewMode == DungeonViewMode.DebugAll
                    ? activeRoute && IsDebugLandmarkReachable(landmark)
                    : landmark.Kind == TileKind.Hole
                        ? anchorVisible || anchorPreview || destinationVisible || destinationPreview
                        : activeRoute && (anchorVisible || destinationVisible);
                landmark.Root.SetActive(show);
                if (!show) continue;

                bool viewedFromBelow =
                    _dungeon.Height.FloorIndex(landmark.Anchor.elevation) > _activeFloorIndex;
                string destinationLabel = VerticalLandmarkDestinationLabel(
                    landmark, viewedFromBelow);
                if (VerticalRouteCue.TryCreate(
                        landmark.Kind, viewedFromBelow, destinationLabel, out VerticalRouteCue cue))
                    landmark.Label.text = cue.WorldLabel;

                float alpha = viewMode == DungeonViewMode.DebugAll ||
                              anchorVisible || destinationVisible
                    ? 1f
                    : verticalPreviewAlpha;
                landmark.Renderer.color = new Color(1f, 1f, 1f, alpha);
                Color labelColor = LandmarkLabelColor(cue.Role);
                labelColor.a = alpha;
                landmark.Label.color = labelColor;
            }
        }

        private bool IsDebugLandmarkReachable(VerticalLandmarkAgent landmark)
        {
            if (landmark.Kind == TileKind.Hole) return true;
            if (_playerState == null) return false;
            if (_playerPos == landmark.Anchor) return true;
            return GridPathfinder.FindPath(_grid.Map, _playerPos, landmark.Anchor).Count > 0;
        }

        private static Color LandmarkLabelColor(VerticalRouteRole role)
        {
            switch (role)
            {
                case VerticalRouteRole.Ladder:
                    return new Color32(255, 205, 83, 255);
                case VerticalRouteRole.FloorUp:
                case VerticalRouteRole.FloorDown:
                    return new Color32(255, 153, 64, 255);
                case VerticalRouteRole.OpeningUp:
                case VerticalRouteRole.OpeningDown:
                    return new Color32(102, 230, 238, 255);
                default:
                    return new Color32(134, 225, 203, 255);
            }
        }

        private string VerticalLandmarkDestinationLabel(
            VerticalLandmarkAgent landmark,
            bool viewedFromBelow)
        {
            if (landmark.Kind == TileKind.Stairs || landmark.Kind == TileKind.Ladder)
                return null;

            if (landmark.Kind == TileKind.Hole && viewedFromBelow)
                return FloorLabel(_dungeon.Height.FloorIndex(landmark.Anchor.elevation));

            if (landmark.Destination.HasValue)
                return FloorLabel(_dungeon.Height.FloorIndex(landmark.Destination.Value.elevation));

            if (landmark.Kind == TileKind.StairsDown &&
                _dungeon.Height.FloorIndex(landmark.Anchor.elevation) == _dungeon.BottomFloorIndex)
                return _stageIndex < stageCount ? "NEXT" : "EXIT";

            return "--";
        }

        /// <summary>수직 수단이 시야에 처음 들어온 그 프레임에만 설명을 보낸다.</summary>
        private void DetectNewVerticalRoute()
        {
            if (hubMode || viewMode == DungeonViewMode.DebugAll || _playerState == null)
                return;

            VerticalLandmarkAgent nearest = null;
            VerticalRouteCue nearestCue = default;
            float nearestDistance = float.MaxValue;

            foreach (VerticalLandmarkAgent landmark in _verticalLandmarks)
            {
                GridPos focus = landmark.Anchor;
                bool viewedFromBelow =
                    _dungeon.Height.FloorIndex(landmark.Anchor.elevation) > _activeFloorIndex;
                if (viewedFromBelow && landmark.Destination.HasValue)
                    focus = landmark.Destination.Value;

                bool seen = _visibleTiles.Contains(focus) ||
                            (landmark.Kind == TileKind.Hole &&
                             (_verticalPreviewTiles.Contains(landmark.Anchor) ||
                              (landmark.Destination.HasValue &&
                               _verticalPreviewTiles.Contains(landmark.Destination.Value))));
                if (!seen) continue;

                string destination = VerticalLandmarkDestinationLabel(landmark, viewedFromBelow);
                if (!VerticalRouteCue.TryCreate(
                        landmark.Kind, viewedFromBelow, destination, out VerticalRouteCue cue) ||
                    _discoveredVerticalRoutes.Contains(cue.Role))
                    continue;

                float distance = _playerPos.ManhattanTo(focus);
                if (distance >= nearestDistance) continue;
                nearest = landmark;
                nearestCue = cue;
                nearestDistance = distance;
            }

            if (nearest == null) return;
            _discoveredVerticalRoutes.Add(nearestCue.Role);
            VerticalRouteDiscovered?.Invoke(nearestCue);
            FloatingText?.Show(
                nearest.Root.transform.position + Vector3.up * 0.35f,
                nearestCue.WorldLabel,
                FloatingTextKind.Alert);
        }

        /// <summary>
        /// 미탐색 방 모양을 노출하지 않고 현재 층의 생성 가능 영역만 어두운 다이아몬드로 표시한다.
        /// 이 배경은 시각 구분 전용이며 타일/콜라이더/입력 대상이 아니다.
        /// </summary>
        private void RefreshDungeonFogBackdrop()
        {
            bool shouldShow = showDungeonFogBackdrop && !hubMode &&
                              viewMode == DungeonViewMode.Play && _visualRoot != null;
            if (!shouldShow)
            {
                if (_dungeonFogBackdrop != null) _dungeonFogBackdrop.enabled = false;
                return;
            }

            if (_dungeonFogBackdrop == null)
            {
                var backdrop = new GameObject("Dungeon Fog Backdrop");
                backdrop.transform.SetParent(_visualRoot, false);
                _dungeonFogBackdrop = backdrop.AddComponent<SpriteRenderer>();
                _dungeonFogBackdrop.sortingOrder = -100000;
            }

            int baseElevation = _dungeon.Height.Elevation(_activeFloorIndex);
            DungeonFogBackdropFrame frame = DungeonFogBackdropLayout.Calculate(
                _grid.iso,
                roomSize,
                roomSize,
                baseElevation);
            _dungeonFogBackdrop.sprite = GetDungeonFogBackdropSprite();
            _dungeonFogBackdrop.transform.position = new Vector3(frame.Center.x, frame.Center.y, 0f);

            // 생성 스프라이트 기본 크기는 2×1 world unit. 프레임에 맞게 균등 확장한다.
            _dungeonFogBackdrop.transform.localScale = new Vector3(
                frame.Width * 0.5f,
                frame.Height,
                1f);
            _dungeonFogBackdrop.enabled = true;
        }

        private Transform _elevationMarkerRoot;

        /// <summary>
        /// 발판 계단의 걸어서 통과 가능한 경계만 얇게 강조한다.
        /// 사다리·층 전환·개구부는 별도 실루엣 오브젝트가 역할을 설명한다.
        /// </summary>
        private void RebuildElevationEdgeMarkers()
        {
            if (_elevationMarkerRoot != null)
            {
                if (Application.isPlaying) Destroy(_elevationMarkerRoot.gameObject);
                else DestroyImmediate(_elevationMarkerRoot.gameObject);
            }
            if (hubMode || viewMode == DungeonViewMode.DebugAll) return;

            var root = new GameObject("Elevation Edge Markers");
            root.hideFlags = HideFlags.DontSaveInEditor;
            root.transform.SetParent(_visualRoot, false);
            _elevationMarkerRoot = root.transform;

            foreach (var pair in _grid.Map.All())
            {
                TileKind kind = pair.Value.kind;
                if (kind != TileKind.Stairs) continue;
                if (_dungeon.Height.FloorIndex(pair.Key.elevation) != _activeFloorIndex) continue;
                if (!_visibleTiles.Contains(pair.Key) && !_exploredTiles.Contains(pair.Key)) continue;

                var marker = new GameObject($"Vertical Marker {kind} {pair.Key}");
                marker.transform.SetParent(_elevationMarkerRoot, false);
                marker.transform.position = VisualPosition(pair.Key) + Vector3.up * 0.02f;
                var renderer = marker.AddComponent<SpriteRenderer>();
                renderer.sprite = visualCatalog != null && visualCatalog.selection != null
                    ? visualCatalog.selection
                    : GetSelectionSprite();
                renderer.sortingOrder = _grid.iso.SortingOrder(pair.Key, 0);
                Color markerColor = new Color(0.33f, 0.83f, 0.77f);
                markerColor.a = _visibleTiles.Contains(pair.Key) ? 0.5f : 0.22f;
                renderer.color = markerColor;
            }
        }

        private void RebuildVerticalShafts()
        {
            if (_shaftRoot != null)
            {
                if (Application.isPlaying) Destroy(_shaftRoot.gameObject);
                else DestroyImmediate(_shaftRoot.gameObject);
            }

            var root = new GameObject("Vertical Connections");
            root.hideFlags = HideFlags.DontSaveInEditor;
            root.transform.SetParent(_visualRoot, false);
            _shaftRoot = root.transform;

            if (viewMode == DungeonViewMode.DebugAll)
            {
                foreach (DungeonFloorInfo floor in _dungeon.Floors)
                {
                    CreateHoleShaft(floor.Hole);
                }
                return;
            }

            // PLAY에서는 실제 개구부만 층 사이를 시각적으로 연결한다.
            // StairsUp/Down은 활성 던전 층을 교체하는 전환구이므로 투시 샤프트를 만들지 않는다.
            foreach (var pair in _grid.Map.All())
            {
                if (pair.Value.kind != TileKind.Hole) continue;
                VerticalOpeningView view = VerticalOpeningRules.ViewFromFloor(
                    _grid.Map,
                    _dungeon.Height,
                    _activeFloorIndex,
                    pair.Key,
                    BottomElevation,
                    _visibleTiles.Contains,
                    out GridPos landing);
                if (view != VerticalOpeningView.None)
                    CreateVerticalShaft(pair.Key, landing, hole: true);
            }
        }

        private void CreateHoleShaft(GridPos? hole)
        {
            if (!hole.HasValue) return;
            int minElevation = _dungeon.Height.Elevation(_dungeon.BottomFloorIndex);
            GridPos? landing = _grid.Map.FindLandingBelow(hole.Value, minElevation);
            if (landing.HasValue)
                CreateVerticalShaft(hole.Value, landing.Value, hole: true);
        }

        private void CreateVerticalShaft(GridPos from, GridPos to, bool hole)
        {
            Vector3 start = VisualPosition(from);
            Vector3 end = VisualPosition(to);
            float distance = Mathf.Max(0.35f, Mathf.Abs(end.y - start.y));

            var shaft = new GameObject(hole ? "Hole Drop Shaft" : "Stair Connection Shaft");
            shaft.transform.SetParent(_shaftRoot, false);
            shaft.transform.position = Vector3.Lerp(start, end, 0.5f) + Vector3.up * 0.05f;
            var renderer = shaft.AddComponent<SpriteRenderer>();
            renderer.sprite = GetShaftSprite(hole);
            renderer.sortingOrder = 29980;
            renderer.color = new Color(1f, 1f, 1f, viewMode == DungeonViewMode.DebugAll ? 0.72f : 0.9f);
            shaft.transform.localScale = new Vector3(1.15f, distance, 1f);

            CreateShaftEndpoint(from, hole, arrival: false);
            CreateShaftEndpoint(to, hole, arrival: true);
        }

        private void CreateShaftEndpoint(GridPos pos, bool hole, bool arrival)
        {
            var endpoint = new GameObject(arrival ? "Shaft Arrival" : "Shaft Entrance");
            endpoint.transform.SetParent(_shaftRoot, false);
            endpoint.transform.position = VisualPosition(pos) + Vector3.up * 0.035f;
            var renderer = endpoint.AddComponent<SpriteRenderer>();
            renderer.sprite = GetShaftEndpointSprite(hole, arrival);
            renderer.sortingOrder = 29979;
            renderer.color = new Color(1f, 1f, 1f, arrival ? 0.72f : 0.95f);
        }

        private void RecomputeVisibility()
        {
            _visibleTiles.Clear();
            _verticalPreviewTiles.Clear();

            // 허브 캠프는 안개 없이 전부 보인다.
            if (hubMode)
            {
                foreach (var pair in _grid.Map.All())
                {
                    _visibleTiles.Add(pair.Key);
                    _exploredTiles.Add(pair.Key);
                }
                return;
            }

            if (viewMode == DungeonViewMode.DebugAll) return;

            GridPos origin;
            if (_playerState != null)
                origin = _playerState.Position;
            else
            {
                _dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo floor);
                origin = floor.Entry;
            }

            int minElevation = _dungeon.Height.Elevation(_activeFloorIndex);
            int maxElevation = minElevation + _dungeon.Height.ElevationsPerFloor - 1;
            foreach (GridPos pos in GridVisibility.Compute(
                         _grid.Map,
                         origin,
                         minElevation,
                         maxElevation,
                         fieldOfViewRadius))
            {
                _visibleTiles.Add(pos);
                _exploredTiles.Add(pos);
            }

            // 실제 Hole만 양방향 시야 포털이다.
            // 위에서는 착지점 주변을 내려다보고, 아래에서는 같은 개구부 주변을 올려다본다.
            foreach (var pair in _grid.Map.All())
            {
                if (pair.Value.kind != TileKind.Hole) continue;
                VerticalOpeningView view = VerticalOpeningRules.ViewFromFloor(
                    _grid.Map,
                    _dungeon.Height,
                    _activeFloorIndex,
                    pair.Key,
                    BottomElevation,
                    _visibleTiles.Contains,
                    out GridPos landing);
                if (view == VerticalOpeningView.Downward)
                    AddVerticalWindow(landing, _dungeon.Height.FloorIndex(landing.elevation));
                else if (view == VerticalOpeningView.Upward)
                    AddVerticalWindow(pair.Key, _dungeon.Height.FloorIndex(pair.Key.elevation));
            }
        }

        private string BuildVerticalHintLabel()
        {
            if (_dungeon == null) return "EXPLORE TO FIND VERTICAL ROUTES";
            if (viewMode == DungeonViewMode.DebugAll) return "DEBUG: ALL FLOORS VISIBLE";
            if (!_dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo floor))
                return "EXPLORE TO FIND VERTICAL ROUTES";

            var hints = new List<string>(4);
            TileKind? playerTile = _grid.Map.Get(_playerPos)?.kind;
            if (playerTile == TileKind.Ladder)
                return "사다리 위 · 캐릭터 탭 또는 SPACE로 오르내리기";
            else if (playerTile == TileKind.StairsUp)
                return $"{AboveFloorLabel} 되돌아가기 · 캐릭터 탭 또는 SPACE";
            else if (playerTile == TileKind.StairsDown)
                return $"{BelowFloorLabel} 되돌아가기 · 캐릭터 탭 또는 SPACE";

            bool localStairsVisible = false;
            bool ladderVisible = false;
            foreach (var pair in _grid.Map.All())
            {
                if (_dungeon.Height.FloorIndex(pair.Key.elevation) != _activeFloorIndex ||
                    !_visibleTiles.Contains(pair.Key))
                    continue;
                localStairsVisible |= pair.Value.kind == TileKind.Stairs;
                ladderVisible |= pair.Value.kind == TileKind.Ladder;
            }
            if (localStairsVisible)
                hints.Add("발판 계단은 그대로 걸어서 통과");
            if (ladderVisible && playerTile != TileKind.Ladder)
                hints.Add("세워진 사다리 앞에 서면 오르기 가능");

            if (floor.UpStairs.HasValue && _visibleTiles.Contains(floor.UpStairs.Value))
                hints.Add($"{AboveFloorLabel} 상층 계단 · 밟으면 즉시 이동");
            if (floor.DownStairs.HasValue && _visibleTiles.Contains(floor.DownStairs.Value))
                hints.Add($"{BelowFloorLabel} 하층 계단 · 밟으면 즉시 이동");

            bool downwardOpeningAdded = false;
            bool upwardOpeningAdded = false;
            foreach (var pair in _grid.Map.All())
            {
                if (pair.Value.kind != TileKind.Hole) continue;
                VerticalOpeningView openingView = VerticalOpeningRules.ViewFromFloor(
                    _grid.Map,
                    _dungeon.Height,
                    _activeFloorIndex,
                    pair.Key,
                    BottomElevation,
                    _visibleTiles.Contains,
                    out GridPos landing);
                if (openingView == VerticalOpeningView.Downward && !downwardOpeningAdded)
                {
                    hints.Add(
                        $"바닥 구멍 탭: 아래로 뛰어내리기 → " +
                        $"{FloorLabel(_dungeon.Height.FloorIndex(landing.elevation))}");
                    downwardOpeningAdded = true;
                }
                else if (openingView == VerticalOpeningView.Upward && !upwardOpeningAdded)
                {
                    hints.Add(
                        $"천장 개구부: 위층 올려다보기 → " +
                        $"{FloorLabel(_dungeon.Height.FloorIndex(pair.Key.elevation))}");
                    upwardOpeningAdded = true;
                }
            }

            return hints.Count > 0
                ? string.Join("\n", hints)
                : "EXPLORE TO FIND VERTICAL ROUTES";
        }

        private void AddVerticalWindow(GridPos center, int floorIndex)
        {
            foreach (var pair in _grid.Map.All())
            {
                if (_dungeon.Height.FloorIndex(pair.Key.elevation) != floorIndex) continue;
                if (Mathf.Abs(pair.Key.x - center.x) <= verticalPreviewRadius &&
                    Mathf.Abs(pair.Key.y - center.y) <= verticalPreviewRadius)
                    _verticalPreviewTiles.Add(pair.Key);
            }
        }

        public int MinimapSize => roomSize;

        /// <summary>
        /// HUD 미니맵용 픽셀 채우기: 활성 층의 안개 상태(시야/탐색)를 그대로 반영한다.
        /// 좌표 회전은 적용하지 않는다(맵은 항상 북쪽 고정). true = 그릴 데이터 있음.
        /// </summary>
        public bool FillMinimap(Color32[] pixels, int width, int height)
        {
            if (_dungeon == null || _grid == null || pixels == null || pixels.Length < width * height)
                return false;

            var empty = new Color32(0, 0, 0, 0);
            for (int i = 0; i < width * height; i++)
                pixels[i] = empty;

            bool debug = viewMode == DungeonViewMode.DebugAll;
            foreach (var pair in _grid.Map.All())
            {
                GridPos pos = pair.Key;
                if (_dungeon.Height.FloorIndex(pos.elevation) != _activeFloorIndex) continue;
                if (pos.x < 0 || pos.x >= width || pos.y < 0 || pos.y >= height) continue;

                bool visible = debug || _visibleTiles.Contains(pos);
                if (!visible && !_exploredTiles.Contains(pos)) continue;

                pixels[pos.y * width + pos.x] = MinimapTileColor(pair.Value.kind, visible);
            }

            foreach (ItemAgent item in _items)
            {
                GridPos pos = item.Spawn.Position;
                if (item.Collected || !_visibleTiles.Contains(pos) && !debug) continue;
                if (_dungeon.Height.FloorIndex(pos.elevation) != _activeFloorIndex) continue;
                if (pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height)
                    pixels[pos.y * width + pos.x] = new Color32(104, 200, 110, 255);
            }

            foreach (EnemyAgent enemy in _enemies)
            {
                GridPos pos = enemy.State.Position;
                if (!enemy.State.IsAlive || !_visibleTiles.Contains(pos) && !debug) continue;
                if (_dungeon.Height.FloorIndex(pos.elevation) != _activeFloorIndex) continue;
                if (pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height)
                    pixels[pos.y * width + pos.x] = new Color32(224, 74, 58, 255);
            }

            if (_playerPos.x >= 0 && _playerPos.x < width && _playerPos.y >= 0 && _playerPos.y < height)
                pixels[_playerPos.y * width + _playerPos.x] = new Color32(255, 213, 84, 255);

            return true;
        }

        private static Color32 MinimapTileColor(TileKind kind, bool visible)
        {
            Color32 bright;
            switch (kind)
            {
                case TileKind.StairsUp:
                case TileKind.StairsDown:
                    bright = new Color32(232, 160, 64, 255);
                    break;
                case TileKind.Stairs:
                    bright = new Color32(190, 168, 128, 255);
                    break;
                case TileKind.Ladder:
                    bright = new Color32(238, 185, 67, 255);
                    break;
                case TileKind.Hole:
                    bright = new Color32(64, 170, 190, 255);
                    break;
                case TileKind.DoorClosed:
                case TileKind.DoorOpen:
                    bright = new Color32(158, 108, 56, 255);
                    break;
                case TileKind.WeakFloor:
                    bright = new Color32(140, 128, 92, 255);
                    break;
                case TileKind.Wall:
                    bright = new Color32(54, 44, 34, 255);
                    break;
                default:
                    bright = new Color32(150, 140, 120, 255);
                    break;
            }

            if (visible) return bright;
            return new Color32(
                (byte)(bright.r * 0.42f), (byte)(bright.g * 0.42f), (byte)(bright.b * 0.42f), 255);
        }

        private GridPos FindPreviewPropPosition()
        {
            _dungeon.TryGetFloor(_activeFloorIndex, out DungeonFloorInfo active);
            if (!active.Hole.HasValue || !_dungeon.TryGetFloor(active.FloorIndex - 1, out _))
                return active.Entry;

            GridPos hole = active.Hole.Value;
            int belowFloor = active.FloorIndex - 1;
            int baseElevation = _dungeon.Height.Elevation(belowFloor);
            for (int localHeight = _dungeon.Height.ElevationsPerFloor - 1; localHeight >= 0; localHeight--)
            {
                var candidate = new GridPos(hole.x, hole.y, baseElevation + localHeight);
                if (_grid.Map.IsSolidGround(candidate)) return candidate;
            }

            return _dungeon.TryGetFloor(belowFloor, out DungeonFloorInfo below)
                ? below.Entry
                : active.Entry;
        }

        private static void SetSpriteHierarchyVisible(GameObject root, bool visible)
        {
            foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
                renderer.enabled = visible;
        }

        private void RebuildRearWalls()
        {
            if (_visualRoot == null) return;

            _rearWallRenderers.Clear();

            if (_wallRoot != null)
            {
                if (Application.isPlaying) Destroy(_wallRoot.gameObject);
                else DestroyImmediate(_wallRoot.gameObject);
            }
            _wallRoot = null;
            if (!showRearWalls) return;

            var root = new GameObject("Rear View Walls");
            root.hideFlags = HideFlags.DontSaveInEditor;
            root.transform.SetParent(_visualRoot, false);
            _wallRoot = root.transform;

            GetViewDirections(out _, out _, out Vector2Int backA, out Vector2Int backB);
            foreach (var pair in _grid.Map.All())
            {
                GridPos pos = pair.Key;
                int floor = _dungeon.Height.FloorIndex(pos.elevation);
                if (!pair.Value.IsWalkable)
                    continue;

                if (!FloorVisibilityRules.ShouldRenderWorldGeometry(
                        viewMode == DungeonViewMode.DebugAll,
                        floor,
                        _activeFloorIndex,
                        _visibleTiles.Contains(pos),
                        _exploredTiles.Contains(pos),
                        _verticalPreviewTiles.Contains(pos)))
                    continue;

                if (!HasPlanarTile(pos.x + backA.x, pos.y + backA.y, floor))
                    CreateRearWall(pos, backA, flip: true);
                if (!HasPlanarTile(pos.x + backB.x, pos.y + backB.y, floor))
                    CreateRearWall(pos, backB, flip: false);
            }
        }

        private void CreateRearWall(GridPos pos, Vector2Int outward, bool flip)
        {
            var wall = new GameObject($"Rear Wall {pos} {outward}");
            wall.transform.SetParent(_wallRoot, false);
            Vector3 center = VisualPosition(pos);
            Vector3 outside = VisualPosition(new GridPos(
                pos.x + outward.x,
                pos.y + outward.y,
                pos.elevation));
            wall.transform.position = Vector3.Lerp(center, outside, 0.46f);

            var renderer = wall.AddComponent<SpriteRenderer>();
            bool torch = Mathf.Abs(pos.x * 3 + pos.y + _grid.iso.viewQuarterTurns) % 5 == 0;
            Sprite mapped = visualCatalog != null
                ? visualCatalog.RearWallFor(torch, risesRight: flip)
                : null;
            renderer.sprite = mapped != null ? mapped : GetWallSprite(torch);
            renderer.flipX = mapped == null && flip;
            renderer.sortingOrder = _grid.iso.SortingOrder(pos, -1);
            Color wallTint = ElevationTint(pos);
            renderer.color = new Color(wallTint.r, wallTint.g, wallTint.b, VisibilityAlpha(pos));
            _rearWallRenderers.Add(renderer, pos);
        }

        private void UpdatePlayerOccluders(float deltaTime, bool instant = false)
        {
            if (_playerRenderer == null || _dungeon == null) return;

            Bounds playerBounds = _playerRenderer.bounds;
            playerBounds.Expand(new Vector3(
                playerOcclusionPadding * 2f,
                playerOcclusionPadding * 2f,
                0f));
            int playerSortingOrder = _playerRenderer.sortingOrder;

            foreach (var pair in _tileRenderers)
            {
                SpriteRenderer renderer = pair.Value;
                float baseAlpha = VisibilityAlpha(pair.Key);
                bool occludes = fadePlayerOccluders && renderer.enabled &&
                                (SpriteOcclusion.ShouldFade(
                                     renderer.bounds,
                                     playerBounds,
                                     renderer.sortingOrder,
                                     playerSortingOrder) ||
                                 HigherElevationOverlapsPlayer(pair.Key, renderer.bounds, playerBounds));
                ApplyOcclusionAlpha(renderer, baseAlpha, occludes, deltaTime, instant);
            }

            foreach (var pair in _rearWallRenderers)
            {
                SpriteRenderer renderer = pair.Key;
                if (renderer == null) continue;
                float baseAlpha = VisibilityAlpha(pair.Value);
                bool occludes = fadePlayerOccluders && renderer.enabled &&
                                (SpriteOcclusion.ShouldFade(
                                     renderer.bounds,
                                     playerBounds,
                                     renderer.sortingOrder,
                                     playerSortingOrder) ||
                                 HigherElevationOverlapsPlayer(pair.Value, renderer.bounds, playerBounds));
                ApplyOcclusionAlpha(renderer, baseAlpha, occludes, deltaTime, instant);
            }
        }

        /// <summary>
        /// 겹치면 내 높이가 메인 — 플레이어보다 높은 elevation(같은 층)의 렌더러가
        /// 화면상 플레이어 영역과 겹치면 반투명 대상으로 판정한다.
        /// </summary>
        private bool HigherElevationOverlapsPlayer(GridPos pos, Bounds bounds, Bounds playerBounds)
        {
            if (_playerState == null) return false;
            GridPos player = _playerState.Position;
            if (!_dungeon.Height.SameFloor(pos, player) || pos.elevation <= player.elevation)
                return false;
            return bounds.max.x > playerBounds.min.x && bounds.min.x < playerBounds.max.x &&
                   bounds.max.y > playerBounds.min.y && bounds.min.y < playerBounds.max.y;
        }

        private void ApplyOcclusionAlpha(
            SpriteRenderer renderer,
            float baseAlpha,
            bool occludes,
            float deltaTime,
            bool instant)
        {
            float targetAlpha = occludes
                ? Mathf.Min(baseAlpha, playerOccluderAlpha)
                : baseAlpha;
            Color color = renderer.color;
            color.a = instant
                ? targetAlpha
                : Mathf.MoveTowards(color.a, targetAlpha, playerOccluderFadeSpeed * deltaTime);
            renderer.color = color;
        }

        private bool IsFrontEdge(GridPos pos)
        {
            int floor = _dungeon.Height.FloorIndex(pos.elevation);
            GetViewDirections(out Vector2Int frontA, out Vector2Int frontB, out _, out _);
            return !HasPlanarTile(pos.x + frontA.x, pos.y + frontA.y, floor) ||
                   !HasPlanarTile(pos.x + frontB.x, pos.y + frontB.y, floor);
        }

        private bool HasPlanarTile(int x, int y, int floorIndex)
        {
            int baseElevation = _dungeon.Height.Elevation(floorIndex);
            for (int local = 0; local < _dungeon.Height.ElevationsPerFloor; local++)
            {
                if (_grid.Map.Has(new GridPos(x, y, baseElevation + local)))
                    return true;
            }
            return false;
        }

        private void GetViewDirections(
            out Vector2Int frontA,
            out Vector2Int frontB,
            out Vector2Int backA,
            out Vector2Int backB)
        {
            switch (_grid.iso.viewQuarterTurns)
            {
                case 1:
                    frontA = Vector2Int.up;
                    frontB = Vector2Int.left;
                    break;
                case 2:
                    frontA = Vector2Int.left;
                    frontB = Vector2Int.down;
                    break;
                case 3:
                    frontA = Vector2Int.down;
                    frontB = Vector2Int.right;
                    break;
                default:
                    frontA = Vector2Int.right;
                    frontB = Vector2Int.up;
                    break;
            }

            backA = -frontA;
            backB = -frontB;
        }

        private Vector3 VisualPosition(GridPos pos)
        {
            Vector3 world = _grid.GridToWorld(pos);
            if (_dungeon == null) return world;

            int floor = _dungeon.Height.FloorIndex(pos.elevation);
            if (viewMode == DungeonViewMode.DebugAll)
                world.y += (floor - _activeFloorIndex) * debugFloorSeparation;
            else if (floor != _activeFloorIndex && _verticalPreviewTiles.Contains(pos))
                world.y += (floor - _activeFloorIndex) * playAdjacentFloorSeparation;
            return world;
        }

        private static readonly Color ActiveTint = Color.white;
        private static readonly Color InactiveTint = new Color(0.50f, 0.55f, 0.70f); // 차가운 비활성 톤

        /// <summary>
        /// 내 높이(플레이어와 같은 elevation)만 원색, 그 외(같은 층 다른 높이·다른 층 잔상)는
        /// 진하게 어둡고 차가운 톤 — "내가 상호작용할 수 있는 평면"을 색으로 못박는다.
        /// </summary>
        private Color ElevationTint(GridPos pos)
        {
            if (viewMode == DungeonViewMode.DebugAll || _playerState == null || _dungeon == null)
                return ActiveTint;
            return pos.elevation == _playerState.Position.elevation ? ActiveTint : InactiveTint;
        }


        private float VisibilityAlpha(GridPos pos)
        {
            if (viewMode == DungeonViewMode.DebugAll)
                return _dungeon.Height.FloorIndex(pos.elevation) == _activeFloorIndex
                    ? 1f
                    : debugAdjacentAlpha;
            if (_visibleTiles.Contains(pos)) return 1f;
            if (_verticalPreviewTiles.Contains(pos)) return verticalPreviewAlpha;
            return exploredAlpha;
        }

        /// <summary>
        /// 계단 위 스프라이트는 한 단 위 착지 칸 기준으로 정렬해
        /// 위쪽 바닥이 계단·캐릭터를 잘못 가리지 않게 한다.
        /// </summary>
        private GridPos SortingAnchor(GridPos pos)
        {
            return StairTopology.TryGetHigherLanding(_grid.Map, pos, out GridPos landing)
                ? landing
                : pos;
        }

        private GridPos TileVisualSortingPos(GridPos pos, TileKind kind) =>
            kind == TileKind.Stairs || kind == TileKind.Ladder ? SortingAnchor(pos) : pos;

        private void ApplyPlayerVisualSorting(GridPos pos)
        {
            if (_playerRenderer == null) return;
            _playerRenderer.sortingOrder = _grid.iso.SortingOrder(SortingAnchor(pos), 1);
        }

        private static int TileSortOffset(TileKind kind)
        {
            if (kind == TileKind.DoorClosed || kind == TileKind.DoorOpen) return 0;
            return kind == TileKind.Stairs || kind == TileKind.Ladder ? -1 : -2;
        }
    }
}
