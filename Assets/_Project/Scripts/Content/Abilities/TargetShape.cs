using System;
using System.Collections.Generic;
using EmberDepths.Core.Grid;
using UnityEngine;

namespace EmberDepths.Content
{
    public enum ShapeKind
    {
        /// <summary>Just the target cell.</summary>
        Single = 0,
        /// <summary>All cells within Radius of the target cell (Chebyshev, so it reads as a diamond on screen).</summary>
        Circle = 1,
        /// <summary>Wedge of Angle degrees opening from the origin towards the target.</summary>
        Cone = 2,
        /// <summary>Straight line of Length cells from origin towards the target, Width cells thick.</summary>
        Line = 3,
        /// <summary>Ring between InnerRadius and Radius. The classic "get out of the middle" mechanic, inverted.</summary>
        Donut = 4,
        /// <summary>Everything on the map. Used by enrage and phase-transition wipes.</summary>
        Global = 5
    }

    /// <summary>
    /// Geometry of an area effect, in grid cells.
    ///
    /// Distances use Chebyshev, matching <see cref="GridCoord.Chebyshev"/>. On an
    /// isometric grid that produces a square in grid space, which the projection
    /// draws as a diamond — the shape players actually see and the reason a
    /// "radius 3 circle" looks correct rather than lopsided.
    /// </summary>
    [Serializable]
    public struct TargetShape
    {
        public ShapeKind Kind;

        [Min(0)]
        [Tooltip("Outer radius in tiles. Ignored by Line and Global.")]
        public int Radius;

        [Min(0)]
        [Tooltip("Donut only: cells closer than this are safe.")]
        public int InnerRadius;

        [Min(1)]
        [Tooltip("Line only: how far the line reaches.")]
        public int Length;

        [Min(1)]
        [Tooltip("Line only: thickness in tiles.")]
        public int Width;

        [Range(10f, 360f)]
        [Tooltip("Cone only: total opening angle in degrees.")]
        public float Angle;

        public static TargetShape Single => new TargetShape { Kind = ShapeKind.Single, Length = 1, Width = 1, Angle = 90f };

        public static TargetShape Circle(int radius) =>
            new TargetShape { Kind = ShapeKind.Circle, Radius = radius, Length = 1, Width = 1, Angle = 90f };

        public static TargetShape Cone(int radius, float angle) =>
            new TargetShape { Kind = ShapeKind.Cone, Radius = radius, Length = 1, Width = 1, Angle = angle };

        public static TargetShape Line(int length, int width) =>
            new TargetShape { Kind = ShapeKind.Line, Length = length, Width = Mathf.Max(1, width), Angle = 90f };

        public static TargetShape Donut(int inner, int outer) =>
            new TargetShape { Kind = ShapeKind.Donut, InnerRadius = inner, Radius = outer, Length = 1, Width = 1, Angle = 90f };

        /// <summary>Rough extent in tiles, for cheap broad-phase rejection.</summary>
        public int MaxExtent => Kind switch
        {
            ShapeKind.Single => 0,
            ShapeKind.Line => Length,
            ShapeKind.Global => int.MaxValue,
            _ => Radius
        };

        /// <summary>
        /// Fills <paramref name="outCells"/> with every cell the shape covers.
        /// Pure geometry — walls, line of sight and map bounds are applied by the
        /// caller, because a telegraph should still draw over a wall it clips.
        /// </summary>
        public void CollectCells(GridCoord origin, GridCoord target, List<GridCoord> outCells)
        {
            outCells.Clear();

            switch (Kind)
            {
                case ShapeKind.Single:
                    outCells.Add(target);
                    break;

                case ShapeKind.Circle:
                    CollectDisc(target, 0, Mathf.Max(0, Radius), outCells);
                    break;

                case ShapeKind.Donut:
                    CollectDisc(target, Mathf.Max(0, InnerRadius) + 1, Mathf.Max(0, Radius), outCells);
                    break;

                case ShapeKind.Cone:
                    CollectCone(origin, target, outCells);
                    break;

                case ShapeKind.Line:
                    CollectLine(origin, target, outCells);
                    break;

                case ShapeKind.Global:
                    // Deliberately empty: the resolver substitutes "every actor" for
                    // Global rather than materialising the whole map as cells.
                    break;
            }
        }

        private static void CollectDisc(GridCoord centre, int minR, int maxR, List<GridCoord> outCells)
        {
            for (int dx = -maxR; dx <= maxR; dx++)
            {
                for (int dy = -maxR; dy <= maxR; dy++)
                {
                    int d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                    if (d < minR || d > maxR) continue;
                    outCells.Add(new GridCoord(centre.X + dx, centre.Y + dy));
                }
            }
        }

        private void CollectCone(GridCoord origin, GridCoord target, List<GridCoord> outCells)
        {
            // Work in world space: the cone must look like a cone on screen, and grid
            // space is sheared relative to what the player sees.
            Vector3 originW = IsoGrid.CellToWorld(origin);
            Vector3 targetW = IsoGrid.CellToWorld(target);
            Vector2 facing = new Vector2(targetW.x - originW.x, targetW.y - originW.y);
            if (facing.sqrMagnitude < 1e-6f) facing = Vector2.down;
            facing.Normalize();

            float halfAngle = Mathf.Clamp(Angle, 10f, 360f) * 0.5f;
            int r = Mathf.Max(1, Radius);

            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var cell = new GridCoord(origin.X + dx, origin.Y + dy);
                    if (GridCoord.Chebyshev(origin, cell) > r) continue;

                    Vector3 cellW = IsoGrid.CellToWorld(cell);
                    Vector2 to = new Vector2(cellW.x - originW.x, cellW.y - originW.y);
                    if (to.sqrMagnitude < 1e-6f) continue;

                    if (Vector2.Angle(facing, to.normalized) <= halfAngle)
                        outCells.Add(cell);
                }
            }
        }

        private void CollectLine(GridCoord origin, GridCoord target, List<GridCoord> outCells)
        {
            GridCoord delta = target - origin;
            if (delta.X == 0 && delta.Y == 0) delta = new GridCoord(1, 0);

            // Snap to one of the eight grid directions so the line lands on whole cells.
            IsoDirection dir = IsoDirectionExtensions.FromGridStep(origin, target);
            GridCoord step = GridCoord.Step(dir);

            // Perpendicular step for thickness, rotated 90 degrees on the grid.
            var perp = new GridCoord(-step.Y, step.X);
            int halfWidth = Mathf.Max(1, Width) / 2;

            for (int i = 1; i <= Mathf.Max(1, Length); i++)
            {
                GridCoord spine = origin + step * i;
                for (int w = -halfWidth; w <= halfWidth; w++)
                {
                    if (Width % 2 == 0 && w == halfWidth) continue; // even widths sit off-centre
                    outCells.Add(spine + perp * w);
                }
            }
        }
    }
}
