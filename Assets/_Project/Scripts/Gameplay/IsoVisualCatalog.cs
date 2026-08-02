using System.Collections.Generic;
using ProjectC.Core;
using UnityEngine;

namespace ProjectC.Gameplay
{
    /// <summary>
    /// 논리 타일/오브젝트를 교체 가능한 픽셀아트 스프라이트에 연결한다.
    /// 비어 있는 슬롯은 프로토타입 런타임 스프라이트로 대체된다.
    /// </summary>
    [CreateAssetMenu(fileName = "IsoVisualCatalog", menuName = "Project-C/Isometric Visual Catalog")]
    public class IsoVisualCatalog : ScriptableObject
    {
        [Header("던전 공통 톤 (Torchstone 18색의 런타임 역할)")]
        [Tooltip("pc-void — 월드 바깥과 깊은 개구부")]
        public Color32 dungeonVoid = new Color32(5, 7, 12, 255);
        [Tooltip("pc-panel — void와 구분되는 미탐색 영역의 청흑 안개")]
        public Color32 dungeonFog = new Color32(10, 13, 19, 255);
        [Tooltip("ash — 실제 방 구조를 노출하지 않는 전체 생성 영역의 외곽")]
        public Color32 dungeonFogEdge = new Color32(31, 31, 27, 228);
        [Tooltip("pc-void — 모든 던전 환경 실루엣의 최암부")]
        public Color32 dungeonOutline = new Color32(5, 7, 12, 255);
        [Tooltip("pc-panel — 바닥 줄눈과 얇은 경계")]
        public Color32 dungeonSeam = new Color32(10, 13, 19, 255);
        [Tooltip("grey-1 — 냉회색 콘크리트의 그림자")]
        public Color32 dungeonStoneShadow = new Color32(44, 49, 56, 255);
        [Tooltip("grey-2 — 조명 전의 공통 콘크리트 중간톤")]
        public Color32 dungeonStone = new Color32(59, 63, 69, 255);
        [Tooltip("grey-3 — 같은 층 안 단차의 밝은 면")]
        public Color32 dungeonStoneLight = new Color32(84, 91, 97, 255);
        [Tooltip("dark-cool — 벽 패널의 최암부")]
        public Color32 dungeonWallShadow = new Color32(21, 23, 29, 255);
        [Tooltip("grey-1 — 폐 아케이드 콘크리트/강철 벽 몸통")]
        public Color32 dungeonWall = new Color32(44, 49, 56, 255);
        [Tooltip("grey-3 — 벽 모서리와 금속 베벨")]
        public Color32 dungeonWallLight = new Color32(84, 91, 97, 255);
        public Color32 dungeonMoss = new Color32(127, 178, 65, 255);
        public Color32 dungeonWood = new Color32(74, 64, 56, 255);
        public Color32 dungeonWoodLight = new Color32(154, 107, 34, 255);
        public Color32 dungeonIron = new Color32(10, 13, 19, 255);
        [Tooltip("pc-torch — 횃불·안전 경로의 국소 물리광")]
        public Color32 dungeonAmber = new Color32(255, 189, 65, 255);
        [Tooltip("pc-gold — 불꽃 중심과 현재 목표")]
        public Color32 dungeonAmberCore = new Color32(255, 213, 84, 255);
        [Tooltip("pc-teal — Hole·게이트·해금된 경로의 국소 신호색")]
        public Color32 dungeonMagic = new Color32(79, 167, 160, 255);
        [Tooltip("sig-neon-cyan — 장식용 충전·서비스 광원. 이상현상 틸과 구분한다")]
        public Color32 dungeonNeonCyan = new Color32(61, 225, 232, 255);
        [Tooltip("sig-neon-magenta — 장식용 아케이드·광고 광원")]
        public Color32 dungeonNeonMagenta = new Color32(230, 68, 184, 255);

        [Header("배경")]
        [Tooltip("미탐색 구조를 노출하지 않는 전체 생성 영역의 교체 가능한 배경")]
        public Sprite dungeonBackdrop;

