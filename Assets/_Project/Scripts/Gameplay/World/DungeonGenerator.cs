using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using UnityEngine;

namespace EmberDepths.Gameplay.World
{
    /// <summary>
    /// Builds a dungeon layout from a <see cref="DungeonDefinition"/> and a seed.
    ///
    /// The shape is deliberately conventional — scattered rooms, a spanning tree
    /// of corridors, a few extra edges for loops — because the interesting
    /// variation in this game comes from encounters and hazards, not from exotic
    /// floor plans. What matters here is that it is *reliable*: same seed, same
    /// dungeon, always connected, boss always reachable.
    ///
    /// Nothing in this class touches Unity scene objects. It fills a
    /// <see cref="GridMap"/> and returns a <see cref="DungeonLayout"/>;
    /// <see cref="DungeonBuilder"/> is what turns that into tiles you can see.
    /// </summary>
    public sealed class DungeonGenerator
    {
        private readonly List<Room> _rooms = new List<Room>(24);
        private readonly List<GridCoord> _scratch = new List<GridCoord>(64);

        public DungeonLayout Generate(DungeonDefinition definition, GridMap map, int seed)
        {
            var rng = new DeterministicRandom(seed).Fork("layout");
            var layout = new DungeonLayout { Seed = seed };

            _rooms.Clear();

            PlaceRooms(definition, map, rng);
            if (_rooms.Count == 0)
            {
                Debug.LogError("[DungeonGenerator] Placed no rooms. Check MapSize against RoomSizeMax.");
                return layout;
            }

            ConnectRooms(definition, rng);
            CarveRooms(map);
            CarveCorridors(definition, map, rng);
            BuildWalls(map);
            FindDoorways(map);

            layout.Rooms.AddRange(_rooms);
            AssignDepths(layout);
            AssignRoomKinds(definition, layout, rng);
            PlaceLiquid(definition, map, layout, rng);
            AssignEncounters(definition, layout, rng);

            Room entrance = layout.Entrance;
            layout.PartySpawn = entrance != null ? entrance.Centre : _rooms[0].Centre;

            return layout;
        }

        // --- room placement ------------------------------------------------------

        private void PlaceRooms(DungeonDefinition def, GridMap map, DeterministicRandom rng)
        {
            int target = rng.Range(def.MinRooms, def.MaxRooms + 1);
            int attempts = target * 40;
            int margin = 2;

            while (_rooms.Count < target && attempts-- > 0)
            {
                int w = rng.Range(def.RoomSizeMin.x, def.RoomSizeMax.x + 1);
                int h = rng.Range(def.RoomSizeMin.y, def.RoomSizeMax.y + 1);

                int x = rng.Range(margin, Mathf.Max(margin + 1, map.Width - w - margin));
                int y = rng.Range(margin, Mathf.Max(margin + 1, map.Height - h - margin));

                var bounds = new RectInt(x, y, w, h);
                if (Overlaps(bounds, def.RoomSpacing)) continue;

                _rooms.Add(new Room { Id = _rooms.Count, Bounds = bounds });
            }
        }

        private bool Overlaps(RectInt candidate, int spacing)
        {
            var padded = new RectInt(
                candidate.x - spacing, candidate.y - spacing,
                candidate.width + spacing * 2, candidate.height + spacing * 2);

            for (int i = 0; i < _rooms.Count; i++)
                if (padded.Overlaps(_rooms[i].Bounds)) return true;

            return false;
        }

        // --- graph ---------------------------------------------------------------

