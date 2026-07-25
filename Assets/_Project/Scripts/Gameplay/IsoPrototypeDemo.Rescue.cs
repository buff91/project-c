using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 갇힌 동료의 월드 표현과 구출 처리. 배치·판정은 Core
    /// (<see cref="ShelterNpcRoster"/>, <see cref="DungeonFloorInfo.RescueNpc"/>)가 소유하고
    /// 여기서는 그리기와 구출만 한다 — 보스 제단(<c>BossArena</c>)과 같은 모양이다.
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        private GameObject _rescueNpc;
        private SpriteRenderer _rescueNpcRenderer;
        private GridPos _rescueNpcPos;
        private int _rescueNpcFloorIndex;
        private string _rescueNpcId;
        private bool _hasRescueNpc;

        /// <summary>이 판에 구출한 동료의 표시명들 — 판 종료 화면이 알린다.</summary>
        public string RescuedThisRun { get; private set; }

        private void ResetRescueForBuild()
        {
            _rescueNpc = null;
            _rescueNpcRenderer = null;
            _hasRescueNpc = false;
            _rescueNpcId = null;
            RescuedThisRun = null;
        }

        /// <summary>갇힌 방의 동료를 세운다. 구출 대상이 없는 층은 그냥 지나간다.</summary>
        private void CreateRescueNpc(DungeonFloorInfo floor)
        {
            if (hubMode || !floor.RescueNpc.HasValue) return;

            ShelterNpcDefinition npc = ShelterNpcRoster.ById(floor.RescueNpcId);
            if (npc == null) return;

            _rescueNpcPos = floor.RescueNpc.Value;
            _rescueNpcFloorIndex = floor.FloorIndex;
            _rescueNpcId = npc.Id;
            // 영웅 스프라이트를 재사용하지 않는다 — 동료는 적도 플레이어도 아니라서
            // 실루엣이 구분돼야 한다. 전용 프롭이 들어올 때까지 상인 계열을 쓴다.
            _rescueNpc = CreateStandingSprite(
                $"Rescue {npc.Id}",
                ActorSprites.GetHubPropSprite("merchant"),
                _rescueNpcPos,
                out _rescueNpcRenderer,
                microOffset: 1);
            _hasRescueNpc = true;
        }

        /// <summary>시점 회전/뷰 갱신 때 같은 GridPos로 다시 투영한다.</summary>
        private void ApplyRescueNpcView()
        {
            if (!_hasRescueNpc || _rescueNpc == null || _rescueNpcRenderer == null) return;
            _rescueNpc.transform.position = _grid.GridToWorld(_rescueNpcPos);
            _rescueNpcRenderer.sortingOrder = _grid.iso.SortingOrder(_rescueNpcPos, 1);
        }

        /// <summary>동료도 FOV를 따른다 — 활성 층에서 실제로 보이는 칸일 때만 드러난다.</summary>
        private void RefreshRescueNpcVisibility()
        {
            if (!_hasRescueNpc || _rescueNpc == null || _rescueNpcRenderer == null) return;

            bool onActiveFloor = _rescueNpcFloorIndex == _activeFloorIndex;
            bool visible = viewMode == DungeonViewMode.DebugAll ||
                           (onActiveFloor && _visibleTiles.Contains(_rescueNpcPos));
            SetSpriteHierarchyVisible(_rescueNpc, visible);

            Color tint = ElevationTint(_rescueNpcPos);
            Color light = TileLightColor(_rescueNpcPos);
            _rescueNpcRenderer.color = new Color(
                tint.r * light.r, tint.g * light.g, tint.b * light.b, 1f);
        }

        /// <summary>이 칸에 구출할 동료가 서 있는가.</summary>
        private bool IsRescueNpcAt(GridPos pos) =>
            _hasRescueNpc && _rescueNpcPos == pos && _rescueNpcFloorIndex == _activeFloorIndex;

        /// <summary>
        /// 동료를 구출한다. <b>즉시 저장한다</b> — 이 판에 죽어도 합류는 남는다.
        /// 구출은 소지품이 아니라 진행이고, 실패한 판도 전진이어야 하기 때문이다.
        /// </summary>
        private bool TryRescueNpcAt(GridPos pos)
        {
            if (!IsRescueNpcAt(pos)) return false;

            ShelterNpcDefinition npc = ShelterNpcRoster.ById(_rescueNpcId);
            if (npc == null) return false;

            MetaSaveData meta = MetaStore.LoadOrNew();
            if (meta.RescueNpc(npc.Id))
            {
                MetaStore.Save(meta);
                Debug.Log($"[Rescue] {npc.Id} 구출 — {npc.Facility} 해금");
            }

            RescuedThisRun = RescuedThisRun == null
                ? npc.DisplayName
                : $"{RescuedThisRun} · {npc.DisplayName}";

            // 같은 사람을 두 번 구출하지 않는다 — 월드에서 치운다.
            _hasRescueNpc = false;
            if (_rescueNpc != null) _rescueNpc.SetActive(false);

            InteractionFeedback?.Invoke(npc.RescueTitle);
            DungeonEntryCue?.Invoke(npc.RescueTitle, npc.RescueDetail);
            return true;
        }
    }
}