        [Header("타일")]
        [Tooltip("B1~B3 기본 높이 바닥")]
        public Sprite floor;
        [Tooltip("B1~B3 단차 바닥")]
        public Sprite raisedFloor;
        [Tooltip("이전 카탈로그 호환용 깊은 층 공용 바닥")]
        public Sprite lowerFloor;
        [Tooltip("B4~B6 기본 높이 바닥")]
        public Sprite midFloor;
        [Tooltip("B4~B6 단차 바닥")]
        public Sprite midRaisedFloor;
        [Tooltip("B7~B9 기본 높이 바닥")]
        public Sprite deepFloor;
        [Tooltip("B7~B9 단차 바닥")]
        public Sprite deepRaisedFloor;
        [Tooltip("B10 기본 높이 바닥")]
        public Sprite bossFloor;
        [Tooltip("B10 단차 바닥")]
        public Sprite bossRaisedFloor;

        [Header("Facility 바닥 드레싱 (슬롯명은 구 폐병원 hospital* 유지 — 아케이드 재발주 예정)")]
        [Tooltip("Facility 지역의 희소 바닥 변주 — 서비스 그레이트")]
        public Sprite hospitalFloorGrate;
        [Tooltip("Facility 지역의 희소 바닥 변주 — 균열과 오염")]
        public Sprite hospitalFloorCracked;
        [Tooltip("Facility 지역의 희소 바닥 변주 — 열린 서비스 패널")]
        public Sprite hospitalFloorService;

        [Header("B2 주차·서비스 구역 바닥 드레싱")]
        [Tooltip("기본 바닥과 합성된 낮은 주차 범퍼 — 비충돌 장식")]
        public Sprite b2ParkingWheelStopFloor;
        [Tooltip("기본 바닥과 합성된 쓰러진 아케이드 안내판 — 비충돌 장식")]
        public Sprite b2FallenWayfindingFloor;
        [Tooltip("B2 진출부의 평평한 균열·마모 바닥 — 비충돌 장식")]
        public Sprite b2CrackedFloor;
        [Tooltip("주차 범퍼 4분기 화면 방향 — view 0..3")]
        public Sprite b2ParkingWheelStopFloorView0;
        public Sprite b2ParkingWheelStopFloorView1;
        public Sprite b2ParkingWheelStopFloorView2;
        public Sprite b2ParkingWheelStopFloorView3;
        [Tooltip("쓰러진 안내판 4분기 화면 방향 — view 0..3")]
        public Sprite b2FallenWayfindingFloorView0;
        public Sprite b2FallenWayfindingFloorView1;
        public Sprite b2FallenWayfindingFloorView2;
        public Sprite b2FallenWayfindingFloorView3;
        [Tooltip("배럴 유출 방지 베이의 service/ring 셀 — view 0..3")]
        public Sprite b2BarrelBayServiceFloorView0;
        public Sprite b2BarrelBayServiceFloorView1;
        public Sprite b2BarrelBayServiceFloorView2;
        public Sprite b2BarrelBayServiceFloorView3;
        [Tooltip("배럴 유출 방지 베이의 drain/grate 셀 — view 0..3")]
        public Sprite b2BarrelBayDrainFloorView0;
        public Sprite b2BarrelBayDrainFloorView1;
        public Sprite b2BarrelBayDrainFloorView2;
        public Sprite b2BarrelBayDrainFloorView3;
        [Tooltip("2×2 연속 주차 바닥의 물리 role 0 — view 0..3")]
        public Sprite b2MacroFloorRole0View0;
        public Sprite b2MacroFloorRole0View1;
        public Sprite b2MacroFloorRole0View2;
        public Sprite b2MacroFloorRole0View3;
        [Tooltip("2×2 연속 주차 바닥의 물리 role 1 — view 0..3")]
        public Sprite b2MacroFloorRole1View0;
        public Sprite b2MacroFloorRole1View1;
        public Sprite b2MacroFloorRole1View2;
        public Sprite b2MacroFloorRole1View3;
        [Tooltip("2×2 연속 주차 바닥의 물리 role 2 — view 0..3")]
        public Sprite b2MacroFloorRole2View0;
        public Sprite b2MacroFloorRole2View1;
        public Sprite b2MacroFloorRole2View2;
        public Sprite b2MacroFloorRole2View3;
        [Tooltip("2×2 연속 주차 바닥의 물리 role 3 — view 0..3")]
        public Sprite b2MacroFloorRole3View0;
        public Sprite b2MacroFloorRole3View1;
        public Sprite b2MacroFloorRole3View2;
        public Sprite b2MacroFloorRole3View3;

