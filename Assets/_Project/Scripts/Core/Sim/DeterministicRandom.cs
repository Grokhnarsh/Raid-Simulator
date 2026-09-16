using System.Collections.Generic;
using UnityEngine;

namespace EmberDepths.Core.Sim
{
    /// <summary>
    /// Seeded PRNG (xorshift128) used everywhere the outcome must be reproducible:
    /// dungeon layout, loot rolls, boss ability selection.
    ///
    /// <see cref="UnityEngine.Random"/> is deliberately avoided in simulation code.
    /// It is global mutable state, so a stray call from a particle system or an
    /// editor tool silently changes the dungeon a seed produces.
    ///
    /// Split streams with <see cref="Fork"/> so that, for example, re-rolling loot
    /// cannot shift the layout that was already generated from the same seed.
    /// </summary>
    public sealed class DeterministicRandom
    {
        private uint _x, _y, _z, _w;

        public int Seed { get; }

        public DeterministicRandom(int seed)
        {
            Seed = seed;
            // SplitMix-style scatter: adjacent seeds must not produce similar streams.
            unchecked
            {
                uint s = (uint)seed;
                _x = Scatter(ref s);
                _y = Scatter(ref s);
                _z = Scatter(ref s);
                _w = Scatter(ref s);
                if ((_x | _y | _z | _w) == 0u) _w = 0x9E3779B9u; // xorshift must not start at zero
            }
        }

        private static uint Scatter(ref uint state)
        {
            unchecked
            {
                state += 0x9E3779B9u;
                uint z = state;
                z = (z ^ (z >> 16)) * 0x85EBCA6Bu;
                z = (z ^ (z >> 13)) * 0xC2B2AE35u;
                return z ^ (z >> 16);
            }
        }

        /// <summary>
        /// A new independent stream derived from this one plus a label. Use a stable
        /// label per subsystem ("layout", "loot", "boss") so streams stay separated
        /// across code changes.
        /// </summary>
        public DeterministicRandom Fork(string label)
        {
            unchecked
            {
                int h = Seed;
                for (int i = 0; i < label.Length; i++) h = h * 31 + label[i];
                return new DeterministicRandom(h ^ (int)NextUInt());
            }
        }

        public uint NextUInt()
        {
            unchecked
            {
                uint t = _x ^ (_x << 11);
                _x = _y; _y = _z; _z = _w;
                _w = _w ^ (_w >> 19) ^ t ^ (t >> 8);
                return _w;
            }
        }

        /// <summary>Uniform in [0, 1).</summary>
        public float Value => (NextUInt() >> 8) * (1f / 16777216f);

        /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            uint span = (uint)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextUInt() % span);
        }

        /// <summary>Uniform float in [min, max).</summary>
        public float Range(float min, float max) => min + Value * (max - min);

        /// <summary>True with probability <paramref name="probability"/> (0..1).</summary>
        public bool Chance(float probability) => Value < probability;

        public T Pick<T>(IReadOnlyList<T> items) =>
            items == null || items.Count == 0 ? default : items[Range(0, items.Count)];

        /// <summary>
        /// Picks an index proportional to <paramref name="weights"/>. Returns -1 if
        /// every weight is zero, which callers should treat as "nothing to pick"
        /// rather than defaulting to index 0.
        /// </summary>
        public int PickWeighted(IReadOnlyList<float> weights)
        {
            if (weights == null || weights.Count == 0) return -1;

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
                if (weights[i] > 0f) total += weights[i];

            if (total <= 0f) return -1;

            float roll = Value * total;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f) continue;
                roll -= weights[i];
                if (roll <= 0f) return i;
            }

            return weights.Count - 1;
        }

        /// <summary>In-place Fisher-Yates.</summary>
        public void Shuffle<T>(IList<T> items)
        {
            for (int i = items.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        /// <summary>Random point inside the unit circle — for scatter placement.</summary>
        public Vector2 InsideUnitCircle()
        {
            float angle = Value * Mathf.PI * 2f;
            float radius = Mathf.Sqrt(Value);
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }
    }
}
