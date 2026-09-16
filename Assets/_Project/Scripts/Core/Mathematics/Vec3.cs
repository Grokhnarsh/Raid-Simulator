using System;
using System.Globalization;

namespace RaidSim.Core.Mathematics
{
    /// <summary>
    /// Engine-independent 3D vector used by the simulation kernel.
    /// </summary>
    /// <remarks>
    /// The simulation deliberately does not reference <c>UnityEngine.Vector3</c>. Keeping a private
    /// vector type is what allows the whole encounter to be simulated headlessly (tests, CI, the
    /// "Run Encounter" batch simulator) while Unity remains a presentation layer that converts at
    /// the boundary. See <c>ARCHITECTURE.md</c>, "Simulation / Presentation split".
    /// <para>
    /// Positions are real 3D. The 2.5D look is a camera choice, not a simulation constraint, so
    /// height, bridges, platforms and line-of-sight stay expressible.
    /// </para>
    /// </remarks>
    [Serializable]
    public readonly struct Vec3 : IEquatable<Vec3>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 Zero => new Vec3(0f, 0f, 0f);
        public static Vec3 One => new Vec3(1f, 1f, 1f);
        public static Vec3 Up => new Vec3(0f, 1f, 0f);
        public static Vec3 Forward => new Vec3(0f, 0f, 1f);
        public static Vec3 Right => new Vec3(1f, 0f, 0f);

        public float SqrMagnitude => (X * X) + (Y * Y) + (Z * Z);

        public float Magnitude => (float)Math.Sqrt(SqrMagnitude);

        /// <summary>Same vector flattened onto the horizontal plane.</summary>
        public Vec3 Flat => new Vec3(X, 0f, Z);

        public Vec3 Normalized
        {
            get
            {
                float magnitude = Magnitude;
                return magnitude > SimMath.Epsilon
                    ? new Vec3(X / magnitude, Y / magnitude, Z / magnitude)
                    : Zero;
            }
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vec3 operator -(Vec3 v) => new Vec3(-v.X, -v.Y, -v.Z);

        public static Vec3 operator *(Vec3 v, float scalar) => new Vec3(v.X * scalar, v.Y * scalar, v.Z * scalar);

        public static Vec3 operator *(float scalar, Vec3 v) => v * scalar;

        public static Vec3 operator /(Vec3 v, float scalar) => new Vec3(v.X / scalar, v.Y / scalar, v.Z / scalar);

        public static bool operator ==(Vec3 a, Vec3 b) => a.Equals(b);

        public static bool operator !=(Vec3 a, Vec3 b) => !a.Equals(b);

        public static float Dot(Vec3 a, Vec3 b) => (a.X * b.X) + (a.Y * b.Y) + (a.Z * b.Z);

        public static Vec3 Cross(Vec3 a, Vec3 b) => new Vec3(
            (a.Y * b.Z) - (a.Z * b.Y),
            (a.Z * b.X) - (a.X * b.Z),
            (a.X * b.Y) - (a.Y * b.X));

        public static float Distance(Vec3 a, Vec3 b) => (a - b).Magnitude;

        public static float SqrDistance(Vec3 a, Vec3 b) => (a - b).SqrMagnitude;

        /// <summary>
        /// Distance ignoring height. Range checks use this by default: a target standing on a
        /// one-metre step is still in melee range.
        /// </summary>
        public static float FlatDistance(Vec3 a, Vec3 b) => (a.Flat - b.Flat).Magnitude;

        public static Vec3 Lerp(Vec3 a, Vec3 b, float t)
        {
            t = SimMath.Clamp01(t);
            return new Vec3(
                a.X + ((b.X - a.X) * t),
                a.Y + ((b.Y - a.Y) * t),
                a.Z + ((b.Z - a.Z) * t));
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxDistance"/>.</summary>
        public static Vec3 MoveTowards(Vec3 current, Vec3 target, float maxDistance)
        {
            Vec3 delta = target - current;
            float distance = delta.Magnitude;
            if (distance <= maxDistance || distance <= SimMath.Epsilon)
            {
                return target;
            }

            return current + (delta / distance * maxDistance);
        }

        public bool Equals(Vec3 other) =>
            SimMath.Approximately(X, other.X) &&
            SimMath.Approximately(Y, other.Y) &&
            SimMath.Approximately(Z, other.Z);

        public override bool Equals(object obj) => obj is Vec3 other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }

        public override string ToString() => string.Format(
            CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", X, Y, Z);
    }
}