        public Sprite stairs;
        public Sprite ladder;
        public Sprite stairsUp;
        public Sprite stairsDown;
        public Sprite hole;
        public Sprite weakFloor;
        public Sprite doorClosed;
        public Sprite doorOpen;

        [Header("방향 타일 (화면 기준)")]
        [Tooltip("화면에서 높은 쪽이 오른쪽인 같은 층 계단")]
        public Sprite stairsRisingRight;
        public Sprite stairsRisingLeft;
        public Sprite stairsUpRisingRight;
        public Sprite stairsUpRisingLeft;
        public Sprite stairsDownRisingRight;
        public Sprite stairsDownRisingLeft;
        public Sprite doorClosedRisingRight;
        public Sprite doorClosedRisingLeft;
        public Sprite doorOpenRisingRight;
        public Sprite doorOpenRisingLeft;

        [Header("후면 벽 (화면 기준)")]
        public Sprite rearWallRisingRight;
        public Sprite rearWallRisingLeft;
        public Sprite rearWallTorchRisingRight;
        public Sprite rearWallTorchRisingLeft;
        public Sprite hospitalWallPipesRisingRight;
        public Sprite hospitalWallPipesRisingLeft;
        public Sprite hospitalWallWindowRisingRight;
        public Sprite hospitalWallWindowRisingLeft;
        public Sprite hospitalWallCabinetRisingRight;
        public Sprite hospitalWallCabinetRisingLeft;

        [Header("B2 연속 서비스 벽 (물리 x 순서 0..2)")]
        public Sprite b2ServiceWallSegment0RisingRight;
        public Sprite b2ServiceWallSegment0RisingLeft;
        public Sprite b2ServiceWallSegment1RisingRight;
        public Sprite b2ServiceWallSegment1RisingLeft;
        public Sprite b2ServiceWallSegment2RisingRight;
        public Sprite b2ServiceWallSegment2RisingLeft;

        [Header("액터와 소품")]
        public Sprite player;
        public Sprite knight;
        public Sprite ranger;
        public Sprite alchemist;
        public Sprite goblin;
        public Sprite skeleton;
        public Sprite slime;
        public Sprite slinger;
        public Sprite arcDrone;
        public Sprite graveWarden;
        public Sprite merchant;
        public Sprite explosiveBarrel;
        public Sprite hubCampfire;
        public Sprite hubStash;
        public Sprite hubPortal;
        public Sprite playerFootprint;
        public Sprite selection;

        /// <summary>
        /// 원정자 스프라이트. 직업이 사라졌으므로 하나뿐이다 —
        /// 옛 <c>knight</c> 슬롯을 그대로 쓴다(씬 인스펙터 연결을 끊지 않으려고 필드명은 둔다).
        /// <c>ranger</c>/<c>alchemist</c> 슬롯은 아직 비워 두지 않았다: 지역별 원정자 스킨이
        /// 생기면 그 자리를 다시 쓸 수 있어서다.
        /// </summary>
        public Sprite SurvivorSprite => knight != null ? knight : player;

        /// <summary>
        /// 아키타입 ID → 카탈로그 슬롯 필드명. <see cref="MonsterFor"/>와
        /// <see cref="MonsterAnimationsFor"/>가 같은 매핑을 공유하는 단일 출처다.
        /// 미등록은 null — goblin 폴백 금지 규칙의 뿌리.
        /// </summary>
        private static string MonsterSlotKey(string archetypeId)
        {
            switch (archetypeId)
            {
                case "Goblin": return "goblin";
                case "Skeleton": return "skeleton";
                case "Slime": return "slime";
                case "Slinger": return "slinger";
                case "ArcDrone": return "arcDrone";
                case "GraveWarden": return "graveWarden";
                default: return null;
            }
        }

