using System.Collections.Generic;
using EmberDepths.Core.Grid;
using NUnit.Framework;

namespace EmberDepths.Tests
{
    public sealed class PathfindingTests
    {
        private static GridMap OpenRoom(int width, int height)
        {
            var map = new GridMap(width, height);
            for (int x = 1; x < width - 1; x++)
                for (int y = 1; y < height - 1; y++)
                    map.SetTerrain(new GridCoord(x, y), CellTerrain.Floor);
            return map;
        }

        [Test]
        public void FindsAStraightPathAcrossAnOpenRoom()
        {
            GridMap map = OpenRoom(16, 16);
            var path = new List<GridCoord>();

            bool found = new AStar().TryFindPath(map, new GridCoord(2, 2), new GridCoord(12, 12), path);

            Assert.IsTrue(found);
            Assert.AreEqual(new GridCoord(12, 12), path[path.Count - 1]);
            // Ten diagonal steps is optimal; allow no detour on empty ground.
            Assert.AreEqual(10, path.Count);
        }

        [Test]
        public void ExcludesTheStartAndEndsAtTheGoal()
        {
            GridMap map = OpenRoom(10, 10);
            var path = new List<GridCoord>();
            var start = new GridCoord(2, 2);

            new AStar().TryFindPath(map, start, new GridCoord(6, 4), path);

            CollectionAssert.DoesNotContain(path, start);
            Assert.AreEqual(new GridCoord(6, 4), path[path.Count - 1]);
        }

        [Test]
        public void RoutesAroundAWall()
        {
            GridMap map = OpenRoom(16, 16);
            for (int y = 1; y < 14; y++) map.SetTerrain(new GridCoord(8, y), CellTerrain.Wall);

            var path = new List<GridCoord>();
            bool found = new AStar().TryFindPath(map, new GridCoord(3, 3), new GridCoord(13, 3), path);

            Assert.IsTrue(found, "should go round the end of the wall");
            foreach (GridCoord step in path)
                Assert.AreNotEqual(CellTerrain.Wall, map.TerrainAt(step), "path walked through a wall");
        }

        [Test]
        public void DoesNotCutCorners()
        {
            // A diagonal step past the corner of a wall would visibly clip
            // through solid rock, so it must be refused.
            var map = new GridMap(8, 8);
            for (int x = 1; x < 7; x++)
                for (int y = 1; y < 7; y++)
                    map.SetTerrain(new GridCoord(x, y), CellTerrain.Floor);

            map.SetTerrain(new GridCoord(3, 2), CellTerrain.Wall);
            map.SetTerrain(new GridCoord(2, 3), CellTerrain.Wall);

            var path = new List<GridCoord>();
            var start = new GridCoord(2, 2);
            new AStar().TryFindPath(map, start, new GridCoord(3, 3), path);

            // Reaching (3,3) is fine — it is the goal, and there is a legal route
            // the long way round. What must not happen is stepping there directly
            // from (2,2), squeezing between the two wall corners.
            Assert.AreNotEqual(new GridCoord(3, 3), path[0],
                "took the illegal diagonal straight through the corner");

            AssertEveryStepIsLegal(map, start, path);
        }

        /// <summary>
        /// Walks a returned path and checks each step is one cell, onto walkable
        /// ground, and never diagonally through a corner.
        /// </summary>
        private static void AssertEveryStepIsLegal(GridMap map, GridCoord start, List<GridCoord> path)
        {
            GridCoord previous = start;

            foreach (GridCoord step in path)
            {
                Assert.AreEqual(1, GridCoord.Chebyshev(previous, step), $"{previous} -> {step} is not one step");
                Assert.IsTrue(map.IsWalkable(step), $"{step} is not walkable");

                bool diagonal = step.X != previous.X && step.Y != previous.Y;
                if (diagonal)
                {
                    Assert.IsTrue(map.IsWalkable(new GridCoord(step.X, previous.Y))
                                  && map.IsWalkable(new GridCoord(previous.X, step.Y)),
                        $"{previous} -> {step} cuts a wall corner");
                }

                previous = step;
            }
        }

        [Test]
        public void StopAdjacent_ReachesAnOccupiedGoal()
        {
            GridMap map = OpenRoom(12, 12);
            var goal = new GridCoord(8, 8);
            map.TryOccupy(goal, actorId: 42);

            var path = new List<GridCoord>();

            // Without stopAdjacent a melee attacker can never path to its target,
            // because the target's own cell is by definition taken.
            bool blocked = new AStar().TryFindPath(map, new GridCoord(2, 2), goal, path,
                stopAdjacent: false, occupancyBlocks: true);
            bool adjacent = new AStar().TryFindPath(map, new GridCoord(2, 2), goal, path,
                stopAdjacent: true, occupancyBlocks: true);

            Assert.IsTrue(blocked, "the goal cell itself is always allowed as a destination");
            Assert.IsTrue(adjacent);
            Assert.AreEqual(1, GridCoord.Chebyshev(goal, path[path.Count - 1]));
        }

        [Test]
        public void ReturnsFalseWhenSealedOff()
        {
            var map = new GridMap(10, 10);
            map.SetTerrain(new GridCoord(2, 2), CellTerrain.Floor);
            map.SetTerrain(new GridCoord(7, 7), CellTerrain.Floor);

            var path = new List<GridCoord>();
            Assert.IsFalse(new AStar().TryFindPath(map, new GridCoord(2, 2), new GridCoord(7, 7), path));
            Assert.IsEmpty(path);
        }

        [Test]
        public void ClosedDoorsBlockAndOpenDoorsDoNot()
        {
            var map = new GridMap(10, 5);
            for (int x = 1; x < 9; x++) map.SetTerrain(new GridCoord(x, 2), CellTerrain.Floor);

            var door = new GridCoord(5, 2);
            map.SetTerrain(door, CellTerrain.Door);

            var path = new List<GridCoord>();
            var astar = new AStar();

            map.SetDoorOpen(door, false);
            Assert.IsFalse(astar.TryFindPath(map, new GridCoord(2, 2), new GridCoord(8, 2), path),
                "a sealed room must actually seal");

            map.SetDoorOpen(door, true);
            Assert.IsTrue(astar.TryFindPath(map, new GridCoord(2, 2), new GridCoord(8, 2), path));
        }

        [Test]
        public void LineOfSight_IsBlockedByWallsButNotByLava()
        {
            GridMap map = OpenRoom(12, 12);
            Assert.IsTrue(map.HasLineOfSight(new GridCoord(2, 6), new GridCoord(9, 6)));

            map.SetTerrain(new GridCoord(5, 6), CellTerrain.Lava);
            Assert.IsTrue(map.HasLineOfSight(new GridCoord(2, 6), new GridCoord(9, 6)),
                "lava is passable and transparent");

            map.SetTerrain(new GridCoord(5, 6), CellTerrain.Wall);
            Assert.IsFalse(map.HasLineOfSight(new GridCoord(2, 6), new GridCoord(9, 6)));
        }
    }
}
