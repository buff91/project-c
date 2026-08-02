using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// B2 시작방 바닥을 공중의 개별 타일이 아니라 하나의 얇은 구조 슬래브로 읽히게 한다.
    /// 이 루트는 콜라이더·입력·격자에 등록되지 않는 순수 프레젠테이션이다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private const int B2FoundationFaceSortingOrder = 1;
        private const int B2FoundationSupportSortingOrder = 2;

        private Transform _b2FoundationRoot;

        private void RebuildB2FloorFoundation(
            IReadOnlyDictionary<GridPos, Color> b2FloorLightField)
        {
            if (_b2FoundationRoot != null)
            {
                if (Application.isPlaying) Destroy(_b2FoundationRoot.gameObject);
                else DestroyImmediate(_b2FoundationRoot.gameObject);
                _b2FoundationRoot = null;
            }

            if (_visualRoot == null || hubMode || _dungeon == null || _grid == null ||
                _b2HeroRoomLayout == null ||
                _dungeon.Region != DungeonRegionProfile.Facility)
                return;

            var root = new GameObject("B2 Floor Foundation");
            root.hideFlags = HideFlags.DontSaveInEditor;
            root.transform.SetParent(_visualRoot, false);
            _b2FoundationRoot = root.transform;

            int floor = _dungeon.Height.FloorIndex(
                _b2HeroRoomLayout.RoomCells[0].elevation);
            bool IsRenderable(GridPos position) =>
                _tileRenderers.TryGetValue(position, out SpriteRenderer renderer) &&
                renderer != null && renderer.enabled;
            bool HasTile(GridPos position) =>
                HasPlanarTile(position.x, position.y, floor);

            GetViewDirections(
                out Vector2Int frontA,
                out Vector2Int frontB,
                out _,
                out _);
            FoundationCell[] cells = FloorFoundationPresentation.Collect(
                _b2HeroRoomLayout.RoomCells,
                IsRenderable,
                HasTile,
                frontA,
                frontB);
            foreach (FoundationCell cell in cells)
                CreateB2FoundationFace(cell, b2FloorLightField);

            FoundationSupport[] supports = FloorFoundationPresentation.CollectSupports(
                _b2HeroRoomLayout.RoomCells,
                IsRenderable,
                position => _grid.Map.Get(position)?.kind == TileKind.Floor,
                HasTile);
            foreach (FoundationSupport support in supports)
            {
                GetFoundationCornerDirections(
                    support.Corner,
                    out Vector2Int outwardA,
                    out Vector2Int outwardB);
                if (!IsFrontFoundationDirection(outwardA, frontA, frontB) &&
                    !IsFrontFoundationDirection(outwardB, frontA, frontB))
                    continue;

                CreateB2FoundationSupport(
                    support,
                    outwardA,
                    outwardB,
                    b2FloorLightField);
            }
        }

        private void CreateB2FoundationFace(
            FoundationCell cell,
            IReadOnlyDictionary<GridPos, Color> b2FloorLightField)
        {
            var face = new GameObject($"Foundation Face {cell.Position} {cell.Faces}");
            face.transform.SetParent(_b2FoundationRoot, false);
            face.transform.position = VisualPosition(cell.Position);

            var renderer = face.AddComponent<SpriteRenderer>();
            renderer.sprite = EnvironmentSprites.GetB2FoundationFaceSprite(
                cell.Faces,
                cell.RibPhase);
            renderer.sortingLayerName = DungeonFogBackdropLayout.SortingLayerName;
            renderer.sortingOrder = B2FoundationFaceSortingOrder;
            renderer.color = B2FoundationColor(cell.Position, b2FloorLightField);
        }

        private void CreateB2FoundationSupport(
            FoundationSupport support,
            Vector2Int outwardA,
            Vector2Int outwardB,
            IReadOnlyDictionary<GridPos, Color> b2FloorLightField)
        {
            var brace = new GameObject($"Foundation Support {support.Position} {support.Corner}");
            brace.transform.SetParent(_b2FoundationRoot, false);

            Vector3 center = VisualPosition(support.Position);
            Vector3 outsideA = VisualPosition(support.Position.Offset(outwardA.x, outwardA.y));
            Vector3 outsideB = VisualPosition(support.Position.Offset(outwardB.x, outwardB.y));
            Vector3 corner = (outsideA + outsideB) * 0.5f;
            brace.transform.position = corner + Vector3.down * (1f / 64f);

            var renderer = brace.AddComponent<SpriteRenderer>();
            renderer.sprite = EnvironmentSprites.GetB2FoundationSupportSprite(
                corner.x < center.x);
            renderer.sortingLayerName = DungeonFogBackdropLayout.SortingLayerName;
            renderer.sortingOrder = B2FoundationSupportSortingOrder;
            renderer.color = B2FoundationColor(support.Position, b2FloorLightField);
        }

        private Color B2FoundationColor(
            GridPos position,
            IReadOnlyDictionary<GridPos, Color> b2FloorLightField)
        {
            Color elevation = ElevationTint(position);
            Color light = b2FloorLightField != null &&
                          b2FloorLightField.TryGetValue(position, out Color coherent)
                ? coherent
                : TileLightColor(position);
            return new Color(
                elevation.r * light.r,
                elevation.g * light.g,
                elevation.b * light.b,
                VisibilityAlpha(position));
        }

        private static bool IsFrontFoundationDirection(
            Vector2Int direction,
            Vector2Int frontA,
            Vector2Int frontB) =>
            direction == frontA || direction == frontB;

        private static void GetFoundationCornerDirections(
            FoundationCorner corner,
            out Vector2Int outwardA,
            out Vector2Int outwardB)
        {
            switch (corner)
            {
                case FoundationCorner.NorthEast:
                    outwardA = Vector2Int.up;
                    outwardB = Vector2Int.right;
                    break;
                case FoundationCorner.NorthWest:
                    outwardA = Vector2Int.up;
                    outwardB = Vector2Int.left;
                    break;
                case FoundationCorner.SouthEast:
                    outwardA = Vector2Int.down;
                    outwardB = Vector2Int.right;
                    break;
                default:
                    outwardA = Vector2Int.down;
                    outwardB = Vector2Int.left;
                    break;
            }
        }
    }
}
