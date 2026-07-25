using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// IsoPrototypeDemo의 조명 확장부: 정적 광원(모닥불·벽 등잔·개구부) 차폐 필드 캐시와
    /// 액터 발밑 접촉 그림자. 깊이 앰비언트·플레이어 광원 웅덩이(=TileLightLevel)는
    /// Visibility 파티션에 있고, 여기서 만든 정적 필드를 그쪽이 합산한다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        // 활성 층의 정적 광량 필드(GridPos→0..1). 정적 지오메트리에만 의존하므로 층당 1회 계산해 캐시한다.
        private Dictionary<GridPos, float> _staticLightField;
        private int _staticLightFloor = int.MinValue;
        private bool _staticLightDirty = true;
        private SpriteRenderer _playerShadow;

        /// <summary>지오메트리/설정 변화로 정적 광량 필드를 다음 갱신에서 다시 계산하게 한다.</summary>
        private void MarkStaticLightDirty() => _staticLightDirty = true;

        /// <summary>필요할 때만(층 전환·더티) 정적 광량 필드를 다시 계산한다.</summary>
        private void EnsureStaticLightField()
        {
            if (!staticLights || !dungeonDarkness || hubMode || _dungeon == null || _grid == null)
            {
                _staticLightField = null;
                _staticLightFloor = int.MinValue;
                return;
            }

            if (!_staticLightDirty && _staticLightFloor == _activeFloorIndex &&
                _staticLightField != null)
                return;

            RebuildStaticLightField();
            _staticLightFloor = _activeFloorIndex;
            _staticLightDirty = false;
        }

        private void RebuildStaticLightField()
        {
            int minElev = _dungeon.Height.Elevation(_activeFloorIndex);
            int maxElev = minElev + _dungeon.Height.ElevationsPerFloor - 1;

            var lights = new List<GridLighting.PointLight>();

            // 모닥불: 던전 안 진짜 불 — 강한 웜 광원.
            foreach (RestSiteAgent site in _restSites)
            {
                if (site.FloorIndex != _activeFloorIndex) continue;
                lights.Add(new GridLighting.PointLight(
                    site.Position, restLightRadius, restLightIntensity));
            }

            foreach (KeyValuePair<GridPos, TileData> pair in _grid.Map.All())
            {
                if (_dungeon.Height.FloorIndex(pair.Key.elevation) != _activeFloorIndex) continue;

                // Hole: 위·아래 층의 빛이 새어드는 개구부.
                if (pair.Value.kind == TileKind.Hole)
                    lights.Add(new GridLighting.PointLight(
                        pair.Key, holeLightRadius, holeLightIntensity));
                // 벽 등잔: 방 가장자리의 seed 고정 타일 — 시점 회전과 무관하게 같은 자리.
                else if (IsWallSconceTile(pair.Key, pair.Value))
                    lights.Add(new GridLighting.PointLight(
                        pair.Key, sconceLightRadius, sconceLightIntensity));
            }

            _staticLightField = lights.Count == 0
                ? new Dictionary<GridPos, float>()
                : GridLighting.ComputeStaticField(_grid.Map, lights, minElev, maxElev);
        }

        /// <summary>
        /// 벽 등잔이 걸릴 자리: 활성 층의 바닥 타일 중 이웃 하나가 비어(벽이 서는 자리)
        /// 방 가장자리이고, seed 해시로 고른 소수만. 시점(viewQuarterTurns)에 의존하지 않는다.
        /// </summary>
        private bool IsWallSconceTile(GridPos pos, TileData tile)
        {
            if (tile == null || tile.kind != TileKind.Floor) return false;
            int floor = _activeFloorIndex;
            bool edge =
                !HasPlanarTile(pos.x + 1, pos.y, floor) ||
                !HasPlanarTile(pos.x - 1, pos.y, floor) ||
                !HasPlanarTile(pos.x, pos.y + 1, floor) ||
                !HasPlanarTile(pos.x, pos.y - 1, floor);
            if (!edge) return false;
            int hash = (pos.x * 73856093) ^ (pos.y * 19349663) ^ (dungeonSeed * 83492791);
            return (hash & 0x7fffffff) % 6 == 0;
        }

        private float StaticLightAt(GridPos pos)
        {
            if (_staticLightField != null && _staticLightField.TryGetValue(pos, out float value))
                return value;
            return 0f;
        }

        /// <summary>액터(플레이어·적) 발밑에 붙는 부드러운 접촉 그림자 렌더러를 만든다.</summary>
        private SpriteRenderer CreateContactShadow(Transform parent)
        {
            var obj = new GameObject("Contact Shadow");
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = GetContactShadowSprite();
            renderer.color = new Color32(0, 0, 0, 0);
            return renderer;
        }

        /// <summary>
        /// 접촉 그림자를 액터 아래 한 칸에 정렬하고 색을 갱신한다. 밝은 곳일수록 진하고
        /// 어두운 곳일수록 옅다(그림자는 빛이 있어야 생긴다). 허브에서는 끈다.
        /// </summary>
        private void UpdateContactShadow(
            SpriteRenderer shadow, GridPos groundPos, int actorSortingOrder, bool actorVisible)
        {
            if (shadow == null) return;
            bool show = contactShadows && !hubMode && _dungeon != null && actorVisible;
            shadow.enabled = show;
            if (!show) return;

            shadow.sortingOrder = actorSortingOrder - 1;
            float light = TileLightLevel(groundPos);
            Color32 v = DungeonVoidColor;
            int alpha = Mathf.Clamp(
                Mathf.RoundToInt(255f * contactShadowStrength * light), 0, 255);
            shadow.color = new Color32(v.r, v.g, v.b, (byte)alpha);
        }
    }
}
