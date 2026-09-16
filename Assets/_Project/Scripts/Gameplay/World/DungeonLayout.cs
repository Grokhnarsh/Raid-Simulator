using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using UnityEngine;

namespace EmberDepths.Gameplay.World
{
    public enum RoomKind
    {
        Entrance = 0,
        Combat = 1,
        Elite = 2,
        Treasure = 3,
        Boss = 4,
        /// <summary>A junction with no encounter. Gives the party somewhere to regroup.</summary>
        Empty = 5
    }

    /// <summary>One rectangular room in the generated layout.</summary>
    public sealed class Room
    {
        public int Id;
        public RectInt Bounds;
        public RoomKind Kind = RoomKind.Combat;

        /// <summary>Graph distance from the entrance, normalised 0..1. Drives difficulty scaling.</summary>
        public float Depth;

        public EncounterDefinition Encounter;

        /// <summary>Cells that connect this room to a corridor. Doors are placed on these.</summary>
        public readonly List<GridCoord> Doorways = new List<GridCoord>(4);

        public readonly List<int> ConnectedRooms = new List<int>(4);

        /// <summary>Set once the room's pack is dead. Unlocks the doors.</summary>
        public bool Cleared;

        public bool Activated;

        public GridCoord Centre => new GridCoord(
            Bounds.x + Bounds.width / 2,
            Bounds.y + Bounds.height / 2);

        /// <summary>Interior cells, excluding the one-tile wall ring.</summary>
        public RectInt Interior => new RectInt(
            Bounds.x + 1, Bounds.y + 1,
            Mathf.Max(1, Bounds.width - 2), Mathf.Max(1, Bounds.height - 2));

        public bool Contains(GridCoord c) =>
            c.X >= Bounds.xMin && c.X < Bounds.xMax && c.Y >= Bounds.yMin && c.Y < Bounds.yMax;
    }

    /// <summary>
    /// The result of generation: rooms, how they connect, and where the party
    /// starts.
    ///
    /// Kept as plain data with no Unity objects in it so the generator can be run
    /// and asserted on in a test, or a thousand times in a row to check that a
    /// seed range never produces an unreachable boss room.
    /// </summary>
    public sealed class DungeonLayout
    {
        public int Seed;
        public readonly List<Room> Rooms = new List<Room>(16);

        public GridCoord PartySpawn;
        public int EntranceRoomId = -1;
        public int BossRoomId = -1;

        public Room RoomById(int id)
        {
            for (int i = 0; i < Rooms.Count; i++)
                if (Rooms[i].Id == id) return Rooms[i];
            return null;
        }

        public Room RoomContaining(GridCoord cell)
        {
            for (int i = 0; i < Rooms.Count; i++)
                if (Rooms[i].Contains(cell)) return Rooms[i];
            return null;
        }

        public Room Entrance => RoomById(EntranceRoomId);
        public Room BossRoom => RoomById(BossRoomId);
    }
}
