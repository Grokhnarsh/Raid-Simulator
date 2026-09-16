using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace EmberDepths.Tests
{
    /// <summary>
    /// A generator only has to be interesting sometimes. It has to be *correct*
    /// every time — a seed that produces an unreachable boss room is a run the
    /// player cannot finish, and it will be found by a player rather than by us
    /// unless it is checked here across many seeds.
    /// </summary>
    public sealed class DungeonGeneratorTests
    {
        private const int SeedsToCheck = 60;

        private static DungeonDefinition MakeDefinition()
        {
            var def = ScriptableObject.CreateInstance<DungeonDefinition>();
            def.MapSize = new Vector2Int(84, 84);
            def.MinRooms = 8;
            def.MaxRooms = 10;
            def.RoomSizeMin = new Vector2Int(9, 9);
            def.RoomSizeMax = new Vector2Int(15, 15);
            def.RoomSpacing = 4;
            def.CorridorWidth = 2;
            def.ExtraConnectionRatio = 0.25f;
            def.EliteRooms = 2;
            def.HasTreasureRoom = true;
            def.DifficultyByDepth = AnimationCurve.Linear(0f, 1f, 1f, 2f);

            // A biome with real liquid coverage, so the lava placement rules are
            // actually exercised. Without one, PlaceLiquid never runs and the
            // doorway test below would pass vacuously.
            var biome = ScriptableObject.CreateInstance<BiomeDefinition>();
            biome.LiquidCoverage = 0.15f;
            def.Biome = biome;

            return def;
        }

        /// <summary>Every floor cell reachable on foot from the party's spawn.</summary>
        private static HashSet<GridCoord> FloodFill(GridMap map, GridCoord from)
        {
            var seen = new HashSet<GridCoord> { from };
            var queue = new Queue<GridCoord>();
            queue.Enqueue(from);

            while (queue.Count > 0)
            {
                GridCoord current = queue.Dequeue();
                for (int i = 0; i < GridCoord.AllNeighbours.Length; i++)
                {
                    GridCoord next = current + GridCoord.AllNeighbours[i];
                    if (!map.IsWalkable(next) || !seen.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }

            return seen;
        }

        [Test]
        public void EveryRoomIsReachableFromTheEntrance([NUnit.Framework.Range(1, SeedsToCheck)] int seed)
        {
            DungeonDefinition def = MakeDefinition();
            var map = new GridMap(def.MapSize.x, def.MapSize.y);
            DungeonLayout layout = new DungeonGenerator().Generate(def, map, seed);

            Assert.GreaterOrEqual(layout.Rooms.Count, def.MinRooms,
                $"seed {seed} placed only {layout.Rooms.Count} rooms");

            Assert.IsTrue(map.IsWalkable(layout.PartySpawn), $"seed {seed}: party spawn is not walkable");

            HashSet<GridCoord> reachable = FloodFill(map, layout.PartySpawn);

            foreach (Room room in layout.Rooms)
            {
                bool anyReachable = false;
                RectInt interior = room.Interior;

                for (int x = interior.xMin; x < interior.xMax && !anyReachable; x++)
                    for (int y = interior.yMin; y < interior.yMax && !anyReachable; y++)
                        anyReachable = reachable.Contains(new GridCoord(x, y));

                Assert.IsTrue(anyReachable, $"seed {seed}: room {room.Id} ({room.Kind}) is walled off");
            }
        }

        [Test]
        public void BossRoomExistsAndIsReachable([NUnit.Framework.Range(1, SeedsToCheck)] int seed)
        {
            DungeonDefinition def = MakeDefinition();
            var map = new GridMap(def.MapSize.x, def.MapSize.y);
            DungeonLayout layout = new DungeonGenerator().Generate(def, map, seed);

            Room boss = layout.BossRoom;
            Assert.IsNotNull(boss, $"seed {seed}: no boss room assigned");
            Assert.AreNotEqual(layout.EntranceRoomId, boss.Id, $"seed {seed}: boss placed in the entrance");

            HashSet<GridCoord> reachable = FloodFill(map, layout.PartySpawn);
            Assert.IsTrue(reachable.Contains(boss.Centre), $"seed {seed}: boss room centre unreachable");
        }

        [Test]
        public void GenerationIsDeterministic()
        {
            DungeonDefinition def = MakeDefinition();

            var mapA = new GridMap(def.MapSize.x, def.MapSize.y);
            var mapB = new GridMap(def.MapSize.x, def.MapSize.y);

            DungeonLayout a = new DungeonGenerator().Generate(def, mapA, 4242);
            DungeonLayout b = new DungeonGenerator().Generate(def, mapB, 4242);

            Assert.AreEqual(a.Rooms.Count, b.Rooms.Count);
            Assert.AreEqual(a.PartySpawn, b.PartySpawn);
            Assert.AreEqual(a.BossRoomId, b.BossRoomId);

            for (int x = 0; x < def.MapSize.x; x++)
                for (int y = 0; y < def.MapSize.y; y++)
                {
                    var c = new GridCoord(x, y);
                    Assert.AreEqual(mapA.TerrainAt(c), mapB.TerrainAt(c), $"terrain differs at {c}");
                }
        }

        [Test]
        public void DoorwaysConnectRoomsToCorridors([NUnit.Framework.Range(1, 20)] int seed)
        {
            DungeonDefinition def = MakeDefinition();
            var map = new GridMap(def.MapSize.x, def.MapSize.y);
            DungeonLayout layout = new DungeonGenerator().Generate(def, map, seed);

            foreach (Room room in layout.Rooms)
            {
                Assert.Greater(room.Doorways.Count, 0, $"seed {seed}: room {room.Id} has no way in");

                foreach (GridCoord door in room.Doorways)
                    Assert.AreEqual(CellTerrain.Door, map.TerrainAt(door),
                        $"seed {seed}: doorway {door} is not marked as a door");
            }
        }

        [Test]
        public void LavaNeverBlocksADoorway([NUnit.Framework.Range(1, 20)] int seed)
        {
            // Liquid across the only exit turns a tuning knob into a wall.
            DungeonDefinition def = MakeDefinition();
            var map = new GridMap(def.MapSize.x, def.MapSize.y);
            DungeonLayout layout = new DungeonGenerator().Generate(def, map, seed);

            foreach (Room room in layout.Rooms)
                foreach (GridCoord door in room.Doorways)
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = -1; dy <= 1; dy++)
                            Assert.AreNotEqual(CellTerrain.Lava,
                                map.TerrainAt(new GridCoord(door.X + dx, door.Y + dy)),
                                $"seed {seed}: lava next to doorway {door}");
        }
    }
}
