using System;
using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using UnityEngine;

namespace EmberDepths.Gameplay.World
{
    /// <summary>
    /// Decides when a room's fight starts, spawns it, seals it, and notices when
    /// it is over.
    ///
    /// Rooms are populated lazily rather than all at once. A dungeon with every
    /// pack alive from the first frame costs a hundred brains ticking for nothing
    /// and, worse, lets a stray area effect pull three rooms at once.
    /// </summary>
    public sealed class EncounterDirector
    {
        private readonly DungeonWorld _world;
        private readonly DungeonDefinition _definition;
        private readonly Dictionary<int, List<Actor>> _roomEnemies = new Dictionary<int, List<Actor>>(16);

        private int _nextCheckTick;

        public event Action<Room> RoomActivated;
        public event Action<Room> RoomCleared;
        public event Action<Actor> BossEngaged;
        public event Action DungeonCleared;

        public int RoomsCleared { get; private set; }
        public int RoomsWithEncounters { get; private set; }
        public bool IsComplete { get; private set; }

        public EncounterDirector(DungeonWorld world, DungeonDefinition definition)
        {
            _world = world;
            _definition = definition;
            _world.ActorDied += OnActorDied;

            if (_world.Layout != null)
                for (int i = 0; i < _world.Layout.Rooms.Count; i++)
                    if (!_world.Layout.Rooms[i].Cleared) RoomsWithEncounters++;
        }

        public void Tick(int tick)
        {
            if (IsComplete || tick < _nextCheckTick) return;

            // Four times a second is plenty: a party cannot cross a room in less.
            _nextCheckTick = tick + SimClock.TicksPerSecond / 4;

            DungeonLayout layout = _world.Layout;
            if (layout == null) return;

            IReadOnlyList<Actor> party = _world.Actors.Party;

            for (int i = 0; i < layout.Rooms.Count; i++)
            {
                Room room = layout.Rooms[i];
                if (room.Activated || room.Cleared) continue;

                if (!AnyPartyInside(party, room)) continue;
                Activate(room);
            }
        }

        private static bool AnyPartyInside(IReadOnlyList<Actor> party, Room room)
        {
            for (int i = 0; i < party.Count; i++)
            {
                Actor member = party[i];
                if (member != null && member.IsAlive && room.Contains(member.Cell)) return true;
            }
            return false;
        }

        private void Activate(Room room)
        {
            room.Activated = true;

            var spawned = new List<Actor>(8);
            _roomEnemies[room.Id] = spawned;

            if (room.Kind == RoomKind.Boss) SpawnBoss(room, spawned);
            else SpawnEncounter(room, room.Encounter, spawned);

            if (spawned.Count == 0)
            {
                // Nothing actually spawned — an empty pool, or the room had no
                // encounter. Mark it done rather than leaving a permanent lock.
                room.Cleared = true;
                RoomCleared?.Invoke(room);
                CheckComplete();
                return;
            }

            if (room.Encounter != null && room.Encounter.LocksDoorsUntilCleared || room.Kind == RoomKind.Boss)
                SetDoors(room, open: false);

            RoomActivated?.Invoke(room);
        }

        private void SpawnEncounter(Room room, EncounterDefinition encounter, List<Actor> outSpawned)
        {
            if (encounter == null) return;

            DeterministicRandom rng = _world.Rng.Fork($"room{room.Id}");

            for (int i = 0; i < encounter.Slots.Count; i++)
            {
                EncounterSlot slot = encounter.Slots[i];
                if (slot.Enemy == null) continue;

                int count = slot.MaxCount > slot.MinCount
                    ? rng.Range(slot.MinCount, slot.MaxCount + 1)
                    : slot.MinCount;

                for (int n = 0; n < count; n++)
                {
                    GridCoord target = ScatterAround(room.Centre, encounter.SpawnRadius, rng);
                    if (!_world.Map.TryFindFreeCellNear(target, 4, out GridCoord cell)) continue;

                    Actor enemy = _world.SpawnEnemy(slot.Enemy, cell);
                    if (enemy != null) outSpawned.Add(enemy);
                }
            }

            for (int i = 0; i < encounter.Hazards.Count; i++)
            {
                PlacedHazard h = encounter.Hazards[i];
                if (h.Hazard == null) continue;

                var cell = new GridCoord(room.Centre.X + h.Offset.x, room.Centre.Y + h.Offset.y);
                _world.Hazards.Spawn(h.Hazard, cell, h.Radius, h.Duration, null);
            }
        }

        private void SpawnBoss(Room room, List<Actor> outSpawned)
        {
            EnemyDefinition bossDef = _definition.Biome != null ? _definition.Biome.Boss : null;
            if (bossDef == null)
            {
                Debug.LogWarning($"[EncounterDirector] Biome '{_definition.Biome?.name}' has no boss assigned.");
                return;
            }

            // The boss stands at the far end of the arena from the door, so the
            // party gets a moment to see it before the fight starts.
            GridCoord spot = room.Centre;
            if (room.Doorways.Count > 0)
            {
                GridCoord door = room.Doorways[0];
                var away = new GridCoord(
                    room.Centre.X + (room.Centre.X - door.X) / 2,
                    room.Centre.Y + (room.Centre.Y - door.Y) / 2);
                if (_world.Map.IsWalkable(away)) spot = away;
            }

            if (!_world.Map.TryFindFreeCellNear(spot, 6, out GridCoord cell)) cell = room.Centre;

            Actor boss = _world.SpawnEnemy(bossDef, cell);
            if (boss == null) return;

            outSpawned.Add(boss);
            BossEngaged?.Invoke(boss);
        }

        private GridCoord ScatterAround(GridCoord centre, int radius, DeterministicRandom rng)
        {
            Vector2 offset = rng.InsideUnitCircle() * radius;
            return new GridCoord(
                centre.X + Mathf.RoundToInt(offset.x),
                centre.Y + Mathf.RoundToInt(offset.y));
        }

        private void OnActorDied(Actor actor, Actor killer)
        {
            if (actor == null || actor.Faction != Faction.Hostile) return;

            foreach (KeyValuePair<int, List<Actor>> pair in _roomEnemies)
            {
                if (!pair.Value.Remove(actor)) continue;
                if (pair.Value.Count > 0) return;

                Room room = _world.Layout?.RoomById(pair.Key);
                if (room == null) return;

                room.Cleared = true;
                RoomsCleared++;
                SetDoors(room, open: true);
                RoomCleared?.Invoke(room);
                CheckComplete();
                return;
            }
        }

        private void CheckComplete()
        {
            if (IsComplete) return;

            Room boss = _world.Layout?.BossRoom;
            if (boss == null || !boss.Cleared) return;

            IsComplete = true;
            DungeonCleared?.Invoke();
        }

        private void SetDoors(Room room, bool open)
        {
            for (int i = 0; i < room.Doorways.Count; i++)
                _world.Map.SetDoorOpen(room.Doorways[i], open);
        }

        public void Dispose()
        {
            _world.ActorDied -= OnActorDied;
        }
    }
}
