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
        [Tooltip("pc-inset — 미탐색 영역의 청흑 안개")]
        public Color32 dungeonFog = new Color32(7, 9, 14, 210);
        [Tooltip("pc-panel — 안개 외곽")]
        public Color32 dungeonFogEdge = new Color32(10, 13, 19, 228);
        [Tooltip("pc-void — 모든 던전 환경 실루엣의 최암부")]
        public Color32 dungeonOutline = new Color32(5, 7, 12, 255);
        [Tooltip("pc-panel — 바닥 줄눈과 얇은 경계")]
        public Color32 dungeonSeam = new Color32(10, 13, 19, 255);
        public Color32 dungeonStoneShadow = new Color32(10, 13, 19, 255);
        [Tooltip("pc-stone-dim — 횃불에 데워진 공통 석재 기준색")]
        public Color32 dungeonStone = new Color32(74, 64, 56, 255);
        [Tooltip("pc-stone — 같은 층 안 단차의 밝은 면")]
        public Color32 dungeonStoneLight = new Color32(152, 134, 111, 255);
        public Color32 dungeonWallShadow = new Color32(10, 13, 19, 255);
        public Color32 dungeonWall = new Color32(74, 64, 56, 255);
        public Color32 dungeonWallLight = new Color32(207, 192, 174, 255);
        public Color32 dungeonMoss = new Color32(127, 178, 65, 255);
        public Color32 dungeonWood = new Color32(74, 64, 56, 255);
        public Color32 dungeonWoodLight = new Color32(154, 107, 34, 255);
        public Color32 dungeonIron = new Color32(10, 13, 19, 255);
        [Tooltip("pc-torch — 횃불·안전 경로의 국소 물리광")]
        public Color32 dungeonAmber = new Color32(255, 189, 65, 255);
        [Tooltip("pc-gold — 불꽃 중심과 현재 목표")]
        public Color32 dungeonAmberCore = new Color32(255, 213, 84, 255);
        [Tooltip("pc-teal — Hole·포탈·마법 경로의 국소 신호색")]
        public Color32 dungeonMagic = new Color32(79, 167, 160, 255);

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

        [Header("액터와 소품")]
        public Sprite player;
        public Sprite knight;
        public Sprite ranger;
        public Sprite alchemist;
        public Sprite goblin;
        public Sprite skeleton;
        public Sprite slime;
        public Sprite slinger;
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
                case "graveWarden": return graveWarden;
                default: return null;
            }
        }

        [Header("액터 애니메이션 (Aseprite 베이크 산출물 — 손으로 편집하지 않는다)")]
        public List<ActorAnimationSet> actorAnimations = new List<ActorAnimationSet>();

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

        public Sprite RearWallFor(bool torch, bool risesRight)
        {
            Sprite directed = torch
                ? (risesRight ? rearWallTorchRisingRight : rearWallTorchRisingLeft)
                : null;
            if (directed != null) return directed;

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
