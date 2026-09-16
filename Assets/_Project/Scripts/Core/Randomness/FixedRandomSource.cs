namespace RaidSim.Core.Randomness
{
    /// <summary>
    /// A random source that always returns the same value. Test-only.
    /// </summary>
    /// <remarks>
    /// Lets a test decide whether a roll succeeds, so "did the critical strike multiply correctly?"
    /// can be asked separately from "did the critical strike happen?".
    /// </remarks>
    public sealed class FixedRandomSource : IRandomSource
    {
        public FixedRandomSource(float value)
        {
            Value = value;
        }

        public float Value { get; set; }

        /// <summary>A source whose rolls always succeed against any non-zero chance.</summary>
        public static FixedRandomSource AlwaysSucceeds() => new FixedRandomSource(0f);

        /// <summary>A source whose rolls always fail against any chance below certainty.</summary>
        public static FixedRandomSource AlwaysFails() => new FixedRandomSource(0.9999999f);

        public float NextFloat() => Value;

        public int NextInt(int minInclusive, int maxExclusive) =>
            maxExclusive <= minInclusive ? minInclusive : minInclusive;

        public bool Chance(float probability)
        {
            if (probability <= 0f)
            {
                return false;
            }

            return probability >= 1f || Value < probability;
        }
    }
}
