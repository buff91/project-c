using System;

namespace ProjectC.Core
{
    /// <summary>
    /// 연속 elevation을 던전 층(floor)과 층 내부 높이(local height)로 해석한다.
    /// 예: stride=4일 때 B1(floor 0)=e0..e3, B2(floor -1)=e-4..e-1.
    /// </summary>
    public sealed class DungeonHeightModel
    {
        public int ElevationsPerFloor { get; }

        public DungeonHeightModel(int elevationsPerFloor = 4)
        {
            if (elevationsPerFloor < 2)
                throw new ArgumentOutOfRangeException(nameof(elevationsPerFloor));
            ElevationsPerFloor = elevationsPerFloor;
        }

        public int FloorIndex(int elevation)
        {
            int quotient = elevation / ElevationsPerFloor;
            int remainder = elevation % ElevationsPerFloor;
            return remainder < 0 ? quotient - 1 : quotient;
        }

        public int LocalHeight(int elevation)
        {
            int floor = FloorIndex(elevation);
            return elevation - floor * ElevationsPerFloor;
        }

        public int Elevation(int floorIndex, int localHeight = 0)
        {
            if (localHeight < 0 || localHeight >= ElevationsPerFloor)
                throw new ArgumentOutOfRangeException(nameof(localHeight));
            return floorIndex * ElevationsPerFloor + localHeight;
        }

        public bool SameFloor(GridPos first, GridPos second) =>
            FloorIndex(first.elevation) == FloorIndex(second.elevation);
    }
}
