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
        [Header("타일")]
        public Sprite floor;
        public Sprite raisedFloor;
        public Sprite lowerFloor;
        public Sprite stairs;
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
        public Sprite goblin;
        public Sprite skeleton;
        public Sprite slime;
        public Sprite explosiveBarrel;
        public Sprite selection;

        public Sprite MonsterFor(string archetypeId)
        {
            switch (archetypeId)
            {
                case "Skeleton": return skeleton;
                case "Slime": return slime;
                default: return goblin;
            }
        }

        [Header("아이템")]
        public Sprite potion;
        public Sprite bomb;
        public Sprite frostBomb;

        public Sprite ItemFor(ItemKind kind) =>
            kind == ItemKind.Potion ? potion
            : kind == ItemKind.FrostBomb ? frostBomb
            : kind == ItemKind.Bomb ? bomb
            : null; // 미등록 종류는 런타임 임시 아트로 폴백

        public Sprite TileFor(TileKind kind, int elevation)
        {
            switch (kind)
            {
                case TileKind.Stairs: return stairs;
                case TileKind.StairsUp: return stairsUp != null ? stairsUp : stairs;
                case TileKind.StairsDown: return stairsDown != null ? stairsDown : stairs;
                case TileKind.Hole: return hole;
                case TileKind.WeakFloor: return weakFloor;
                case TileKind.DoorClosed: return doorClosed;
                case TileKind.DoorOpen: return doorOpen != null ? doorOpen : floor;
                default:
                    if (elevation < 0) return lowerFloor;
                    if (elevation > 0) return raisedFloor;
                    return floor;
            }
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

            return directed != null ? directed : TileFor(kind, 0);
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

            return directed != null ? directed : TileFor(kind, 0);
        }

        public Sprite RearWallFor(bool torch, bool risesRight)
        {
            Sprite directed = torch
                ? (risesRight ? rearWallTorchRisingRight : rearWallTorchRisingLeft)
                : null;
            if (directed != null) return directed;

            return risesRight ? rearWallRisingRight : rearWallRisingLeft;
        }
    }
}
