using System;
using System.Collections.Generic;

namespace ProjectC.Core
{
    public static partial class DungeonGenerator
    {

        /// <summary>층 하나의 골격 치수를 seed 로 뽑는다. 방 최소 폭/간격 제약은 범위로 보장한다.</summary>
        private static FloorPlan PlanFloor(
            Random random,
            int width,
            int height,
            int depth,
            int floorCount,
            DungeonHeightModel heightModel,
            FloorPlan previous,
            bool forceSecretBranch)
        {
            var p = new FloorPlan
            {
                Width = width,
                Height = height,
                FloorIndex = -depth
            };
            p.BaseElevation = heightModel.Elevation(p.FloorIndex);

            // 남쪽 두 방: 입구 방(남서)과 동쪽 방(남동). 사이에 1칸 이상 복도 공간을 남긴다.
            p.LeftMaxX = 3 + random.Next(0, Math.Max(1, width - 9));
            p.RightMinX = random.Next(p.LeftMaxX + 2, width - 2);
            p.LowerMaxY = 3 + random.Next(0, Math.Max(1, height - 8));

            // 북쪽 방. 동쪽 방(rows 0..LowerMaxY)과 행 간격을 두어 문을 우회하는 인접을 막는다.
            p.UpperMinY = random.Next(p.LowerMaxY + 2, height - 3);
            int upperMinCap = Math.Min(
                p.RightMinX - 2,
                previous != null ? previous.UpperMaxX - 1 : int.MaxValue);
            int upperMinFloor = forceSecretBranch ? 3 : 1;
            int upperMinCeiling = Math.Max(upperMinFloor, upperMinCap);
            p.UpperMinX = random.Next(upperMinFloor, upperMinCeiling + 1);
            int upperMaxFloor = Math.Max(
                Math.Max(p.UpperMinX + 3, p.RightMinX),
                previous != null ? previous.UpperMinX + 2 : 0);
            p.UpperMaxX = random.Next(upperMaxFloor, width - 1);

            p.RaisedY = height - 2;
            p.StairX = random.Next(p.UpperMinX, p.UpperMaxX + 1);
            p.LadderX = p.StairX != p.UpperMinX ? p.UpperMinX : p.UpperMaxX;
            p.HorizontalY = random.Next(1, p.LowerMaxY + 1);
            p.VerticalX = random.Next(p.RightMinX, Math.Min(p.UpperMaxX, width - 2) + 1);

            p.Doors.Add(new GridPos(p.LeftMaxX + 1, p.HorizontalY, p.BaseElevation));
            p.Doors.Add(new GridPos(p.VerticalX, p.LowerMaxY + 1, p.BaseElevation));

            // 확률적 막다른 분기 방: 북서쪽 빈 공간이 충분할 때만 문 하나로 매달린다.
            // 분기 확률은 깊이 밴드별로 다르다(깊을수록 파밍 방이 잦다).
            int branchChance = DungeonBandProfiles.ForDepth(depth).BranchChancePercent;
            bool wantBranch = forceSecretBranch || random.Next(0, 100) < branchChance;
            int branchDoorCap = Math.Min(p.LeftMaxX, p.UpperMinX - 2);
            if (wantBranch && p.UpperMinX >= 3 && branchDoorCap >= 0)
            {
                p.HasBranch = true;
                p.BranchIsSecret = forceSecretBranch;
                p.BranchDoorX = random.Next(0, branchDoorCap + 1);
                p.BranchMinX = Math.Max(0, p.BranchDoorX - 1);
                p.BranchMaxX = Math.Min(p.UpperMinX - 2, p.BranchMinX + 1 + random.Next(0, 2));
                p.BranchMinY = p.LowerMaxY + 2;
                p.BranchMaxY = Math.Min(height - 2, p.BranchMinY + 1 + random.Next(0, 2));
                var branchDoor = new GridPos(p.BranchDoorX, p.LowerMaxY + 1, p.BaseElevation);
                if (p.BranchIsSecret)
                    p.SecretDoor = branchDoor;
                else
                    p.Doors.Add(branchDoor);
            }

            // 층간 링크는 같은 x/y의 수직 샤프트를 공유한다.
            // 중간층에서는 좌·우 샤프트를 번갈아 써 Up/Down이 한 칸에 겹치지 않게 한다.
            int upX = (depth - 1) % 2 == 0 ? width - 2 : 1;
            int downX = depth % 2 == 0 ? width - 2 : 1;
            p.Up = depth == 0 ? (GridPos?)null : p.At(upX, 1);
            // 최심층에도 하행 계단을 둔다 — 링크가 없는 하행 계단이 "다음 던전 출구"다.
            p.Down = p.At(downX, 1);
            p.Entry = p.Up ?? p.At(1, 1);
            return p;
        }
    }
}
