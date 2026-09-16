using System;

namespace RaidSim.Core.Mathematics
{
    /// <summary>
    /// Small numeric helpers shared by the simulation kernel.
    /// </summary>
    /// <remarks>
    /// Mirrors the handful of <c>UnityEngine.Mathf</c> functions the simulation needs so the kernel
    /// can stay engine-free.
    /// </remarks>
    public static class SimMath
    {
        /// <summary>Tolerance used for float comparisons across the simulation.</summary>
        public const float Epsilon = 1e-5f;

        public const float Deg2Rad = (float)(Math.PI / 180.0);

        public const float Rad2Deg = (float)(180.0 / Math.PI);

        public static bool Approximately(float a, float b) => Math.Abs(a - b) <= Epsilon;

        public static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public static float Clamp01(float value) => Clamp(value, 0f, 1f);

        public static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        public static float Lerp(float a, float b, float t) => a + ((b - a) * Clamp01(t));

        /// <summary>
        /// Safe ratio helper. Returns <paramref name="fallback"/> when the denominator is zero, which
        /// keeps UI bars and AI heuristics free of NaN checks at every call site.
        /// </summary>
        public static float SafeRatio(float numerator, float denominator, float fallback = 0f)
        {
            return Math.Abs(denominator) <= Epsilon ? fallback : numerator / denominator;
        }

        /// <summary>
        /// Angle in degrees between two directions. Used for facing and cone checks.
        /// </summary>
        public static float AngleBetween(Vec3 from, Vec3 to)
        {
            Vec3 a = from.Normalized;
            Vec3 b = to.Normalized;
            if (a == Vec3.Zero || b == Vec3.Zero)
            {
                return 0f;
            }

            float dot = Clamp(Vec3.Dot(a, b), -1f, 1f);
            return (float)Math.Acos(dot) * Rad2Deg;
        }
    }
}