        /// <summary>
        /// Minimum spanning tree over room centres, then a few extra edges.
        ///
        /// The tree guarantees every room is reachable — that is the property that
        /// must never fail. The extra edges create loops, which matter for play:
        /// without them every room is a dead end and retreating from a fight is
        /// impossible.
        /// </summary>
        private void ConnectRooms(DungeonDefinition def, DeterministicRandom rng)
        {
            if (_rooms.Count < 2) return;

            var inTree = new bool[_rooms.Count];
            inTree[0] = true;
            int connected = 1;

            while (connected < _rooms.Count)
            {
                int bestA = -1, bestB = -1;
                float bestDistance = float.MaxValue;

                for (int a = 0; a < _rooms.Count; a++)
                {
                    if (!inTree[a]) continue;
                    for (int b = 0; b < _rooms.Count; b++)
                    {
                        if (inTree[b]) continue;
                        float d = GridCoord.Euclidean(_rooms[a].Centre, _rooms[b].Centre);
                        if (d >= bestDistance) continue;
                        bestDistance = d;
                        bestA = a;
                        bestB = b;
                    }
                }

                if (bestA < 0) break;

                Link(_rooms[bestA], _rooms[bestB]);
                inTree[bestB] = true;
                connected++;
            }

            int extra = Mathf.RoundToInt(_rooms.Count * def.ExtraConnectionRatio);
            for (int i = 0; i < extra; i++)
            {
                Room a = _rooms[rng.Range(0, _rooms.Count)];
                Room b = _rooms[rng.Range(0, _rooms.Count)];
                if (a == b || a.ConnectedRooms.Contains(b.Id)) continue;
                Link(a, b);
            }
        }

        private static void Link(Room a, Room b)
        {
            if (!a.ConnectedRooms.Contains(b.Id)) a.ConnectedRooms.Add(b.Id);
            if (!b.ConnectedRooms.Contains(a.Id)) b.ConnectedRooms.Add(a.Id);
        }

        // --- carving --------------------------------------------------------------

        private void CarveRooms(GridMap map)
        {
            for (int i = 0; i < _rooms.Count; i++)
            {
                Room room = _rooms[i];
                RectInt interior = room.Interior;

                for (int x = interior.xMin; x < interior.xMax; x++)
                    for (int y = interior.yMin; y < interior.yMax; y++)
                        map.SetTerrain(new GridCoord(x, y), CellTerrain.Floor, (short)room.Id);
            }
        }

        private void CarveCorridors(DungeonDefinition def, GridMap map, DeterministicRandom rng)
        {
            var done = new HashSet<long>();

            for (int i = 0; i < _rooms.Count; i++)
            {
                Room a = _rooms[i];
                for (int j = 0; j < a.ConnectedRooms.Count; j++)
                {
                    Room b = RoomById(a.ConnectedRooms[j]);
                    if (b == null) continue;

                    long key = a.Id < b.Id ? ((long)a.Id << 32) | (uint)b.Id : ((long)b.Id << 32) | (uint)a.Id;
                    if (!done.Add(key)) continue;

                    CarveLCorridor(map, a.Centre, b.Centre, def.CorridorWidth, rng.Chance(0.5f));
                }
            }
        }

        private static void CarveLCorridor(GridMap map, GridCoord from, GridCoord to, int width, bool horizontalFirst)
        {
            GridCoord elbow = horizontalFirst
                ? new GridCoord(to.X, from.Y)
                : new GridCoord(from.X, to.Y);

            CarveStraight(map, from, elbow, width);
            CarveStraight(map, elbow, to, width);
        }

        private static void CarveStraight(GridMap map, GridCoord a, GridCoord b, int width)
        {
            int half = Mathf.Max(0, width - 1) / 2;

            if (a.X == b.X)
            {
                int step = b.Y >= a.Y ? 1 : -1;
                for (int y = a.Y; y != b.Y + step; y += step)
                    for (int w = -half; w <= half; w++)
                        CarveCell(map, new GridCoord(a.X + w, y));
            }
            else
            {
                int step = b.X >= a.X ? 1 : -1;
                for (int x = a.X; x != b.X + step; x += step)
                    for (int w = -half; w <= half; w++)
                        CarveCell(map, new GridCoord(x, a.Y + w));
            }
        }

        private static void CarveCell(GridMap map, GridCoord c)
        {
            if (!map.InBounds(c)) return;
            // Corridors never overwrite room floor, which would clear the room id
            // that encounter and door logic depends on.
            if (map.TerrainAt(c) == CellTerrain.Floor) return;
            map.SetTerrain(c, CellTerrain.Floor);
        }

        /// <summary>Every void cell touching floor becomes wall. Gives clean silhouettes for free.</summary>
        private static void BuildWalls(GridMap map)
        {
            for (int x = 0; x < map.Width; x++)
            {
                for (int y = 0; y < map.Height; y++)
                {
                    var c = new GridCoord(x, y);
                    if (map.TerrainAt(c) != CellTerrain.Void) continue;

                    bool touchesFloor = false;
                    for (int i = 0; i < GridCoord.AllNeighbours.Length && !touchesFloor; i++)
                        touchesFloor = map.TerrainAt(c + GridCoord.AllNeighbours[i]) == CellTerrain.Floor;

                    if (touchesFloor) map.SetTerrain(c, CellTerrain.Wall);
                }
            }
        }

