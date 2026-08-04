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
            if (_b2HeroRoomLayout != null &&
                _b2HeroRoomLayout.TryGetWallSconce(pos, out bool authoredSconce))
                return authoredSconce;
            return SconcePlacement.IsSconce(
                pos.x, pos.y, dungeonSeed, WallSconceRarityFor(floor));
        }

        /// <summary>
        /// 이 층의 등잔 희소도. 깊어질수록 드물어 어둠이 깊어진다.
        /// 아트(<c>CreateRearWall</c>)와 빛이 <b>같은 값</b>을 봐야 램프와 빛 웅덩이가 겹친다.
        /// </summary>
        private int WallSconceRarityFor(int floorIndex) =>
            DungeonBandProfiles
                .ForDepth(
                    _dungeon?.Region ?? DungeonRegionProfile.Facility,
                    _dungeon != null ? _dungeon.ProgressIndexFor(floorIndex) : 0)
                .WallSconceRarity;

        private float StaticWarmAt(GridPos pos) =>
            _staticWarmField != null && _staticWarmField.TryGetValue(pos, out float v) ? v : 0f;

        private float StaticCoolAt(GridPos pos) =>
            _staticCoolField != null && _staticCoolField.TryGetValue(pos, out float v) ? v : 0f;

        /// <summary>
        /// 활성 층에서 보이는 바닥을 4방향 연결 영역별 한 조명 덩어리로 묶는다. Door·Stairs 등
        /// Floor가 아닌 경계는 연결에서 제외되므로 방 사이의 광량은 섞이지 않는다. 기존 타일별 조명은
        /// 벽·액터·소품에 그대로 남겨 광원 위치를 보여 주고, 바닥 RGB만 영역 평균 쪽으로 모아
        /// 128px 다이아마다 밝기가 뚝 끊기는 체크무늬를 억제한다. FOV 알파·원소 상태와는 별도 축이다.
        /// </summary>
        private Dictionary<GridPos, Color> BuildCoherentFloorLightField()
        {
            if (_grid == null ||
                viewMode == DungeonViewMode.DebugAll ||
                !dungeonDarkness)
                return null;

            var localLights = new Dictionary<GridPos, Color>();
            foreach (GridPos pos in _visibleTiles)
            {
                if (_dungeon.Height.FloorIndex(pos.elevation) != _activeFloorIndex ||
                    _grid.Map.Get(pos)?.kind != TileKind.Floor)
                    continue;
                localLights[pos] = TileLightColor(pos);
            }
            if (localLights.Count == 0) return null;

            return RoomFloorLighting.BuildCoherentField(localLights);
        }

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
            obj.transform.localPosition = new Vector3(0f, -0.01f, 0f);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = ActorSprites.GetContactShadowSprite();
            renderer.color = new Color32(0, 0, 0, 0);
            return renderer;
        }

        /// <summary>
        /// 플레이어도 적·바닥과 같은 월드 광원을 받는다. 상태색만 적용하던 구 경로는 밝은 스티커처럼
        /// 분리됐고, 특히 흰 재킷이 어두운 B2 바닥에서 허공에 떠 보였다.
        /// </summary>
        private void ApplyPlayerVisuals()
        {
            if (_playerRenderer == null || _playerState == null || _dungeon == null) return;

            GridPos pos = _playerState.Position;
            _playerRenderer.color = ActorGroundingPresentation.WorldTint(
                CombatantTint(_playerState),
                ElevationTint(pos),
                TileLightColor(pos));
            UpdateContactShadow(
                _playerShadow,
                pos,
                _playerRenderer.sortingOrder,
                _playerState.IsAlive);
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
            Color32 v = Palette.Void;
            int alpha = Mathf.RoundToInt(
                255f * ActorGroundingPresentation.ShadowTintAlpha(
                    contactShadowStrength,
                    lit));
            shadow.color = new Color32(v.r, v.g, v.b, (byte)alpha);
        }
    }

    /// <summary>연결된 방 바닥의 조명 분산 규칙. 런타임 상태와 무관한 수치부만 분리한다.</summary>
    internal static class RoomFloorLighting
    {
        internal const float LocalLightRetention = 0.2f;

        /// <summary>
        /// 같은 elevation의 4방향 이웃만 한 영역으로 묶고 영역별 평균광을 적용한다.
        /// 호출자가 Floor만 넘기므로 Door·Stairs 같은 비-Floor 타일은 자연스럽게 경계가 된다.
        /// </summary>
        internal static Dictionary<GridPos, Color> BuildCoherentField(
            IReadOnlyDictionary<GridPos, Color> localLights)
        {
            var coherent = new Dictionary<GridPos, Color>(localLights.Count);
            var remaining = new HashSet<GridPos>(localLights.Keys);
            var frontier = new Queue<GridPos>();
            var component = new List<GridPos>();
            var samples = new List<Color>();

            while (remaining.Count > 0)
            {
                GridPos start = default;
                foreach (GridPos pos in remaining)
                {
                    start = pos;
                    break;
                }

                remaining.Remove(start);
                frontier.Enqueue(start);
                component.Clear();
                samples.Clear();

                while (frontier.Count > 0)
                {
                    GridPos current = frontier.Dequeue();
                    component.Add(current);
                    samples.Add(localLights[current]);
                    EnqueueIfRemaining(current.North, remaining, frontier);
                    EnqueueIfRemaining(current.South, remaining, frontier);
                    EnqueueIfRemaining(current.East, remaining, frontier);
                    EnqueueIfRemaining(current.West, remaining, frontier);
                }

                Color reference = Average(samples);
                foreach (GridPos pos in component)
                    coherent[pos] = Coherent(reference, localLights[pos]);
            }

            return coherent;
        }

        private static void EnqueueIfRemaining(
            GridPos pos,
            HashSet<GridPos> remaining,
            Queue<GridPos> frontier)
        {
            if (remaining.Remove(pos))
                frontier.Enqueue(pos);
        }

        internal static Color Average(IEnumerable<Color> samples)
        {
            Color sum = Color.clear;
            int count = 0;
            foreach (Color sample in samples)
            {
                sum += sample;
                count++;
            }
            if (count == 0) return Color.white;
            return sum / count;
        }

        internal static Color Coherent(Color roomReference, Color local)
        {
            Color result = Color.Lerp(roomReference, local, LocalLightRetention);
            result.a = local.a;
            return result;
        }
    }

    /// <summary>액터의 안정 상태 월드 틴트와 접촉 AO 수치 계약.</summary>
    internal static class ActorGroundingPresentation
    {
        internal const float ShadowLightFloor = 0.65f;
        internal const float PlayerFootprintAlpha = 0.46f;

        internal static Color WorldTint(Color state, Color elevation, Color light) =>
            new Color(
                state.r * elevation.r * light.r,
                state.g * elevation.g * light.g,
                state.b * elevation.b * light.b,
                state.a);

        internal static float ShadowTintAlpha(float strength, float light) =>
            Mathf.Clamp01(strength) *
            Mathf.Lerp(ShadowLightFloor, 1f, Mathf.Clamp01(light));
    }
}
