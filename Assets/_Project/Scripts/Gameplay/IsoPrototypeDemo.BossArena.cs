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

        /// <summary>지상 진입(B1 → 1F) 알림을 한 판에 한 번만 띄우기 위한 플래그.</summary>
        private bool _surfaceCrossingAnnounced;

        /// <summary>건물 전원이 들어와 엘리베이터가 움직이는 상태인지(표시·안내용).</summary>
        private bool _elevatorPowered;

        public bool HasBossAltar => _hasBossAltar;

        private void ResetBossArenaForBuild()
        {
            _bossAltar = null;
            _bossAltarRenderer = null;
            _hasBossAltar = false;
            _bossApproachAnnounced = false;
            _surfaceCrossingAnnounced = false;
            _elevatorPowered = false;
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
            _bossAltar.transform.position = VisualPosition(_bossAltarPos);
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

        /// <summary>이 칸이 엘리베이터 <b>탑승구</b>인가. 표지는 탑승구에만 세운다 —
        /// 도착 칸에도 세우면 한 대가 두 대로 보인다.</summary>
        private bool IsElevatorEntrance(GridPos pos)
        {
            if (_dungeon == null) return false;
            foreach (DungeonFloorInfo floor in _dungeon.Floors)
                if (floor.ElevatorShaft.HasValue && floor.ElevatorShaft.Value == pos) return true;
            return false;
        }

        /// <summary>이 칸이 엘리베이터 설비(탑승구 또는 도착 칸)인가.</summary>
        private bool IsElevatorTile(GridPos pos)
        {
            if (_dungeon == null) return false;
            foreach (DungeonFloorInfo floor in _dungeon.Floors)
            {
                if (floor.ElevatorShaft.HasValue && floor.ElevatorShaft.Value == pos) return true;
                if (floor.ElevatorLanding.HasValue && floor.ElevatorLanding.Value == pos) return true;
            }
            return false;
        }

        /// <summary>
        /// 건물 전원이 들어오면 엘리베이터에 링크를 넣는다 — 생성기는 설비만 놓고
        /// 링크를 만들지 않는다(<see cref="ElevatorShaftRules"/>). 링크가 곧 "움직인다"이며,
        /// 없는 동안은 경로 탐색도 이 칸을 지름길로 쓰지 않는다.
        ///
        /// <para>
        /// 보스 처치 직후와 <b>이어하기 복원</b> 양쪽에서 불린다. 여러 번 불려도 안전하도록
        /// 이미 링크가 있으면 그냥 돌아간다.
        /// </para>
        /// </summary>
        private void PowerElevatorIfUnlocked()
        {
            if (hubMode || _dungeon == null) return;
            if (!ElevatorShaftRules.IsPowered(
                    DungeonSelection.Selected?.Boss != null,
                    _bossDefeated))
                return;

            GridPos? entrance = null;
            GridPos? landing = null;
            foreach (DungeonFloorInfo floor in _dungeon.Floors)
            {
                if (floor.ElevatorShaft.HasValue) entrance = floor.ElevatorShaft;
                if (floor.ElevatorLanding.HasValue) landing = floor.ElevatorLanding;
            }

            if (!entrance.HasValue || !landing.HasValue) return;
            if (_grid.Map.LinksFrom(entrance.Value).Count > 0) return;

            // 복귀 전용이라 한 방향이다 — 타고 올라오면 계단을 건너뛰는 지름길이 된다.
            _grid.Map.Connect(entrance.Value, landing.Value, bidirectional: false);
            _elevatorPowered = true;
            // 표지의 스프라이트·목적지를 먼저 바꾸고(랜드마크는 빌드 때 한 번 만들어진다),
            // 그다음 가시성·라벨을 갱신한다. 순서가 바뀌면 옛 목적지로 라벨이 그려진다.
            RefreshElevatorLandmark();
            RefreshFloorVisibility();
            InteractionFeedback?.Invoke("건물에 전원이 들어왔다 — 엘리베이터가 움직인다");
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

            // 진행 지수는 레이아웃에서 받는다 — 예전의 `-_activeFloorIndex` 는 상승 던전에서
            // 음수가 되어 전조가 아예 뜨지 않았다(GDD §5.1).
            // 방향도 함께 넘긴다 — 문구가 "한 층 아래" 로 고정이던 시절 상승 던전(아케이드 타워)에서
            // 정반대를 가리켰다. 레이아웃이 실제 생성에 쓴 값이라 표시와 구조가 어긋나지 않는다.
            if (!DungeonBossArenaRules.TryApproachCue(
                    boss.DisplayName,
                    _dungeon.Direction,
                    _dungeon.ProgressIndexFor(_activeFloorIndex),
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
