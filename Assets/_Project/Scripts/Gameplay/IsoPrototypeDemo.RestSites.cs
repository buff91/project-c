using System.Collections;
using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>던전 내부 1회용 휴식처의 월드 표현·상호작용·런 상태 보존.</summary>
    public partial class IsoPrototypeDemo
    {
        private readonly List<RestSiteAgent> _restSites = new List<RestSiteAgent>();
        private readonly HashSet<int> _usedRestFloorIndices = new HashSet<int>();

        public int RestSiteCount => _restSites.Count;

        private void ResetRestSitesForBuild()
        {
            _restSites.Clear();
            _usedRestFloorIndices.Clear();
        }

        private void CreateRestSite(DungeonFloorInfo floor)
        {
            if (!floor.RestSite.HasValue) return;

            GridPos position = floor.RestSite.Value;
            Sprite sprite = visualCatalog != null ? visualCatalog.hubCampfire : null;
            GameObject root = CreateStandingSprite(
                $"Rest Site {FloorLabel(floor.FloorIndex)}",
                sprite != null ? sprite : ActorSprites.GetHubPropSprite("campfire"),
                position,
                out SpriteRenderer renderer,
                microOffset: 1);
            _restSites.Add(new RestSiteAgent
            {
                FloorIndex = floor.FloorIndex,
                GlobalFloorIndex = GlobalFloorIndex(floor.FloorIndex),
                Position = position,
                Root = root,
                Renderer = renderer
            });
        }

        private bool TryGetRestSiteAt(GridPos position, out RestSiteAgent site)
        {
            foreach (RestSiteAgent candidate in _restSites)
            {
                if (candidate.Position != position) continue;
                site = candidate;
                return true;
            }

            site = null;
            return false;
        }

        private bool IsRestSiteUsed(RestSiteAgent site) =>
            site != null && _usedRestFloorIndices.Contains(site.GlobalFloorIndex);

        private void RestoreUsedRestSites(IReadOnlyList<int> floorIndices)
        {
            _usedRestFloorIndices.Clear();
            if (floorIndices != null)
            {
                for (int i = 0; i < floorIndices.Count; i++)
                    _usedRestFloorIndices.Add(floorIndices[i]);
            }
            RefreshRestSiteVisibility();
        }

        private List<int> SnapshotUsedRestSites()
        {
            var result = new List<int>(_usedRestFloorIndices);
            result.Sort();
            return result;
        }

        private void ApplyRestSiteView()
        {
            foreach (RestSiteAgent site in _restSites)
            {
                if (site.Root == null || site.Renderer == null) continue;
                site.Root.transform.position = _grid.GridToWorld(site.Position);
                site.Renderer.sortingOrder = _grid.iso.SortingOrder(site.Position, 1);
            }
        }

        private void RefreshRestSiteVisibility()
        {
            if (_dungeon == null) return;

            foreach (RestSiteAgent site in _restSites)
            {
                if (site.Root == null || site.Renderer == null) continue;
                bool onActiveFloor = site.FloorIndex == _activeFloorIndex;
                bool visible = viewMode == DungeonViewMode.DebugAll ||
                               (onActiveFloor && _visibleTiles.Contains(site.Position));
                SetSpriteHierarchyVisible(site.Root, visible);
                site.Renderer.color = IsRestSiteUsed(site)
                    ? new Color32(100, 104, 108, 190)
                    : Color.white;
                site.Root.transform.localScale = IsRestSiteUsed(site)
                    ? new Vector3(0.82f, 0.82f, 1f)
                    : Vector3.one;
            }
        }

        private IEnumerator ApproachAndRest(IReadOnlyList<GridPos> path, RestSiteAgent site)
        {
            yield return MovePlayerPath(path);
            if (!_playerState.IsAlive || !IsPlayerAdjacentTo(site.Position))
                yield break;

            if (IsRestSiteUsed(site))
            {
                InteractionFeedback?.Invoke("이 휴식처는 이미 식었다");
                yield break;
            }

            int healAmount = DungeonRestRules.HealingAmount(_playerState.Hp, _playerState.MaxHp);
            if (healAmount <= 0)
            {
                InteractionFeedback?.Invoke("지금은 쉴 필요가 없다");
                yield break;
            }

            _usedRestFloorIndices.Add(site.GlobalFloorIndex);
            int healed = _playerState.Heal(healAmount);
            _runTelemetry?.RecordRest(healed, site.GlobalFloorIndex);
            UpdateHealthBar(_playerHpFill, _playerState);
            PlayerHpChanged?.Invoke();
            RefreshRestSiteVisibility();
            FloatingText?.ShowDamage(_player.transform.position, healed, FloatingTextKind.Heal);
            InteractionFeedback?.Invoke(
                $"휴식 — +{healed} HP · 이 모닥불은 다시 사용할 수 없다");
            Debug.Log(
                $"[Rest] {FloorLabel(site.FloorIndex)} 휴식: +{healed} HP → " +
                $"{_playerState.Hp}/{_playerState.MaxHp}");

            yield return FlashColor(_playerRenderer, new Color32(255, 190, 92, 255));
            yield return ResolveEnemyPhase();
            if (_playerState.IsAlive)
                SaveCheckpoint();
        }

        private sealed class RestSiteAgent
        {
            public int FloorIndex;
            public int GlobalFloorIndex;
            public GridPos Position;
            public GameObject Root;
            public SpriteRenderer Renderer;
        }
    }
}
