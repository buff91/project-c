using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 중간 탈출구의 월드 표현과 상호작용. 배치 규칙은 Core(<see cref="ExtractionRules"/>)가 갖고,
    /// 여기서는 프롭을 그리고 걸어가 확인 모달을 띄우는 일만 한다.
    /// 실제 정산은 출구와 같은 <see cref="ExtractRun"/> 경로를 쓴다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private readonly List<ExtractionAgent> _extractionPoints = new List<ExtractionAgent>();

        /// <summary>지금 띄운 출구 선택이 중간 탈출구인가 — HUD 문구/버튼이 갈린다.</summary>
        public bool AtExtractionPoint { get; private set; }

        private void ResetExtractionPointsForBuild()
        {
            _extractionPoints.Clear();
            AtExtractionPoint = false;
        }

        private void CreateExtractionPoint(DungeonFloorInfo floor)
        {
            if (hubMode || !floor.ExtractionPoint.HasValue) return;

            GridPos position = floor.ExtractionPoint.Value;
            GameObject root = CreateStandingSprite(
                $"Extraction Point {FloorLabel(floor.FloorIndex)}",
                ActorSprites.GetExtractionPointSprite(),
                position,
                out SpriteRenderer renderer,
                microOffset: 1);
            _extractionPoints.Add(new ExtractionAgent
            {
                FloorIndex = floor.FloorIndex,
                Position = position,
                Root = root,
                Renderer = renderer
            });
        }

        private bool TryGetExtractionPointAt(GridPos position, out ExtractionAgent point)
        {
            foreach (ExtractionAgent candidate in _extractionPoints)
            {
                if (candidate.Position != position) continue;
                point = candidate;
                return true;
            }

            point = null;
            return false;
        }

        private void ApplyExtractionPointView()
        {
            foreach (ExtractionAgent point in _extractionPoints)
            {
                if (point.Root == null || point.Renderer == null) continue;
                point.Root.transform.position = VisualPosition(point.Position);
                point.Renderer.sortingOrder = _grid.iso.SortingOrder(point.Position, 1);
            }
        }

        private void RefreshExtractionPointVisibility()
        {
            if (_dungeon == null) return;

            foreach (ExtractionAgent point in _extractionPoints)
            {
                if (point.Root == null || point.Renderer == null) continue;
                bool onActiveFloor = point.FloorIndex == _activeFloorIndex;
                bool visible = viewMode == DungeonViewMode.DebugAll ||
                               (onActiveFloor && _visibleTiles.Contains(point.Position));
                SetSpriteHierarchyVisible(point.Root, visible);

                Color tint = ElevationTint(point.Position);
                Color light = TileLightColor(point.Position);
                point.Renderer.color = new Color(
                    tint.r * light.r, tint.g * light.g, tint.b * light.b, 1f);
            }
        }

        /// <summary>탈출구까지 걸어가 "계속 탐색 vs 여기서 생환" 선택지를 띄운다.</summary>
        private IEnumerator ApproachAndOfferExtraction(
            IReadOnlyList<GridPos> path, ExtractionAgent point)
        {
            yield return MovePlayerPath(path);
            if (!_playerState.IsAlive || !IsPlayerAdjacentTo(point.Position)) yield break;

            AtExtractionPoint = true;
            InteractionFeedback?.Invoke("비상 탈출구 — 여기서 나가면 지금까지 챙긴 것을 지킨다");
            ExitChoiceRequested?.Invoke();
        }

        /// <summary>
        /// 비상 송출기: 어디서든 즉시 생환한다. 탈출구까지 못 가는 상황을 위한 보험이자,
        /// 숨은 방·깊은 층 보상이 "살아 나갈 권리"가 되게 하는 아이템이다.
        /// </summary>
        public void UseExtractionBeacon()
        {
            if (!Application.isPlaying || _resolvingAction || hubMode ||
                _playerState == null || !_playerState.IsAlive || _runSummary.Ended)
                return;
            if (_inventory.Count(ItemKind.ExtractionBeacon) <= 0)
            {
                InteractionFeedback?.Invoke("NO BEACON");
                return;
            }

            _inventory.TryUse(ItemKind.ExtractionBeacon);
            _runTelemetry?.RecordItemUsed(
                ItemKind.ExtractionBeacon, GlobalFloorIndex(_activeFloorIndex));
            InventoryChanged?.Invoke();
            InteractionFeedback?.Invoke("비상 송출기 작동 — 지상으로 끌어올려진다");
            Debug.Log($"[Run] 비상 송출기 생환: {FloorLabel(_activeFloorIndex)}");
            ExtractRun();
        }

        private sealed class ExtractionAgent
        {
            public int FloorIndex;
            public GridPos Position;
            public GameObject Root;
            public SpriteRenderer Renderer;
        }
    }
}
