using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 최심층 보스 아레나의 월드 표현. 생성기가 고른 랜드마크(제단) 한 칸을 실제로 그리고,
    /// 바로 위층에 들어서면 접근 전조를 한 번 알린다. 배치·판정은 Core
    /// (<see cref="DungeonBossArenaRules"/>, <see cref="DungeonFloorInfo.Landmark"/>)가 소유하고
    /// 여기서는 그리기와 알림만 한다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private GameObject _bossAltar;
        private SpriteRenderer _bossAltarRenderer;
        private GridPos _bossAltarPos;
        private int _bossAltarFloorIndex;
        private bool _hasBossAltar;
        private bool _bossApproachAnnounced;

        public bool HasBossAltar => _hasBossAltar;

        private void ResetBossArenaForBuild()
        {
            _bossAltar = null;
            _bossAltarRenderer = null;
            _hasBossAltar = false;
            _bossApproachAnnounced = false;
        }

        /// <summary>아레나 층의 제단을 만든다. 랜드마크가 없는 층(=최심층이 아닌 층)은 그냥 지나간다.</summary>
        private void CreateBossAltar(DungeonFloorInfo floor)
        {
            if (hubMode || !floor.Landmark.HasValue) return;

            _bossAltarPos = floor.Landmark.Value;
            _bossAltarFloorIndex = floor.FloorIndex;
            _bossAltar = CreateStandingSprite(
                $"Boss Altar {FloorLabel(floor.FloorIndex)}",
                ActorSprites.GetBossAltarSprite(),
                _bossAltarPos,
                out _bossAltarRenderer,
                microOffset: 1);
            _hasBossAltar = true;
        }

        /// <summary>시점 회전/뷰 갱신 때 같은 GridPos로 다시 투영한다.</summary>
        private void ApplyBossAltarView()
        {
            if (!_hasBossAltar || _bossAltar == null || _bossAltarRenderer == null) return;
            _bossAltar.transform.position = _grid.GridToWorld(_bossAltarPos);
            _bossAltarRenderer.sortingOrder = _grid.iso.SortingOrder(_bossAltarPos, 1);
        }

        /// <summary>제단도 FOV를 따른다 — 활성 층에서 실제로 보이는 칸일 때만 드러난다.</summary>
        private void RefreshBossAltarVisibility()
        {
            if (!_hasBossAltar || _bossAltar == null || _bossAltarRenderer == null) return;

            bool onActiveFloor = _bossAltarFloorIndex == _activeFloorIndex;
            bool visible = viewMode == DungeonViewMode.DebugAll ||
                           (onActiveFloor && _visibleTiles.Contains(_bossAltarPos));
            SetSpriteHierarchyVisible(_bossAltar, visible);

            Color tint = ElevationTint(_bossAltarPos);
            Color light = TileLightColor(_bossAltarPos);
            // 보스를 쓰러뜨리면 제단의 신호색이 식는다 — 봉인 해제와 같은 방향의 상태 표현.
            float spent = _bossDefeated ? 0.62f : 1f;
            _bossAltarRenderer.color = new Color(
                tint.r * light.r * spent,
                tint.g * light.g * spent,
                tint.b * light.b * spent,
                1f);
        }

        /// <summary>
        /// 아레나 바로 위층에 처음 들어섰을 때의 접근 전조. 문구 판정은 Core가 소유하고,
        /// 여기서는 현재 던전의 층 수·보스 처치 여부만 넘긴다. 한 세션에 한 번만 알린다.
        /// </summary>
        private void AnnounceBossApproachIfNeeded()
        {
            if (hubMode || _dungeon == null || _bossApproachAnnounced) return;

            DungeonBossDefinition boss = DungeonSelection.Selected?.Boss;
            if (boss == null) return;

            if (!DungeonBossArenaRules.TryApproachCue(
                    boss.DisplayName,
                    -_activeFloorIndex,
                    _dungeon.Floors.Count,
                    _bossDefeated,
                    out string message))
                return;

            _bossApproachAnnounced = true;
            InteractionFeedback?.Invoke(message);
            if (_player != null)
                FloatingText?.Show(_player.transform.position, "...!", FloatingTextKind.Alert);
            Debug.Log($"[Boss] 접근 전조: {message}");
        }
    }
}
