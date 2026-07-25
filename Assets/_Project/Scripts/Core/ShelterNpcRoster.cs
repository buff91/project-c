using System.Collections.Generic;

namespace ProjectC.Core
{
    /// <summary>쉘터 시설 종류. 미구출 시설은 허브에 <b>프롭도 상호작용도 없다</b>.</summary>
    public enum ShelterFacility
    {
        /// <summary>대장간 — 장비 제작·장착. 장비 4종이 여기에 종속된다.</summary>
        Forge = 0,

        /// <summary>의뢰 게시판 — 계약을 걸고 생환 시 보상을 받는다.</summary>
        BountyBoard = 1
    }

    /// <summary>
    /// 던전에 갇혀 있는 동료 한 명. 구출하면 쉘터에 시설이 생긴다.
    /// </summary>
    public sealed class ShelterNpcDefinition
    {
        public ShelterNpcDefinition(
            string id,
            string displayName,
            ShelterFacility facility,
            int progressIndex,
            string rescueTitle,
            string rescueDetail)
        {
            Id = id;
            DisplayName = displayName;
            Facility = facility;
            ProgressIndex = progressIndex;
            RescueTitle = rescueTitle;
            RescueDetail = rescueDetail;
        }

        public string Id { get; }
        public string DisplayName { get; }
        public ShelterFacility Facility { get; }

        /// <summary>갇힌 방이 생기는 진행 지수. <b>확률이 아니라 고정</b>이다(아래 주석 참조).</summary>
        public int ProgressIndex { get; }

        public string RescueTitle { get; }
        public string RescueDetail { get; }
    }

    /// <summary>
    /// 구출로 열리는 시설 — 엔터 더 건전의 브리치 NPC 계보이고, 로드맵의 "쉘터 재건·성장"의
    /// 실체화다. 거점이 자라는 감각이 재도전 동력의 두 번째 갈래다(첫째는 도구 해금).
    ///
    /// <para>
    /// <b>등장은 확률이 아니라 보장이다.</b> 확률로 두면 운이 나쁜 플레이어는 시설이 영원히
    /// 열리지 않는다 — 해금이 막히면 되돌릴 방법이 없으므로 미구출 NPC가 있으면 그 층에
    /// <b>반드시</b> 갇힌 방이 생긴다. 대신 층이 정해져 있어 어디로 가야 하는지가 분명하다.
    /// </para>
    /// <para>
    /// <b>숨은 방과 겹치지 않는다.</b> 숨은 방은 벽처럼 위장해 못 찾을 수 있으므로,
    /// 거기에 NPC를 두면 진행이 막힌다. 생성기가 NPC 층을 먼저 정하고 숨은 방 후보에서 뺀다.
    /// </para>
    /// <para>
    /// <b>상인·창고는 잠그지 않는다.</b> 파밍 → 골드 → 상점 루프가 첫 판부터 돌아야 하고,
    /// 기록실도 항상 열려 있어야 한다(무엇을 해야 하는지 배우는 창구다).
    /// </para>
    /// </summary>
    public static class ShelterNpcRoster
    {
        /// <summary>
        /// 표시 순서 = 기록실 칸 순서. 진행 지수는 <b>초반(연락책) → 중반(대장장이)</b>으로
        /// 두어 첫 판에 하나는 만날 수 있게 한다 — 둘 다 깊으면 초반 허브가 너무 오래 빈다.
        /// </summary>
        public static readonly IReadOnlyList<ShelterNpcDefinition> All = new[]
        {
            new ShelterNpcDefinition(
                "quartermaster", "연락책", ShelterFacility.BountyBoard,
                progressIndex: 2,
                "연락책을 구출했다",
                "쉘터로 돌아가면 의뢰 게시판을 세운다 — 계약을 걸고 생환하면 보상을 받는다."),
            new ShelterNpcDefinition(
                "smith", "대장장이", ShelterFacility.Forge,
                progressIndex: 5,
                "대장장이를 구출했다",
                "쉘터로 돌아가면 대장간이 다시 불을 켠다 — 장비를 제작하고 장착할 수 있다.")
        };

        public static ShelterNpcDefinition ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (ShelterNpcDefinition npc in All)
                if (npc.Id == id) return npc;
            return null;
        }

        /// <summary>이 시설을 여는 NPC. 없으면 null(항상 열려 있는 시설).</summary>
        public static ShelterNpcDefinition ForFacility(ShelterFacility facility)
        {
            foreach (ShelterNpcDefinition npc in All)
                if (npc.Facility == facility) return npc;
            return null;
        }

        /// <summary>이 시설이 지금 쉘터에 있는가.</summary>
        public static bool IsFacilityOpen(
            ShelterFacility facility,
            IReadOnlyCollection<string> rescued)
        {
            ShelterNpcDefinition npc = ForFacility(facility);
            return npc == null || Contains(rescued, npc.Id);
        }

        /// <summary>
        /// 이 층에 갇힌 방을 둘 NPC. 아직 구출하지 않았고 층이 맞을 때만 돌려준다.
        /// 이미 구출했으면 null — 같은 사람을 두 번 구출하는 방을 만들지 않는다.
        /// </summary>
        public static ShelterNpcDefinition PendingAt(
            int progressIndex,
            IReadOnlyCollection<string> rescued)
        {
            foreach (ShelterNpcDefinition npc in All)
            {
                if (npc.ProgressIndex != progressIndex) continue;
                if (Contains(rescued, npc.Id)) continue;
                return npc;
            }
            return null;
        }

        /// <summary>갇힌 방이 생길 진행 지수들 — 숨은 방 후보에서 빼기 위해 쓴다.</summary>
        public static HashSet<int> PendingFloors(IReadOnlyCollection<string> rescued)
        {
            var floors = new HashSet<int>();
            foreach (ShelterNpcDefinition npc in All)
                if (!Contains(rescued, npc.Id)) floors.Add(npc.ProgressIndex);
            return floors;
        }

        public static int RescuedCount(IReadOnlyCollection<string> rescued)
        {
            int found = 0;
            foreach (ShelterNpcDefinition npc in All)
                if (Contains(rescued, npc.Id)) found++;
            return found;
        }

        public static int TotalCount => All.Count;

        private static bool Contains(IReadOnlyCollection<string> rescued, string id)
        {
            if (rescued == null) return false;
            foreach (string found in rescued)
                if (found == id) return true;
            return false;
        }
    }
}