        /// <summary>
        /// 미등록 아키타입은 goblin이 아니라 null을 돌려준다 — null이어야 호출부가
        /// 아키타입 전용 절차 생성 폴백으로 내려가고, 몬스터끼리 같은 그림으로 뭉개지지 않는다.
        /// (Slinger·GraveWarden이 goblin 폴백에 가려 전용 실루엣을 잃었던 회귀의 방지선.)
        /// </summary>
        public Sprite MonsterFor(string archetypeId)
        {
            switch (MonsterSlotKey(archetypeId))
            {
                case "goblin": return goblin;
                case "skeleton": return skeleton;
                case "slime": return slime;
                case "slinger": return slinger;
                case "arcDrone": return arcDrone;
                case "graveWarden": return graveWarden;
                default: return null;
            }
        }

        [Header("액터 애니메이션 (Aseprite 베이크 산출물 — 손으로 편집하지 않는다)")]
        public List<ActorAnimationSet> actorAnimations = new List<ActorAnimationSet>();

        [Header("환경 애니메이션 (Aseprite idle 태그 베이크 산출물)")]
        public List<EnvironmentAnimationSet> environmentAnimations =
            new List<EnvironmentAnimationSet>();

        /// <summary>actorKey = Sprite 슬롯 필드명 계약. 없으면 null(정지 1프레임 유지).</summary>
        public ActorAnimationSet AnimationsFor(string actorKey)
        {
            if (actorAnimations == null || string.IsNullOrEmpty(actorKey)) return null;
            for (int i = 0; i < actorAnimations.Count; i++)
            {
                ActorAnimationSet set = actorAnimations[i];
                if (set != null && set.HasClips &&
                    string.Equals(set.actorKey, actorKey, System.StringComparison.Ordinal))
                    return set;
            }

            return null;
        }

        /// <summary>환경/소품 Catalog 슬롯 필드명으로 idle 루프 세트를 찾는다.</summary>
        public EnvironmentAnimationSet EnvironmentAnimationsFor(string slotKey)
        {
            if (environmentAnimations == null || string.IsNullOrEmpty(slotKey))
                return null;
            for (int i = 0; i < environmentAnimations.Count; i++)
            {
                EnvironmentAnimationSet set = environmentAnimations[i];
                if (set != null && set.HasClips &&
                    string.Equals(
                        set.slotKey,
                        slotKey,
                        System.StringComparison.Ordinal))
                    return set;
            }

            return null;
        }

        /// <summary>원정자 애니 — <see cref="SurvivorSprite"/>와 같은 knight→player 규칙.</summary>
        public ActorAnimationSet SurvivorAnimations
        {
            get
            {
                ActorAnimationSet knightSet = AnimationsFor("knight");
                return knightSet != null ? knightSet : AnimationsFor("player");
            }
        }

        /// <summary>미등록 아키타입은 null — <see cref="MonsterFor"/>와 같은 방지선.</summary>
        public ActorAnimationSet MonsterAnimationsFor(string archetypeId)
        {
            string slotKey = MonsterSlotKey(archetypeId);
            return slotKey != null ? AnimationsFor(slotKey) : null;
        }

        [Header("아이템")]
        public Sprite potion;
        public Sprite bomb;
        public Sprite frostBomb;
        public Sprite oilFlask;
        public Sprite throwingKnife;
        public Sprite recallScroll;
        public Sprite coinPouch;
        public Sprite gemstone;
        public Sprite relic;
        public Sprite herb;
        public Sprite blastPowder;
        public Sprite frostShard;

        public Sprite ItemFor(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Potion: return potion;
                case ItemKind.Bomb: return bomb;
                case ItemKind.FrostBomb: return frostBomb;
                case ItemKind.OilFlask: return oilFlask;
                case ItemKind.ThrowingKnife: return throwingKnife;
                case ItemKind.RecallScroll: return recallScroll;
                case ItemKind.CoinPouch: return coinPouch;
                case ItemKind.Gemstone: return gemstone;
                case ItemKind.Relic: return relic;
                case ItemKind.Herb: return herb;
                case ItemKind.BlastPowder: return blastPowder;
                case ItemKind.FrostShard: return frostShard;
                default: return null;
            }
        }

