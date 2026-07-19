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

        [Header("액터와 소품")]
        public Sprite player;
        public Sprite goblin;
        public Sprite explosiveBarrel;
        public Sprite selection;

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
    }
}
