using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 투척 조준의 월드 미리보기. 화면공간 설명을 늘리지 않고 실제로 선택 가능한 칸 위에 표시한다.
    /// 판정은 Core <see cref="BombRules.ForEachThrowTarget"/>와 원거리 시야선 규칙을 그대로 쓴다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
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
                    AddThrowRangeMarker(target);
                }
                return;
            }

            BombRules.ForEachThrowTarget(
                _grid.Map,
                _playerPos,
                bombThrowRange,
                target =>
                {
                    if (IsVisibleAimTarget(target))
                        AddThrowRangeMarker(target);
                });
        }

        private bool IsVisibleAimTarget(GridPos target) =>
            viewMode == DungeonViewMode.DebugAll || _visibleTiles.Contains(target);

        private void AddThrowRangeMarker(GridPos pos)
        {
            var marker = new GameObject($"Throw Range {pos}");
            marker.transform.SetParent(_visualRoot, false);
            marker.transform.position = VisualPosition(pos);

            var renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = ActorSprites.GetThrowRangeSprite();
            renderer.color = ThrowRangeColor(_bombAimKind);
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
    }
}