        /// <summary>
        /// 모든 던전의 공통 석재색. 단차는 색상군을 바꾸지 않고 같은 색의 명도만 올린다.
        /// 깊이별 변주는 이 공통값 위에 제한된 보정으로 별도 적용한다.
        /// </summary>
        public Color32 DungeonSurfaceFor(DungeonVisualContext context)
        {
            return context.IsRaised ? dungeonStoneLight : dungeonStone;
        }

        public Sprite TileFor(TileKind kind, DungeonVisualContext context)
        {
            switch (kind)
            {
                case TileKind.Stairs: return stairs;
                // Ladder의 발밑은 일반 바닥이다 — ladder 슬롯의 주인은 바닥 타일이 아니라
                // "세워진 사다리" 랜드마크 오브젝트(CreateVerticalLandmarks)다. 여기서 ladder를
                // 반환하면 랜드마크와 이중 표시가 된다.
                case TileKind.Ladder: return FloorFor(context);
                case TileKind.StairsUp: return stairsUp != null ? stairsUp : stairs;
                case TileKind.StairsDown: return stairsDown != null ? stairsDown : stairs;
                case TileKind.Hole: return hole;
                case TileKind.WeakFloor: return weakFloor;
                case TileKind.DoorClosed: return doorClosed;
                case TileKind.DoorOpen: return doorOpen != null ? doorOpen : floor;
                default:
                    return FloorFor(context);
            }
        }

        /// <summary>
        /// 이 밴드의 바닥이 전용 슬롯 없이 공용 바닥으로 폴백되는 상태인가.
        /// 절차 밴드 오버레이(임시 — 밴드 아트 도착 전 대행)는 이때만 켠다.
        /// 전용 아트가 슬롯에 연결되는 순간 자동으로 꺼진다. 판정은 FloorFor의 선택 사슬과
        /// 동일해야 한다 — 어긋나면 전용 아트 위에 오버레이가 얹히거나 폴백이 민짜가 된다.
        /// </summary>
        public bool BandFloorFallsBackToShared(DungeonVisualContext context)
        {
            Sprite flat;
            Sprite raised;
            switch (context.DepthBand)
            {
                case DungeonDepthBand.Mid:
                    flat = midFloor;
                    raised = midRaisedFloor;
                    break;
                case DungeonDepthBand.Deep:
                    flat = deepFloor;
                    raised = deepRaisedFloor;
                    break;
                case DungeonDepthBand.Boss:
                    flat = bossFloor != null ? bossFloor : deepFloor;
                    raised = bossRaisedFloor;
                    break;
                default:
                    return false; // Shallow은 공용 바닥이 곧 정답 — 오버레이 없음.
            }

            return (context.IsRaised && raised != null ? raised : flat) == null;
        }

        private Sprite FloorFor(DungeonVisualContext context)
        {
            Sprite flat;
            Sprite raised;
            switch (context.DepthBand)
            {
                case DungeonDepthBand.Mid:
                    flat = midFloor != null ? midFloor : lowerFloor != null ? lowerFloor : floor;
                    raised = midRaisedFloor;
                    break;
                case DungeonDepthBand.Deep:
                    flat = deepFloor != null ? deepFloor : lowerFloor != null ? lowerFloor : floor;
                    raised = deepRaisedFloor;
                    break;
                case DungeonDepthBand.Boss:
                    flat = bossFloor != null
                        ? bossFloor
                        : deepFloor != null
                            ? deepFloor
                            : lowerFloor != null ? lowerFloor : floor;
                    raised = bossRaisedFloor;
                    break;
                default:
                    flat = floor;
                    raised = raisedFloor;
                    break;
            }

            return context.IsRaised && raised != null ? raised : flat;
        }

        public Sprite StairsFor(TileKind kind, bool risesRight)
        {
            Sprite directed;
            switch (kind)
            {
                case TileKind.Stairs:
                    directed = risesRight ? stairsRisingRight : stairsRisingLeft;
                    break;
                case TileKind.StairsUp:
                    directed = risesRight ? stairsUpRisingRight : stairsUpRisingLeft;
                    break;
                case TileKind.StairsDown:
                    directed = risesRight ? stairsDownRisingRight : stairsDownRisingLeft;
                    break;
                default:
                    return null;
            }

            return directed != null
                ? directed
                : TileFor(kind, DungeonVisualContext.Preview());
        }

