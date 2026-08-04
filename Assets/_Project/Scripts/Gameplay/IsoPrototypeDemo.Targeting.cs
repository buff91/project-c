using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 투척 조준의 월드 미리보기. 화면공간 설명을 늘리지 않고 실제로 선택 가능한 칸 위에 표시한다.
    /// 판정은 Core <see cref="BombRules.ForEachThrowTarget"/>와 원거리 시야선 규칙을 그대로 쓴다.
    /// 사거리(어디에 던질 수 있나)와 영향 범위(무엇이 휘말리나)는 서로 다른 질문이라
    /// 스프라이트와 색을 갈라 둘 다 보여준다 — 범위는 포인터가 올라간 칸에서만 그린다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        /// <summary>조준 중 포인터가 올라간 칸. 마우스가 없으면 계속 null 이다.</summary>
        private GridPos? _aimHoverCell;

        /// <summary>이번 미리보기의 영향 범위 칸 — 폭발 훑는 순서를 그대로 유지한다.</summary>
        private readonly List<GridPos> _blastPreviewCells = new List<GridPos>();

        private void HandleTileHovered(GridPos? cell)
        {
            bool aimChanged = !System.Nullable.Equals(cell, _aimHoverCell);
            _aimHoverCell = cell;
            if (_bombAiming)
            {
                ClearVerticalReadOnlyMarker();
                if (aimChanged) RefreshThrowRangePreview();
                return;
            }

            HandleVerticalReadOnlyHover(cell);
            HandleVerticalFocusHover(cell);
        }

        private void RefreshThrowRangePreview()
        {
            ClearThrowRangePreview();
            if (!_bombAiming || _visualRoot == null || _grid == null || _playerState == null)
                return;

            if (_bombAimKind == ItemKind.ThrowingKnife)
            {
                foreach (EnemyAgent enemy in _enemies)
                {
                    if (enemy?.State == null || !enemy.State.IsAlive) continue;
                    GridPos target = enemy.State.Position;
                    if (!IsVisibleAimTarget(target)) continue;
                    if (CombatRules.DiagnoseRanged(
                            _grid.Map, _playerPos, target, rangedAttackRange) !=
                        RangedBlockReason.None)
                        continue;
                    AddThrowRangeMarker(target, blast: false);
                }
                return;
            }

            CollectBlastPreviewCells();

            if (IsVerticalLookActive)
            {
                VerticalThrowRules.ForEachThrowTarget(
                    _grid.Map,
                    _dungeon.Height,
                    _playerPos,
                    _bombAimKind,
                    bombThrowRange,
                    CanUseVerticalThrowNearEndpoint,
                    (target, _) =>
                    {
                        if (IsVerticalLookTarget(target) &&
                            !_blastPreviewCells.Contains(target))
                            AddThrowRangeMarker(target, blast: false);
                    });

                foreach (GridPos cell in _blastPreviewCells)
                    AddThrowRangeMarker(cell, blast: true);
                return;
            }

            BombRules.ForEachThrowTarget(
                _grid.Map,
                _playerPos,
                bombThrowRange,
                target =>
                {
                    // 영향 범위 칸은 아래에서 자기 스프라이트로 한 번만 그린다 — 같은 칸에
                    // 마커를 두 장 겹치면 정렬이 같아 어느 쪽이 위인지 프레임마다 흔들린다.
                    if (IsVisibleAimTarget(target) && !_blastPreviewCells.Contains(target))
                        AddThrowRangeMarker(target, blast: false);
                });

            foreach (GridPos cell in _blastPreviewCells)
                AddThrowRangeMarker(cell, blast: true);
        }

        /// <summary>포인터가 <b>던질 수 있는</b> 칸 위에 있을 때만 영향 범위를 모은다.</summary>
        private void CollectBlastPreviewCells()
        {
            _blastPreviewCells.Clear();

            GridPos? aim = _aimHoverCell.HasValue && IsVisibleAimTarget(_aimHoverCell.Value)
                ? _aimHoverCell
                : null;
            GridPos center;
            if (IsVerticalLookActive)
            {
                if (!aim.HasValue || !TryResolveVerticalThrow(aim.Value, out _)) return;
                center = aim.Value;
            }
            else if (!ThrowAimPreviewRules.TryResolveBlastCenter(
                         _grid.Map,
                         _playerPos,
                         aim,
                         _bombAimKind,
                         bombThrowRange,
                         out center))
            {
                return;
            }

            ThrowAimPreviewRules.ForEachBlastPreviewCell(_grid.Map, center, pos =>
            {
                // 아직 못 본 칸에 범위를 그리면 시야 밖 지형을 알려주는 셈이 된다(기둥 ③).
                if (IsVisibleAimTarget(pos)) _blastPreviewCells.Add(pos);
            });
        }

        private bool IsVisibleAimTarget(GridPos target) =>
            IsVerticalLookActive
                ? _verticalLookTiles.Contains(target)
                : viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(target);

        private void AddThrowRangeMarker(GridPos pos, bool blast)
        {
            var marker = new GameObject(blast ? $"Blast Preview {pos}" : $"Throw Range {pos}");
            marker.transform.SetParent(_visualRoot, false);
            marker.transform.position = VisualPosition(pos);

            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = blast
                ? ActorSprites.GetBlastPreviewSprite()
                : ActorSprites.GetThrowRangeSprite();
            renderer.color = blast
                ? BlastPreviewColor(_bombAimKind)
                : ThrowRangeColor(_bombAimKind);
            // 일반 바닥(-2)보다 한 단계 위, 아이템(0)·액터(1)보다 아래에 둔다.
            renderer.sortingOrder = _grid.iso.SortingOrder(pos, -1);
            _throwRangeMarkers.Add(renderer, pos);
        }

        private void ApplyThrowRangePreviewView()
        {
            foreach (KeyValuePair<SpriteRenderer, GridPos> pair in _throwRangeMarkers)
            {
                if (pair.Key == null) continue;
                pair.Key.transform.position = VisualPosition(pair.Value);
                pair.Key.sortingOrder = _grid.iso.SortingOrder(pair.Value, -1);
            }
        }

        private void ClearThrowRangePreview()
        {
            foreach (SpriteRenderer renderer in _throwRangeMarkers.Keys)
            {
                if (renderer == null) continue;
                if (Application.isPlaying)
                    Destroy(renderer.gameObject);
                else
                    DestroyImmediate(renderer.gameObject);
            }
            _throwRangeMarkers.Clear();
        }

        private static Color ThrowRangeColor(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.FrostBomb:
                    return new Color(0.60f, 0.90f, 1f, 0.52f);
                case ItemKind.OilFlask:
                    return new Color(0.94f, 0.52f, 0.20f, 0.48f);
                case ItemKind.ThrowingKnife:
                    return new Color(0.36f, 0.92f, 0.78f, 0.62f);
                default:
                    return new Color(1f, 0.74f, 0.25f, 0.50f);
            }
        }

        /// <summary>영향 범위는 같은 계열의 더 밝고 진한 색 — 사거리와 색상이 아니라 명도로 갈린다.</summary>
        private static Color BlastPreviewColor(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.FrostBomb:
                    return new Color(0.78f, 0.98f, 1f, 0.92f);
                case ItemKind.OilFlask:
                    return new Color(1f, 0.72f, 0.34f, 0.88f);
                default:
                    return new Color(1f, 0.60f, 0.24f, 0.92f);
            }
        }
    }
}
