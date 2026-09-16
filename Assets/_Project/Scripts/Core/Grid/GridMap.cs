using System.Collections.Generic;
using UnityEngine;

namespace EmberDepths.Core.Grid
{
    public enum CellTerrain : byte
    {
        /// <summary>Outside the dungeon. Never rendered, never entered.</summary>
        Void = 0,
        Floor = 1,
        Wall = 2,
        /// <summary>Bottomless. Blocks walking but not line of sight or projectiles.</summary>
        Chasm = 3,
        /// <summary>Standing lava. Passable, but applies the biome's terrain hazard.</summary>
        Lava = 4,
        /// <summary>Passable while open; blocks while shut (used to gate encounters).</summary>
        Door = 5,
        /// <summary>Cover / decor that blocks movement but is chest-height.</summary>
        Rubble = 6
    }

    public struct CellData
    {
        public CellTerrain Terrain;
        public byte Elevation;
        public short RoomId;

        /// <summary>Extra movement cost in tenths of a step; lets AI prefer safe ground.</summary>
        public byte ExtraCost;

        public static CellData Void => new CellData { Terrain = CellTerrain.Void, RoomId = -1 };
    }

    /// <summary>
    /// The dungeon's static terrain plus a live occupancy index.
    ///
    /// Terrain and occupancy are kept apart on purpose: pathfinding asks about
    /// terrain (can this cell ever be walked?) while movement asks about occupancy
    /// (is someone standing there right now?). Melee AI needs to path *towards* an
    /// occupied cell and stop next to it, which only works if the two questions
    /// have separate answers.
    /// </summary>
    public sealed class GridMap
    {
        public readonly int Width;
        public readonly int Height;

        private readonly CellData[] _cells;
        private readonly Dictionary<GridCoord, int> _occupants = new Dictionary<GridCoord, int>(256);
        private readonly HashSet<GridCoord> _openDoors = new HashSet<GridCoord>();

        public GridMap(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            _cells = new CellData[Width * Height];
            for (int i = 0; i < _cells.Length; i++) _cells[i] = CellData.Void;
        }

        public bool InBounds(GridCoord c) => c.X >= 0 && c.Y >= 0 && c.X < Width && c.Y < Height;

        private int Index(GridCoord c) => c.Y * Width + c.X;

        public CellData Get(GridCoord c) => InBounds(c) ? _cells[Index(c)] : CellData.Void;

        public void Set(GridCoord c, CellData data)
        {
            if (InBounds(c)) _cells[Index(c)] = data;
        }

        public void SetTerrain(GridCoord c, CellTerrain terrain, short roomId = -1)
        {
            if (!InBounds(c)) return;
            int i = Index(c);
            _cells[i].Terrain = terrain;
            if (roomId >= 0) _cells[i].RoomId = roomId;
        }

        public CellTerrain TerrainAt(GridCoord c) => Get(c).Terrain;

        public short RoomAt(GridCoord c) => Get(c).RoomId;

        /// <summary>Can an actor ever stand here, ignoring who is standing there now?</summary>
        public bool IsWalkable(GridCoord c)
        {
            if (!InBounds(c)) return false;
            switch (_cells[Index(c)].Terrain)
            {
                case CellTerrain.Floor:
                case CellTerrain.Lava:
                    return true;
                case CellTerrain.Door:
                    return _openDoors.Contains(c);
                default:
                    return false;
            }
        }

        /// <summary>Walkable and nobody is standing there.</summary>
        public bool IsFree(GridCoord c) => IsWalkable(c) && !_occupants.ContainsKey(c);

        /// <summary>Blocks projectiles and line of sight. Chasms and lava do not.</summary>
        public bool BlocksSight(GridCoord c)
        {
            if (!InBounds(c)) return true;
            CellTerrain t = _cells[Index(c)].Terrain;
            if (t == CellTerrain.Door) return !_openDoors.Contains(c);
            return t == CellTerrain.Wall || t == CellTerrain.Void;
        }

        /// <summary>
        /// Dynamic surcharge on top of the terrain cost, in steps. The hazard
        /// system installs one of these so pathfinding routes around fire pools
        /// without the hazard layer having to write into terrain data.
        /// </summary>
        public System.Func<GridCoord, float> ExtraCostProvider { get; set; }

        /// <summary>Movement cost of entering a cell, in steps. 1.0 is a plain floor.</summary>
        public float CostToEnter(GridCoord c)
        {
            if (!InBounds(c)) return float.PositiveInfinity;
            CellData d = _cells[Index(c)];
            float cost = 1f + d.ExtraCost * 0.1f;
            // Lava is passable but AI and companions should route around it.
            if (d.Terrain == CellTerrain.Lava) cost += 8f;
            if (ExtraCostProvider != null) cost += ExtraCostProvider(c);
            return cost;
        }

        // --- doors --------------------------------------------------------------

        public void SetDoorOpen(GridCoord c, bool open)
        {
            if (open) _openDoors.Add(c);
            else _openDoors.Remove(c);
        }

        public bool IsDoorOpen(GridCoord c) => _openDoors.Contains(c);

        // --- occupancy ----------------------------------------------------------

        public bool TryOccupy(GridCoord c, int actorId)
        {
            if (!IsWalkable(c)) return false;
            if (_occupants.TryGetValue(c, out int existing)) return existing == actorId;
            _occupants[c] = actorId;
            return true;
        }

        public void Vacate(GridCoord c, int actorId)
        {
            if (_occupants.TryGetValue(c, out int existing) && existing == actorId)
                _occupants.Remove(c);
        }

        public bool TryGetOccupant(GridCoord c, out int actorId) => _occupants.TryGetValue(c, out actorId);

        public bool IsOccupied(GridCoord c) => _occupants.ContainsKey(c);

        public void ClearOccupancy() => _occupants.Clear();

        // --- queries used by spawning and AI -------------------------------------

        /// <summary>
        /// Nearest free cell to <paramref name="origin"/> within
        /// <paramref name="maxRadius"/> rings, searched outward. Returns false if the
        /// area is packed — callers must handle that rather than stacking actors.
        /// </summary>
        public bool TryFindFreeCellNear(GridCoord origin, int maxRadius, out GridCoord result)
        {
            if (IsFree(origin))
            {
                result = origin;
                return true;
            }

            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dy = -r; dy <= r; dy++)
                    {
                        // Only the ring, not the filled square.
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r) continue;
                        var c = new GridCoord(origin.X + dx, origin.Y + dy);
                        if (!IsFree(c)) continue;
                        result = c;
                        return true;
                    }
                }
            }

            result = origin;
            return false;
        }

        /// <summary>
        /// Bresenham-style line of sight. Walls and closed doors block; lava,
        /// chasms and other actors do not.
        /// </summary>
        public bool HasLineOfSight(GridCoord from, GridCoord to)
        {
            int dx = Mathf.Abs(to.X - from.X);
            int dy = Mathf.Abs(to.Y - from.Y);
            int sx = from.X < to.X ? 1 : -1;
            int sy = from.Y < to.Y ? 1 : -1;
            int err = dx - dy;
            int x = from.X, y = from.Y;

            // Bounded so a malformed map cannot spin forever.
            int guard = dx + dy + 2;
            while (guard-- > 0)
            {
                if (x == to.X && y == to.Y) return true;
                if (!(x == from.X && y == from.Y) && BlocksSight(new GridCoord(x, y))) return false;

                int e2 = err * 2;
                if (e2 > -dy) { err -= dy; x += sx; }
                if (e2 < dx) { err += dx; y += sy; }
            }

            return false;
        }
    }
}