        /// <summary>
        /// A doorway is a cell on a room's boundary ring that a corridor punched
        /// through. Marking them lets the encounter director seal a room.
        /// </summary>
        private void FindDoorways(GridMap map)
        {
            for (int i = 0; i < _rooms.Count; i++)
            {
                Room room = _rooms[i];
                RectInt b = room.Bounds;

                for (int x = b.xMin; x < b.xMax; x++)
                {
                    TryDoorway(map, room, new GridCoord(x, b.yMin));
                    TryDoorway(map, room, new GridCoord(x, b.yMax - 1));
                }

                for (int y = b.yMin; y < b.yMax; y++)
                {
                    TryDoorway(map, room, new GridCoord(b.xMin, y));
                    TryDoorway(map, room, new GridCoord(b.xMax - 1, y));
                }
            }
        }

        private static void TryDoorway(GridMap map, Room room, GridCoord c)
        {
            if (map.TerrainAt(c) != CellTerrain.Floor) return;
            if (room.Doorways.Contains(c)) return;

            room.Doorways.Add(c);
            map.SetTerrain(c, CellTerrain.Door, (short)room.Id);
            map.SetDoorOpen(c, true);
        }

        // --- classification -----------------------------------------------------------

        /// <summary>Breadth-first from the entrance so "deeper" means "further along the graph".</summary>
        private static void AssignDepths(DungeonLayout layout)
        {
            if (layout.Rooms.Count == 0) return;

            // The entrance is the room nearest the map's lower-left, which keeps
            // the run starting from a consistent corner rather than the middle.
            Room entrance = layout.Rooms[0];
            for (int i = 1; i < layout.Rooms.Count; i++)
            {
                Room r = layout.Rooms[i];
                if (r.Bounds.x + r.Bounds.y < entrance.Bounds.x + entrance.Bounds.y) entrance = r;
            }

            layout.EntranceRoomId = entrance.Id;

            var distance = new Dictionary<int, int> { [entrance.Id] = 0 };
            var queue = new Queue<Room>();
            queue.Enqueue(entrance);
            int maxDistance = 0;

            while (queue.Count > 0)
            {
                Room current = queue.Dequeue();
                int d = distance[current.Id];
                maxDistance = Mathf.Max(maxDistance, d);

                for (int i = 0; i < current.ConnectedRooms.Count; i++)
                {
                    int nextId = current.ConnectedRooms[i];
                    if (distance.ContainsKey(nextId)) continue;

                    Room next = layout.RoomById(nextId);
                    if (next == null) continue;

                    distance[nextId] = d + 1;
                    queue.Enqueue(next);
                }
            }

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                Room r = layout.Rooms[i];
                int d = distance.TryGetValue(r.Id, out int v) ? v : maxDistance;
                r.Depth = maxDistance > 0 ? d / (float)maxDistance : 0f;
            }
        }

        private void AssignRoomKinds(DungeonDefinition def, DungeonLayout layout, DeterministicRandom rng)
        {
            Room entrance = layout.Entrance;
            if (entrance != null)
            {
                entrance.Kind = RoomKind.Entrance;
                entrance.Cleared = true;
            }

            // The boss goes in the deepest room, and ties break on floor area so
            // the fight gets the biggest arena available.
            Room boss = null;
            foreach (Room r in layout.Rooms)
            {
                if (r.Kind == RoomKind.Entrance) continue;
                if (boss == null ||
                    r.Depth > boss.Depth ||
                    (Mathf.Approximately(r.Depth, boss.Depth) &&
                     r.Bounds.width * r.Bounds.height > boss.Bounds.width * boss.Bounds.height))
                {
                    boss = r;
                }
            }

            if (boss != null)
            {
                boss.Kind = RoomKind.Boss;
                layout.BossRoomId = boss.Id;
            }

            var candidates = new List<Room>();
            foreach (Room r in layout.Rooms)
                if (r.Kind == RoomKind.Combat) candidates.Add(r);

            rng.Shuffle(candidates);

            int elites = Mathf.Min(def.EliteRooms, candidates.Count);
            for (int i = 0; i < elites; i++) candidates[i].Kind = RoomKind.Elite;

            if (def.HasTreasureRoom && candidates.Count > elites)
                candidates[elites].Kind = RoomKind.Treasure;
        }

