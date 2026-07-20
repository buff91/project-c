using NUnit.Framework;
using ProjectC.Core;

namespace ProjectC.Tests
{
    public class StairTopologyTests
    {
        [Test]
        public void TryGetHigherLanding_FindsAdjacentWalkableTileOneElevationUp()
        {
            var map = new GridMap();
            var stairs = new GridPos(3, 4, 0);
            var landing = new GridPos(3, 5, 1);
            map.Set(stairs, TileKind.Stairs);
            map.Set(landing, TileKind.Floor);

            Assert.That(StairTopology.TryGetHigherLanding(map, stairs, out GridPos result), Is.True);
            Assert.That(result, Is.EqualTo(landing));
        }

        [Test]
        public void TryGetHigherLanding_IgnoresSameElevationNeighbor()
        {
            var map = new GridMap();
            var stairs = new GridPos(3, 4, 0);
            map.Set(stairs, TileKind.Stairs);
            map.Set(stairs.North, TileKind.Floor);

            Assert.That(StairTopology.TryGetHigherLanding(map, stairs, out _), Is.False);
        }

        [Test]
        public void TryGetHigherLanding_RequiresStairTile()
        {
            var map = new GridMap();
            var floor = new GridPos(3, 4, 0);
            map.Set(floor, TileKind.Floor);
            map.Set(new GridPos(3, 5, 1), TileKind.Floor);

            Assert.That(StairTopology.TryGetHigherLanding(map, floor, out _), Is.False);
        }
    }
}
