using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 갇힌 동료의 월드 표현과 구출 처리. 배치·판정은 Core
    /// (<see cref="ShelterNpcRoster"/>, <see cref="DungeonFloorInfo.RescueNpc"/>)가 소유하고
    /// 여기서는 그리기와 구출만 한다 — 보스 제단(<c>BossArena</c>)과 같은 모양이다.
    /// <para>
    /// <b>한 판에 동료가 여럿이다.</b> <see cref="ShelterNpcRoster.All"/>의 미구출 NPC는
    /// <i>전부</i> 같은 던전에 갇힌 방을 얻는다(첫 판이면 연락책 2층 + 대장장이 5층).
    /// 그래서 상태를 스칼라 한 벌로 들면 뒤에 만들어진 동료가 앞의 것을 덮어써서,
    /// 앞 동료의 GameObject 가 <b>참조를 잃은 채 씬에 남는다</b> — 회전 때 다시 투영되지도,
    /// FOV 로 가려지지도, 구출되지도 않는다. 목록으로 드는 이유가 이것이다.
    /// </para>
    /// </summary>
    public partial class IsoPrototypeDemo
    {
        /// <summary>월드에 서 있는 동료 하나. 구출되면 목록에서 빠진다.</summary>
        private sealed class RescueNpcAgent
        {
            public GameObject Root;
            public SpriteRenderer Renderer;
            public GridPos Pos;
            public int FloorIndex;
            public string Id;
        }

        private readonly List<RescueNpcAgent> _rescueNpcs = new List<RescueNpcAgent>();

        /// <summary>이 판에 구출한 동료의 표시명들 — 판 종료 화면이 알린다.</summary>
        public string RescuedThisRun { get; private set; }

        private void ResetRescueForBuild()
        {
            // GameObject 는 비주얼 루트째 다시 만들어지므로 목록만 비운다.
            _rescueNpcs.Clear();
            RescuedThisRun = null;
        }

        /// <summary>갇힌 방의 동료를 세운다. 구출 대상이 없는 층은 그냥 지나간다.</summary>
        private void CreateRescueNpc(DungeonFloorInfo floor)
        {
            if (hubMode || !floor.RescueNpc.HasValue) return;

            ShelterNpcDefinition npc = ShelterNpcRoster.ById(floor.RescueNpcId);
            if (npc == null) return;

            GridPos pos = floor.RescueNpc.Value;
            // 동료는 적도 플레이어도 아니라 제3의 실루엣이어야 한다 — 묶인 손이 그 표식이다.
            GameObject root = CreateActorSprite(
                $"Rescue {npc.Id}",
                ActorSprites.GetRescueNpcSprite(npc.Id),
                pos,
                out SpriteRenderer renderer,
                microOffset: 1);

            _rescueNpcs.Add(new RescueNpcAgent
            {
                Root = root,
                Renderer = renderer,
                Pos = pos,
                FloorIndex = floor.FloorIndex,
                Id = npc.Id
            });
        }

        /// <summary>시점 회전/뷰 갱신 때 같은 GridPos로 다시 투영한다.</summary>
        private void ApplyRescueNpcView()
        {
            foreach (RescueNpcAgent agent in _rescueNpcs)
            {
                if (agent.Root == null || agent.Renderer == null) continue;
                agent.Root.transform.position = VisualPosition(agent.Pos);
                agent.Renderer.sortingOrder = _grid.iso.SortingOrder(agent.Pos, 1);
            }
        }

        /// <summary>동료도 FOV를 따른다 — 활성 층에서 실제로 보이는 칸일 때만 드러난다.</summary>
        private void RefreshRescueNpcVisibility()
        {
            foreach (RescueNpcAgent agent in _rescueNpcs)
            {
                if (agent.Root == null || agent.Renderer == null) continue;

                bool onActiveFloor = agent.FloorIndex == _activeFloorIndex;
                bool visible = viewMode == DungeonViewMode.DebugAll ||
                               (onActiveFloor && _visibleTiles.Contains(agent.Pos));
                SetSpriteHierarchyVisible(agent.Root, visible);

                Color tint = ElevationTint(agent.Pos);
                Color light = TileLightColor(agent.Pos);
                agent.Renderer.color = new Color(
                    tint.r * light.r, tint.g * light.g, tint.b * light.b, 1f);
            }
        }

        /// <summary>이 칸에 서 있는 동료. 없으면 null.</summary>
        private RescueNpcAgent RescueNpcAt(GridPos pos)
        {
            foreach (RescueNpcAgent agent in _rescueNpcs)
                if (agent.Pos == pos && agent.FloorIndex == _activeFloorIndex)
                    return agent;
            return null;
        }

        /// <summary>이 칸에 구출할 동료가 서 있는가.</summary>
        private bool IsRescueNpcAt(GridPos pos) => RescueNpcAt(pos) != null;

        /// <summary>
        /// 동료를 구출한다. <b>즉시 저장한다</b> — 이 판에 죽어도 합류는 남는다.
        /// 구출은 소지품이 아니라 진행이고, 실패한 판도 전진이어야 하기 때문이다.
        /// </summary>
        private bool TryRescueNpcAt(GridPos pos)
        {
            RescueNpcAgent agent = RescueNpcAt(pos);
            if (agent == null) return false;

            ShelterNpcDefinition npc = ShelterNpcRoster.ById(agent.Id);
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
            // 목록에서도 빼야 남은 동료의 판정이 이 자리에 걸리지 않는다.
            _rescueNpcs.Remove(agent);
            if (agent.Root != null) agent.Root.SetActive(false);

            InteractionFeedback?.Invoke(npc.RescueTitle);
            DungeonEntryCue?.Invoke(npc.RescueTitle, npc.RescueDetail);
            return true;
        }
    }
}
