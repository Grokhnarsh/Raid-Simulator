using UnityEngine;

namespace EmberDepths.Core.Grid
{
    /// <summary>
    /// The eight facings a sprite can be drawn in. The order and the names are
    /// deliberately identical to PixelLab's rotation names (south, south-east, ...)
    /// so the importer can map generated sprites onto this enum by string without a
    /// lookup table. Do not reorder.
    /// </summary>
    public enum IsoDirection
    {
        South = 0,
        SouthEast = 1,
        East = 2,
        NorthEast = 3,
        North = 4,
        NorthWest = 5,
        West = 6,
        SouthWest = 7
    }

    public static class IsoDirectionExtensions
    {
        /// <summary>Lower-case hyphenated name, matching the generator's file naming.</summary>
        public static string ToAssetKey(this IsoDirection dir) => dir switch
        {
            IsoDirection.South => "south",
            IsoDirection.SouthEast => "south-east",
            IsoDirection.East => "east",
            IsoDirection.NorthEast => "north-east",
            IsoDirection.North => "north",
            IsoDirection.NorthWest => "north-west",
            IsoDirection.West => "west",
            IsoDirection.SouthWest => "south-west",
            _ => "south"
        };

        public static bool TryParseAssetKey(string key, out IsoDirection dir)
        {
            switch (key.Trim().ToLowerInvariant().Replace('_', '-'))
            {
                case "south": dir = IsoDirection.South; return true;
                case "south-east": case "southeast": dir = IsoDirection.SouthEast; return true;
                case "east": dir = IsoDirection.East; return true;
                case "north-east": case "northeast": dir = IsoDirection.NorthEast; return true;
                case "north": dir = IsoDirection.North; return true;
                case "north-west": case "northwest": dir = IsoDirection.NorthWest; return true;
                case "west": dir = IsoDirection.West; return true;
                case "south-west": case "southwest": dir = IsoDirection.SouthWest; return true;
                default: dir = IsoDirection.South; return false;
            }
        }

        /// <summary>
        /// Screen-space facing for a world-space movement vector. "South" is straight
        /// down the screen, which is what the artwork assumes — so this deliberately
        /// works in world space, not grid space.
        /// </summary>
        public static IsoDirection FromWorldVector(Vector2 delta, IsoDirection fallback = IsoDirection.South)
        {
            if (delta.sqrMagnitude < 1e-6f) return fallback;

            // atan2 gives -180..180 with 0 = +X (east). Rotate so South (-Y) is bucket 0.
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg; // east = 0, north = 90
            float fromSouth = Mathf.Repeat(angle + 90f, 360f);           // south = 0, east = 90
            int bucket = Mathf.RoundToInt(fromSouth / 45f) % 8;

            // Buckets run south -> east -> north -> west, i.e. counter-clockwise on
            // screen, which matches the enum order above.
            return (IsoDirection)bucket;
        }

        /// <summary>Facing implied by a step across the grid.</summary>
        public static IsoDirection FromGridStep(GridCoord from, GridCoord to, IsoDirection fallback = IsoDirection.South)
        {
            Vector3 a = IsoGrid.CellToWorld(from);
            Vector3 b = IsoGrid.CellToWorld(to);
            return FromWorldVector(new Vector2(b.x - a.x, b.y - a.y), fallback);
        }

        /// <summary>Unit vector in world space pointing along this facing.</summary>
        public static Vector2 ToWorldVector(this IsoDirection dir)
        {
            float deg = (int)dir * 45f - 90f; // reverse of FromWorldVector
            return new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));
        }

        public static IsoDirection Opposite(this IsoDirection dir) => (IsoDirection)(((int)dir + 4) % 8);
    }
}