        // --- dressing --------------------------------------------------------------------

        /// <summary>
        /// Scatters lava blobs across room floors.
        ///
        /// Doorways and the cells beside them are excluded deliberately: a pool
        /// across the only exit turns a tuning knob into a wall.
        /// </summary>
        private void PlaceLiquid(DungeonDefinition def, GridMap map, DungeonLayout layout, DeterministicRandom rng)
        {
            BiomeDefinition biome = def.Biome;
            if (biome == null || biome.LiquidCoverage <= 0f) return;

            DeterministicRandom liquidRng = rng.Fork("liquid");

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                Room room = layout.Rooms[i];
                if (room.Kind == RoomKind.Entrance || room.Kind == RoomKind.Treasure) continue;

                RectInt interior = room.Interior;
                int area = interior.width * interior.height;
                int budget = Mathf.RoundToInt(area * biome.LiquidCoverage);
                if (budget <= 0) continue;

                int placed = 0;
                int attempts = budget * 8;

                while (placed < budget && attempts-- > 0)
                {
                    var centre = new GridCoord(
                        liquidRng.Range(interior.xMin, interior.xMax),
                        liquidRng.Range(interior.yMin, interior.yMax));

                    int radius = liquidRng.Range(0, 2);

                    for (int dx = -radius; dx <= radius && placed < budget; dx++)
                    {
                        for (int dy = -radius; dy <= radius && placed < budget; dy++)
                        {
                            var c = new GridCoord(centre.X + dx, centre.Y + dy);
                            if (map.TerrainAt(c) != CellTerrain.Floor) continue;
                            if (IsNearDoorway(room, c)) continue;

                            map.SetTerrain(c, CellTerrain.Lava, (short)room.Id);
                            placed++;
                        }
                    }
                }
            }
        }

        private static bool IsNearDoorway(Room room, GridCoord c)
        {
            for (int i = 0; i < room.Doorways.Count; i++)
                if (GridCoord.Chebyshev(room.Doorways[i], c) <= 2) return true;
            return false;
        }

        private void AssignEncounters(DungeonDefinition def, DungeonLayout layout, DeterministicRandom rng)
        {
            BiomeDefinition biome = def.Biome;
            if (biome == null) return;

            DeterministicRandom encounterRng = rng.Fork("encounters");

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                Room room = layout.Rooms[i];

                switch (room.Kind)
                {
                    case RoomKind.Combat:
                        room.Encounter = PickByBudget(biome.TrashEncounters, def, room, encounterRng);
                        break;

                    case RoomKind.Elite:
                        room.Encounter = PickByBudget(biome.EliteEncounters, def, room, encounterRng)
                                      ?? PickByBudget(biome.TrashEncounters, def, room, encounterRng);
                        break;

                    default:
                        room.Cleared = true;
                        break;
                }

                if (room.Encounter == null && room.Kind != RoomKind.Boss) room.Cleared = true;
            }
        }

        /// <summary>
        /// Picks the encounter whose threat sits closest to this room's budget,
        /// with a random tiebreak so repeat rooms are not identical.
        /// </summary>
        private static EncounterDefinition PickByBudget(
            List<EncounterDefinition> pool, DungeonDefinition def, Room room, DeterministicRandom rng)
        {
            if (pool == null || pool.Count == 0) return null;

            float budget = def.BaseRoomBudget * def.DifficultyByDepth.Evaluate(room.Depth);

            var weights = new List<float>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == null) { weights.Add(0f); continue; }
                float distance = Mathf.Abs(pool[i].Threat - budget);
                weights.Add(1f / (1f + distance));
            }

            int index = rng.PickWeighted(weights);
            return index >= 0 ? pool[index] : null;
        }

        private Room RoomById(int id)
        {
            for (int i = 0; i < _rooms.Count; i++)
                if (_rooms[i].Id == id) return _rooms[i];
            return null;
        }
    }
}
