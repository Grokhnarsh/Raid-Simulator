using UnityEngine;

namespace EmberDepths.Core.Grid
{
    /// <summary>
    /// The single source of truth for the 2:1 isometric projection.
    ///
    /// The numbers here are chosen to line up exactly with a Unity <c>Grid</c>
    /// component configured as <c>Isometric</c> with cell size (2, 1, 1) at 32
    /// pixels per unit, so tilemap-placed tiles and script-placed actors land on
    /// identical world positions. Change <see cref="TileWidthPx"/> and friends here
    /// and re-run <c>EmberDepths/Setup/Configure Project</c> rather than editing the
    /// Grid component by hand.
    /// </summary>
    public static class IsoGrid
    {
        /// <summary>Width of a floor tile's top diamond, in source pixels.</summary>
        public const int TileWidthPx = 64;

        /// <summary>Height of a floor tile's top diamond, in source pixels. Always half the width.</summary>
        public const int TileHeightPx = 32;

        /// <summary>Import scale for every sprite in the game.</summary>
        public const int PixelsPerUnit = 32;

        /// <summary>One vertical "step" of elevation, in source pixels.</summary>
        public const int ElevationStepPx = 16;

        public const float CellSizeX = TileWidthPx / (float)PixelsPerUnit;   // 2.0
        public const float CellSizeY = TileHeightPx / (float)PixelsPerUnit;  // 1.0
        public const float HalfW = CellSizeX * 0.5f;                         // 1.0
        public const float HalfH = CellSizeY * 0.5f;                         // 0.5
        public const float ElevationStep = ElevationStepPx / (float)PixelsPerUnit; // 0.5

        public static readonly Vector3 UnityCellSize = new Vector3(CellSizeX, CellSizeY, 1f);

        /// <summary>
        /// Centre of a cell in world space. <paramref name="elevation"/> is in whole
        /// steps and only shifts the sprite up the screen — it deliberately does not
        /// affect sorting, so a raised platform still sorts by its floor cell.
        /// </summary>
        public static Vector3 CellToWorld(GridCoord cell, float elevation = 0f)
        {
            return new Vector3(
                (cell.X - cell.Y) * HalfW,
                (cell.X + cell.Y) * HalfH + elevation * ElevationStep,
                0f);
        }

        /// <summary>Continuous variant, for actors interpolating between cells.</summary>
        public static Vector3 CellToWorld(Vector2 fractionalCell, float elevation = 0f)
        {
            return new Vector3(
                (fractionalCell.x - fractionalCell.y) * HalfW,
                (fractionalCell.x + fractionalCell.y) * HalfH + elevation * ElevationStep,
                0f);
        }

        /// <summary>Inverse of <see cref="CellToWorld"/>, ignoring elevation.</summary>
        public static GridCoord WorldToCell(Vector3 world)
        {
            Vector2 f = WorldToFractionalCell(world);
            return new GridCoord(Mathf.RoundToInt(f.x), Mathf.RoundToInt(f.y));
        }

        public static Vector2 WorldToFractionalCell(Vector3 world)
        {
            float sum = world.y / HalfH;   // x + y
            float diff = world.x / HalfW;  // x - y
            return new Vector2((sum + diff) * 0.5f, (sum - diff) * 0.5f);
        }

        /// <summary>
        /// Depth key for painter's-algorithm sorting. Larger = further from the
        /// camera = drawn earlier. Cameras in this project use a custom transparency
        /// sort axis of (0,1,0), which sorts on exactly this value, so you normally
        /// don't need to call this — it exists for renderers that must be ordered
        /// explicitly (hazard decals, world-space UI).
        /// </summary>
        public static float DepthKey(GridCoord cell) => (cell.X + cell.Y) * HalfH;

        /// <summary>
        /// Sorting order for a renderer that has to be placed manually. Negated and
        /// scaled so that "closer to the camera" yields a larger sorting order.
        /// </summary>
        public static int SortingOrder(Vector3 world, int bias = 0) =>
            Mathf.RoundToInt(-world.y * 16f) + bias;

        /// <summary>
        /// The four corners of a cell's top diamond, clockwise from the top corner.
        /// Used by telegraph rendering and the editor gizmos.
        /// </summary>
        public static void GetCellCorners(GridCoord cell, Vector3[] outCorners, float elevation = 0f)
        {
            Vector3 c = CellToWorld(cell, elevation);
            outCorners[0] = c + new Vector3(0f, HalfH, 0f);
            outCorners[1] = c + new Vector3(HalfW, 0f, 0f);
            outCorners[2] = c + new Vector3(0f, -HalfH, 0f);
            outCorners[3] = c + new Vector3(-HalfW, 0f, 0f);
        }
    }
}