        public Sprite DoorFor(TileKind kind, bool risesRight)
        {
            Sprite directed;
            switch (kind)
            {
                case TileKind.DoorClosed:
                    directed = risesRight ? doorClosedRisingRight : doorClosedRisingLeft;
                    break;
                case TileKind.DoorOpen:
                    directed = risesRight ? doorOpenRisingRight : doorOpenRisingLeft;
                    break;
                default:
                    return null;
            }

            return directed != null
                ? directed
                : TileFor(kind, DungeonVisualContext.Preview());
        }

        /// <summary>
        /// Facility 바닥의 seed 고정 드레싱. 0..7 중 세 값만 슬롯을 사용해 방의 기본 재질이
        /// 장식에 묻히지 않게 한다. 비어 있는 슬롯은 호출부가 공용 바닥으로 폴백한다.
        /// </summary>
        public Sprite HospitalFloorFor(int variation)
        {
            switch ((variation % 8 + 8) % 8)
            {
                case 0: return hospitalFloorGrate;
                case 3: return hospitalFloorCracked;
                case 6: return hospitalFloorService;
                default: return null;
            }
        }

        public bool HasB2ParkingWheelStopFloor =>
            b2ParkingWheelStopFloor != null ||
            b2ParkingWheelStopFloorView0 != null ||
            b2ParkingWheelStopFloorView1 != null ||
            b2ParkingWheelStopFloorView2 != null ||
            b2ParkingWheelStopFloorView3 != null;

        public bool HasB2FallenWayfindingFloor =>
            b2FallenWayfindingFloor != null ||
            b2FallenWayfindingFloorView0 != null ||
            b2FallenWayfindingFloorView1 != null ||
            b2FallenWayfindingFloorView2 != null ||
            b2FallenWayfindingFloorView3 != null;

        public Sprite B2ParkingWheelStopFloorFor(int viewQuarterTurns)
        {
            return B2FloorDressingFor(
                viewQuarterTurns,
                b2ParkingWheelStopFloor,
                b2ParkingWheelStopFloorView0,
                b2ParkingWheelStopFloorView1,
                b2ParkingWheelStopFloorView2,
                b2ParkingWheelStopFloorView3);
        }

        public Sprite B2FallenWayfindingFloorFor(int viewQuarterTurns)
        {
            return B2FloorDressingFor(
                viewQuarterTurns,
                b2FallenWayfindingFloor,
                b2FallenWayfindingFloorView0,
                b2FallenWayfindingFloorView1,
                b2FallenWayfindingFloorView2,
                b2FallenWayfindingFloorView3);
        }

        public Sprite B2CrackedFloorFor() =>
            b2CrackedFloor != null ? b2CrackedFloor : hospitalFloorCracked;

        /// <summary>
        /// B2 폭발통 아래 service 셀과 인접 drain 셀의 한 쌍. 여덟 슬롯이 모두
        /// 승격됐을 때만 켜서 회전 중 한쪽 셀만 구판 베이지 타일로 돌아가는 일을 막는다.
        /// </summary>
        public bool HasCompleteB2BarrelBayFloor =>
            b2BarrelBayServiceFloorView0 != null &&
            b2BarrelBayServiceFloorView1 != null &&
            b2BarrelBayServiceFloorView2 != null &&
            b2BarrelBayServiceFloorView3 != null &&
            b2BarrelBayDrainFloorView0 != null &&
            b2BarrelBayDrainFloorView1 != null &&
            b2BarrelBayDrainFloorView2 != null &&
            b2BarrelBayDrainFloorView3 != null;

