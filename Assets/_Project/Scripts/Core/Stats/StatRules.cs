using System;
using RaidSim.Core.Mathematics;

namespace RaidSim.Core.Stats
{
    /// <summary>
    /// Per-stat invariants: the neutral value a stat has when nothing authored it, and the range it
    /// may never leave.
    /// </summary>
    /// <remarks>
    /// <para>These are <b>system invariants, not balance numbers</b>. Every default here is the
    /// identity element for how the stat is used — multipliers default to 1 (change nothing), rates
    /// and ratings default to 0 (contribute nothing). Real values come from authored data; the
    /// project rule that gameplay code carries no tuning constants stays intact.</para>
    /// <para>The clamps exist so no data-entry mistake or stacked debuff can produce negative
    /// health, negative mitigation or a negative multiplier that would flip damage into healing.</para>
    /// </remarks>
    public static class StatRules
    {
        /// <summary>Every stat except <see cref="StatType.None"/>, in declaration order.</summary>
        public static readonly StatType[] AllStats = BuildAllStats();

        /// <summary>
        /// Value used when a stat was never authored. Always the neutral element for that stat.
        /// </summary>
        public static float DefaultBase(StatType stat)
        {
            switch (stat)
            {
                // Multipliers: neutral means "multiply by one".
                case StatType.CriticalMultiplier:
                case StatType.ThreatModifier:
                case StatType.DamageTakenModifier:
                case StatType.DamageDoneModifier:
                case StatType.HealingDoneModifier:
                case StatType.CastSpeed:
                    return 1f;

                // Pools, rates and ratings: neutral means "none".
                default:
                    return 0f;
            }
        }

        /// <summary>Constrains a computed value to the range the stat is allowed to take.</summary>
        public static float Clamp(StatType stat, float value)
        {
            if (float.IsNaN(value))
            {
                return DefaultBase(stat);
            }

            switch (stat)
            {
                // A probability.
                case StatType.CriticalChance:
                    return SimMath.Clamp01(value);

                // Multipliers may drop to zero (full immunity, a total silence) but never below:
                // a negative multiplier would turn damage into healing.
                case StatType.CriticalMultiplier:
                case StatType.ThreatModifier:
                case StatType.DamageTakenModifier:
                case StatType.DamageDoneModifier:
                case StatType.HealingDoneModifier:
                case StatType.CastSpeed:
                    return Math.Max(0f, value);

                // Pools, rates and ratings are non-negative.
                default:
                    return Math.Max(0f, value);
            }
        }

        private static StatType[] BuildAllStats()
        {
            Array raw = Enum.GetValues(typeof(StatType));
            var result = new StatType[raw.Length - 1];
            int index = 0;
            foreach (StatType stat in raw)
            {
                if (stat == StatType.None)
                {
                    continue;
                }

                result[index++] = stat;
            }

            return result;
        }
    }
}
