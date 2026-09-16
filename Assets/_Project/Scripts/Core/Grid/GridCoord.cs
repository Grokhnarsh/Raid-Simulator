using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmberDepths.Core.Grid
{
    /// <summary>
    /// Integer cell address on the dungeon grid. The whole simulation addresses the
    /// world in these; world-space <see cref="Vector3"/> only ever appears at the
    /// rendering edge (see <see cref="IsoGrid"/>).
    /// </summary>
    [Serializable]
    public struct GridCoord : IEquatable<GridCoord>
    {
        public int X;
        public int Y;

        public GridCoord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static readonly GridCoord Zero = new GridCoord(0, 0);

        /// <summary>
        /// The four grid-orthogonal steps. On screen these read as the diagonals
        /// (NE, NW, SW, SE) because the grid is drawn isometrically.
        /// </summary>
        public static readonly GridCoord[] Orthogonal =
        {
            new GridCoord(1, 0), new GridCoord(0, 1), new GridCoord(-1, 0), new GridCoord(0, -1)
        };

        /// <summary>
        /// All eight steps, indexed by <see cref="IsoDirection"/>: the entry at
        /// <c>AllNeighbours[(int)IsoDirection.North]</c> is the step that moves an
        /// actor up the screen. Keep this in sync with the enum.
        /// </summary>
        public static readonly GridCoord[] AllNeighbours =
        {
            new GridCoord(-1, -1), // South
            new GridCoord(0, -1),  // SouthEast
            new GridCoord(1, -1),  // East
            new GridCoord(1, 0),   // NorthEast
            new GridCoord(1, 1),   // North
            new GridCoord(0, 1),   // NorthWest
            new GridCoord(-1, 1),  // West
            new GridCoord(-1, 0)   // SouthWest
        };

        public static GridCoord Step(IsoDirection dir) => AllNeighbours[(int)dir];

        public static GridCoord operator +(GridCoord a, GridCoord b) => new GridCoord(a.X + b.X, a.Y + b.Y);
        public static GridCoord operator -(GridCoord a, GridCoord b) => new GridCoord(a.X - b.X, a.Y - b.Y);
        public static GridCoord operator *(GridCoord a, int s) => new GridCoord(a.X * s, a.Y * s);
        public static bool operator ==(GridCoord a, GridCoord b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(GridCoord a, GridCoord b) => !(a == b);

        /// <summary>Steps needed with only grid-orthogonal moves.</summary>
        public static int Manhattan(GridCoord a, GridCoord b) => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

        /// <summary>
        /// Steps needed when diagonals are allowed. This is the game's notion of
        /// "range in tiles" — abilities and aggro radii all measure with it, so a
        /// diagonal neighbour counts as adjacent just like an orthogonal one.
        /// </summary>
        public static int Chebyshev(GridCoord a, GridCoord b) =>
            Mathf.Max(Mathf.Abs(a.X - b.X), Mathf.Abs(a.Y - b.Y));

        /// <summary>Euclidean distance in tiles, for smooth falloff curves.</summary>
        public static float Euclidean(GridCoord a, GridCoord b)
        {
            float dx = a.X - b.X;
            float dy = a.Y - b.Y;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        public bool IsAdjacentTo(GridCoord other) => other != this && Chebyshev(this, other) == 1;

        public IEnumerable<GridCoord> Neighbours()
        {
            for (int i = 0; i < AllNeighbours.Length; i++)
                yield return this + AllNeighbours[i];
        }

        public bool Equals(GridCoord other) => X == other.X && Y == other.Y;
        public override bool Equals(object obj) => obj is GridCoord other && Equals(other);

        public override int GetHashCode() => unchecked((X * 73856093) ^ (Y * 19349663));

        public override string ToString() => $"({X},{Y})";
    }
}