        public Sprite B2BarrelBayFloorFor(bool drain, int viewQuarterTurns)
        {
            if (!HasCompleteB2BarrelBayFloor)
                return null;

            int view = NormalizeQuarterTurns(viewQuarterTurns);
            if (drain)
            {
                switch (view)
                {
                    case 1: return b2BarrelBayDrainFloorView1;
                    case 2: return b2BarrelBayDrainFloorView2;
                    case 3: return b2BarrelBayDrainFloorView3;
                    default: return b2BarrelBayDrainFloorView0;
                }
            }

            switch (view)
            {
                case 1: return b2BarrelBayServiceFloorView1;
                case 2: return b2BarrelBayServiceFloorView2;
                case 3: return b2BarrelBayServiceFloorView3;
                default: return b2BarrelBayServiceFloorView0;
            }
        }

        /// <summary>
        /// 2×2 연결 바닥은 16개 슬롯 전체가 하나의 원자적 자산이다. 일부만 연결되면
        /// 내부 무늬가 끊기므로 모든 role에서 null을 돌려 일반 바닥 폴백을 강제한다.
        /// </summary>
        public bool HasCompleteB2MacroFloor =>
            b2MacroFloorRole0View0 != null &&
            b2MacroFloorRole0View1 != null &&
            b2MacroFloorRole0View2 != null &&
            b2MacroFloorRole0View3 != null &&
            b2MacroFloorRole1View0 != null &&
            b2MacroFloorRole1View1 != null &&
            b2MacroFloorRole1View2 != null &&
            b2MacroFloorRole1View3 != null &&
            b2MacroFloorRole2View0 != null &&
            b2MacroFloorRole2View1 != null &&
            b2MacroFloorRole2View2 != null &&
            b2MacroFloorRole2View3 != null &&
            b2MacroFloorRole3View0 != null &&
            b2MacroFloorRole3View1 != null &&
            b2MacroFloorRole3View2 != null &&
            b2MacroFloorRole3View3 != null;

        public Sprite B2MacroFloorFor(int role, int viewQuarterTurns)
        {
            if (!HasCompleteB2MacroFloor || role < 0 || role > 3)
                return null;

            int view = NormalizeQuarterTurns(viewQuarterTurns);
            switch (role)
            {
                case 0:
                    switch (view)
                    {
                        case 1: return b2MacroFloorRole0View1;
                        case 2: return b2MacroFloorRole0View2;
                        case 3: return b2MacroFloorRole0View3;
                        default: return b2MacroFloorRole0View0;
                    }
                case 1:
                    switch (view)
                    {
                        case 1: return b2MacroFloorRole1View1;
                        case 2: return b2MacroFloorRole1View2;
                        case 3: return b2MacroFloorRole1View3;
                        default: return b2MacroFloorRole1View0;
                    }
                case 2:
                    switch (view)
                    {
                        case 1: return b2MacroFloorRole2View1;
                        case 2: return b2MacroFloorRole2View2;
                        case 3: return b2MacroFloorRole2View3;
                        default: return b2MacroFloorRole2View0;
                    }
                default:
                    switch (view)
                    {
                        case 1: return b2MacroFloorRole3View1;
                        case 2: return b2MacroFloorRole3View2;
                        case 3: return b2MacroFloorRole3View3;
                        default: return b2MacroFloorRole3View0;
                    }
            }
        }

        private static Sprite B2FloorDressingFor(
            int viewQuarterTurns,
            Sprite legacy,
            Sprite view0,
            Sprite view1,
            Sprite view2,
            Sprite view3)
        {
            int view = NormalizeQuarterTurns(viewQuarterTurns);
            bool complete = view0 != null && view1 != null &&
                            view2 != null && view3 != null;
            if (complete)
            {
                switch (view)
                {
                    case 1: return view1;
                    case 2: return view2;
                    case 3: return view3;
                    default: return view0;
                }
            }

            // 부분 승격 중에는 방향형과 무방향형을 섞지 않는다. legacy가 있으면 네
            // 시점 모두 같은 완성형 타일을 써서 회전 중 소품이 바뀌거나 사라지지 않게 한다.
            if (legacy != null) return legacy;

            // legacy도 없으면 같은 화면 축 parity(0/2 또는 1/3)를 우선한다. 요청 parity가
            // 통째로 비어 있는 비정상 상태에서도 첫 존재 슬롯으로 내려가 렌더링은 유지한다.
            Sprite sameAxis;
            switch (view)
            {
                case 1: sameAxis = view1 != null ? view1 : view3; break;
                case 2: sameAxis = view2 != null ? view2 : view0; break;
                case 3: sameAxis = view3 != null ? view3 : view1; break;
                default: sameAxis = view0 != null ? view0 : view2; break;
            }
            return sameAxis != null
                ? sameAxis
                : view0 ?? view1 ?? view2 ?? view3;
        }

