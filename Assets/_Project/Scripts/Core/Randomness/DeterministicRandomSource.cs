using System;

namespace RaidSim.Core.Randomness
{
    /// <summary>
    /// Seeded pseudo-random source. The same seed always produces the same sequence.
    /// </summary>
    /// <remarks>
    /// <para>Implements xorshift128 rather than wrapping <see cref="System.Random"/>, because
    /// <see cref="System.Random"/>'s sequence is not contractually stable across .NET versions or
    /// platforms. A replay that produced different criticals on a different machine would make the
    /// encounter simulator's output impossible to compare, which is the one thing it exists to do.
    /// </para>
    /// <para>The algorithm is small, fast and has a long period. It is not cryptographically secure
    /// and must never be used where that matters.</para>
    /// </remarks>
    public sealed class DeterministicRandomSource : IRandomSource
    {
        // 1 / 2^24. Floats have 24 bits of mantissa, so this converts the top 24 bits of a draw
        // into [0, 1) without ever rounding up to exactly 1.
        private const float FloatScale = 1f / 16777216f;

        private uint _x;
        private uint _y;
        private uint _z;
        private uint _w;

        public DeterministicRandomSource(int seed)
        {
            Seed = seed;
            Reset();
        }

        /// <summary>The seed this source was created with.</summary>
        public int Seed { get; }

        /// <summary>Number of draws taken. Part of the state a replay has to match.</summary>
        public long DrawCount { get; private set; }

        /// <summary>Returns the stream to its starting point.</summary>
        public void Reset()
        {
            // Scatter the seed across the state. A state of all zeroes is a fixed point for
            // xorshift and would produce nothing but zeroes, so the constants guarantee it cannot
            // happen even for seed 0.
            unchecked
            {
                _x = (uint)Seed ^ 0x9E3779B9u;
                _y = _x ^ 0x85EBCA6Bu;
                _z = _y ^ 0xC2B2AE35u;
                _w = _z ^ 0x27D4EB2Fu;
            }

            DrawCount = 0;
        }

        public float NextFloat() => (NextUInt() >> 8) * FloatScale;

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            long range = (long)maxExclusive - minInclusive;
            return (int)(minInclusive + (long)(NextFloat() * range));
        }

        public bool Chance(float probability)
        {
            // Certainty must not depend on a draw: a 0% chance never fires and a 100% chance always
            // does, even if the stream is at an extreme.
            if (probability <= 0f)
            {
                return false;
            }

            return probability >= 1f || NextFloat() < probability;
        }

        private uint NextUInt()
        {
            DrawCount++;
            unchecked
            {
                uint t = _x ^ (_x << 11);
                _x = _y;
                _y = _z;
                _z = _w;
                _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
                return _w;
            }
        }
    }
}
