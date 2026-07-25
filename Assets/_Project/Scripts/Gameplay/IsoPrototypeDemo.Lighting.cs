using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// IsoPrototypeDemo의 조명 확장부: 정적 광원(모닥불·벽 등잔·개구부) 차폐 필드 캐시,
    /// 방향성 캐스트 그림자, 액터 발밑 접촉 그림자. 깊이 앰비언트·플레이어 광원 웅덩이와
    /// 색 합성(=TileLightColor)은 Visibility 파티션에 있고, 여기서 만든 필드를 그쪽이 읽는다.
    /// 정적 광원은 웜(불·등잔)과 쿨(개구부로 새어드는 빛)로 나눠 색조까지 만든다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        // 활성 층의 정적 광량 필드(GridPos→0..1). 정적 지오메트리에만 의존하므로 층당 1회 계산해 캐시한다.
        private Dictionary<GridPos, float> _staticWarmField; // 모닥불·벽 등잔
        private Dictionary<GridPos, float> _staticCoolField; // 개구부(Hole)
        private int _staticLightFloor = int.MinValue;
        private bool _staticLightDirty = true;
        private SpriteRenderer _playerShadow;

        private Color WarmLightColor => warmLightColor;
        private Color CoolLightColor => coolLightColor;

        /// <summary>지오메트리/설정 변화로 정적 광량 필드를 다음 갱신에서 다시 계산하게 한다.</summary>
        private void MarkStaticLightDirty() => _staticLightDirty = true;

        /// <summary>필요할 때만(층 전환·더티) 정적 광량 필드를 다시 계산한다.</summary>
        private void EnsureStaticLightField()
        {
            if (!staticLights || !dungeonDarkness || hubMode || _dungeon == null || _grid == null)
            {
                _staticWarmField = null;
                _staticCoolField = null;
                _staticLightFloor = int.MinValue;
                return;
            }

            if (!_staticLightDirty && _staticLightFloor == _activeFloorIndex &&
                _staticWarmField != null && _staticCoolField != null)
                return;

            RebuildStaticLightField();
            _staticLightFloor = _activeFloorIndex;
            _staticLightDirty = false;
        }

        private void RebuildStaticLightField()
        {
            int minElev = _dungeon.Height.Elevation(_activeFloorIndex);
            int maxElev = minElev + _dungeon.Height.ElevationsPerFloor - 1;

            var warmLights = new List<GridLighting.PointLight>(); // 불·등잔 = 따뜻한 앰버
            var coolLights = new List<GridLighting.PointLight>(); // 개구부 = 차가운 새어드는 빛

            // 모닥불: 던전 안 진짜 불 — 강한 웜 광원.
            foreach (RestSiteAgent site in _restSites)
            {
                if (site.FloorIndex != _activeFloorIndex) continue;
                warmLights.Add(new GridLighting.PointLight(
                    site.Position, restLightRadius, restLightIntensity));
            }

            foreach (KeyValuePair<GridPos, TileData> pair in _grid.Map.All())
            {
                if (_dungeon.Height.FloorIndex(pair.Key.elevation) != _activeFloorIndex) continue;

                // Hole: 위·아래 층의 빛이 새어드는 개구부 — 차가운 국소 광원.
                if (pair.Value.kind == TileKind.Hole)
                    coolLights.Add(new GridLighting.PointLight(
                        pair.Key, holeLightRadius, holeLightIntensity));
                // 벽 등잔: 방 가장자리의 seed 고정 타일 — 은은한 웜 토치 앰비언스.
                else if (IsWallSconceTile(pair.Key, pair.Value))
                    warmLights.Add(new GridLighting.PointLight(
                        pair.Key, sconceLightRadius, sconceLightIntensity));
            }

            _staticWarmField = warmLights.Count == 0
                ? new Dictionary<GridPos, float>()
                : GridLighting.ComputeStaticField(_grid.Map, warmLights, minElev, maxElev);
            _staticCoolField = coolLights.Count == 0
                ? new Dictionary<GridPos, float>()
                : GridLighting.ComputeStaticField(_grid.Map, coolLights, minElev, maxElev);
        }

        /// <summary>
        /// 벽 등잔이 걸릴 자리: 활성 층의 바닥 타일 중 이웃 하나가 비어(벽이 서는 자리)
        /// 방 가장자리이고, seed 해시로 고른 소수만. 시점(viewQuarterTurns)에 의존하지 않는다.
        /// 희소도는 깊이 밴드가 정한다 — 깊어질수록 등잔이 드물어 어둠이 깊어진다.
        /// 팔레트는 건드리지 않고 광원 밀도만 바꾸므로 던전 공통 톤은 그대로다.
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
            int rarity = DungeonBandProfiles
                .ForDepth(_dungeon != null ? _dungeon.ProgressIndexFor(floor) : 0)
                .WallSconceRarity;
            int hash = (pos.x * 73856093) ^ (pos.y * 19349663) ^ (dungeonSeed * 83492791);
            return (hash & 0x7fffffff) % rarity == 0;
        }

        private float StaticWarmAt(GridPos pos) =>
            _staticWarmField != null && _staticWarmField.TryGetValue(pos, out float v) ? v : 0f;

        private float StaticCoolAt(GridPos pos) =>
            _staticCoolField != null && _staticCoolField.TryGetValue(pos, out float v) ? v : 0f;

        /// <summary>
        /// 벽·융기 지형 발치의 방향성 캐스트 그림자 배수. 고정 키 라이트가 +x/+y 쪽에서
        /// 온다고 보고, 그 방향 이웃이 벽이거나 더 높으면 이 타일을 살짝 어둡게 한다.
        /// 월드 고정 방향이라 시점을 돌리면 그림자도 반대편에서 보인다.
        /// </summary>
        private float DirectionalShadowFactor(GridPos pos)
        {
            if (!directionalShadows) return 1f;
            int minElev = _dungeon.Height.Elevation(_activeFloorIndex);
            int maxElev = minElev + _dungeon.Height.ElevationsPerFloor - 1;
            bool occluded =
                GridLighting.ShadowedByNeighbor(_grid.Map, pos, minElev, maxElev, 1, 0) ||
                GridLighting.ShadowedByNeighbor(_grid.Map, pos, minElev, maxElev, 0, 1);
            return occluded ? directionalShadowStrength : 1f;
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
            Color light = TileLightColor(groundPos);
            float lit = Mathf.Max(light.r, Mathf.Max(light.g, light.b)); // 얼마나 밝은가
            Color32 v = DungeonVoidColor;
            int alpha = Mathf.Clamp(
                Mathf.RoundToInt(255f * contactShadowStrength * lit), 0, 255);
            shadow.color = new Color32(v.r, v.g, v.b, (byte)alpha);
        }
    }
}