        private static int NormalizeQuarterTurns(int value)
        {
            int normalized = value % 4;
            return normalized < 0 ? normalized + 4 : normalized;
        }

        public Sprite RearWallFor(bool torch, bool risesRight)
        {
            return RearWallFor(torch, risesRight, -1);
        }

        /// <summary>
        /// B2 시작방 전용 연속 벽. 여섯 방향 슬롯이 모두 승격됐을 때만 사용해
        /// 부분 임포트 중 한 칸만 새 아트로 바뀌는 이음새 회귀를 막는다.
        /// </summary>
        public Sprite B2ServiceWallSegmentFor(int segment, bool risesRight)
        {
            bool complete =
                b2ServiceWallSegment0RisingRight != null &&
                b2ServiceWallSegment0RisingLeft != null &&
                b2ServiceWallSegment1RisingRight != null &&
                b2ServiceWallSegment1RisingLeft != null &&
                b2ServiceWallSegment2RisingRight != null &&
                b2ServiceWallSegment2RisingLeft != null;
            if (!complete)
                return null;

            switch (segment)
            {
                case 0:
                    return risesRight
                        ? b2ServiceWallSegment0RisingRight
                        : b2ServiceWallSegment0RisingLeft;
                case 1:
                    return risesRight
                        ? b2ServiceWallSegment1RisingRight
                        : b2ServiceWallSegment1RisingLeft;
                case 2:
                    return risesRight
                        ? b2ServiceWallSegment2RisingRight
                        : b2ServiceWallSegment2RisingLeft;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 벽 등잔이 없는 Facility 후면 벽 일부만 드레싱으로 교체한다(hospital* 슬롯명은 구판 유지).
        /// decoration은 물리 벽면 좌표로 만든 0..7 값이다. 시점을 돌려도 같은 설비가 남아야 한다.
        /// </summary>
        public Sprite RearWallFor(bool torch, bool risesRight, int decoration)
        {
            Sprite directed = torch
                ? (risesRight ? rearWallTorchRisingRight : rearWallTorchRisingLeft)
                : null;
            if (directed != null) return directed;

            if (!torch)
            {
                switch (decoration)
                {
                    case 0:
                        directed = risesRight
                            ? hospitalWallPipesRisingRight
                            : hospitalWallPipesRisingLeft;
                        break;
                    case 1:
                        directed = risesRight
                            ? hospitalWallWindowRisingRight
                            : hospitalWallWindowRisingLeft;
                        break;
                    case 2:
                        directed = risesRight
                            ? hospitalWallCabinetRisingRight
                            : hospitalWallCabinetRisingLeft;
                        break;
                }
                if (directed != null) return directed;
            }

            return risesRight ? rearWallRisingRight : rearWallRisingLeft;
        }

        [Header("전투 이펙트")]
        public Sprite fxImpactPhysical;
        public Sprite fxImpactFire;
        public Sprite fxImpactFrost;
        public Sprite fxImpactHeavy;
        public Sprite fxStatusBurn;
        public Sprite fxStatusFreeze;

        // 승격된 FX 아트를 우선 제공한다. 비어 있으면 null을 돌려주고,
        // 호출부(IsoPrototypeDemo.CombatFx)가 기존 절차 생성으로 폴백한다.
        public Sprite ImpactFx(CombatImpactKind kind)
        {
            switch (kind)
            {
                case CombatImpactKind.Fire: return fxImpactFire;
                case CombatImpactKind.Frost: return fxImpactFrost;
                case CombatImpactKind.Heavy: return fxImpactHeavy;
                default: return fxImpactPhysical;
            }
        }

        public Sprite StatusFx(StatusKind kind)
        {
            return kind == StatusKind.Burn ? fxStatusBurn : fxStatusFreeze;
        }
    }
}
