using System;
using RaidSim.Core.Common;

namespace RaidSim.Core.Stats
{
    /// <summary>How a modifier folds into the final stat value.</summary>
    public enum StatModifierMode
    {
        /// <summary>Added to the base value before any multiplier. Applied first.</summary>
        Flat = 0,

        /// <summary>
        /// Percent bonuses that add together before multiplying: +20% and +30% give +50%, not +56%.
        /// Use for stacking buffs of the same kind.
        /// </summary>
        PercentAdditive = 1,

        /// <summary>
        /// Percent bonuses that multiply with each other. Use for effects that should stay valuable
        /// no matter how many are already active, such as damage-reduction cooldowns.
        /// </summary>
        PercentMultiplicative = 2,
    }

    /// <summary>
    /// A single contribution to a stat, owned by whatever applied it.
    /// </summary>
    /// <remarks>
    /// <paramref name="Source"/> identifies the owner so an effect, an item or an aura can remove
    /// exactly its own contributions when it expires, without recomputing the world.
    /// </remarks>
    public readonly struct StatModifier : IEquatable<StatModifier>
    {
        public readonly StatType Stat;
        public readonly StatModifierMode Mode;
        public readonly float Value;
        public readonly EntityId Source;

        /// <summary>
        /// Opaque key identifying the thing that applied this modifier (effect instance, item slot).
        /// Zero means "unattributed".
        /// </summary>
        public readonly int SourceKey;

        public StatModifier(StatType stat, StatModifierMode mode, float value, EntityId source = default, int sourceKey = 0)
        {
            Stat = stat;
            Mode = mode;
            Value = value;
            Source = source;
            SourceKey = sourceKey;
        }

        public static StatModifier Flat(StatType stat, float value, EntityId source = default, int sourceKey = 0) =>
            new StatModifier(stat, StatModifierMode.Flat, value, source, sourceKey);

        public static StatModifier PercentAdditive(StatType stat, float value, EntityId source = default, int sourceKey = 0) =>
            new StatModifier(stat, StatModifierMode.PercentAdditive, value, source, sourceKey);

        public static StatModifier PercentMultiplicative(StatType stat, float value, EntityId source = default, int sourceKey = 0) =>
            new StatModifier(stat, StatModifierMode.PercentMultiplicative, value, source, sourceKey);

        public bool Equals(StatModifier other) =>
            Stat == other.Stat &&
            Mode == other.Mode &&
            Math.Abs(Value - other.Value) < float.Epsilon &&
            Source == other.Source &&
            SourceKey == other.SourceKey;

        public override bool Equals(object obj) => obj is StatModifier other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Stat;
                hash = (hash * 397) ^ (int)Mode;
                hash = (hash * 397) ^ Value.GetHashCode();
                hash = (hash * 397) ^ Source.GetHashCode();
                hash = (hash * 397) ^ SourceKey;
                return hash;
            }
        }
    }
}
