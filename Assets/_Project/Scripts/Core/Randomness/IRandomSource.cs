namespace RaidSim.Core.Randomness
{
    /// <summary>
    /// Every random decision the simulation makes goes through this.
    /// </summary>
    /// <remarks>
    /// <para>Critical strikes, random-target mechanics and loot rolls all need randomness, and the
    /// encounter simulator needs to replay a fight and get the same answer. Routing every roll
    /// through one seedable source is what makes that possible; a direct call to
    /// <c>UnityEngine.Random</c> or <c>System.Random</c> anywhere in the kernel would quietly break
    /// it.</para>
    /// <para>Tests use <see cref="FixedRandomSource"/> to make a roll land exactly where the test
    /// needs it, so "did the critical strike multiply correctly?" is a separate question from "did
    /// the critical strike happen?".</para>
    /// </remarks>
    public interface IRandomSource
    {
        /// <summary>A value in the half-open range [0, 1).</summary>
        float NextFloat();

        /// <summary>An integer in the half-open range [minInclusive, maxExclusive).</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>
        /// Rolls against a 0..1 probability. A chance of zero never succeeds and a chance of one
        /// always does, regardless of the underlying stream.
        /// </summary>
        bool Chance(float probability);
    }
}
