using System;
using RaidSim.Core.Mathematics;

namespace RaidSim.Core.Combat
{
    /// <summary>
    /// The balance numbers the damage and healing pipelines need.
    /// </summary>
    /// <remarks>
    /// <para>These are tuning values, so they are authored data rather than constants in code — see
    /// <c>CLAUDE.md</c>, architecture rule 5. Unity authors them on a <c>CombatTuningAsset</c>
    /// and hands the pipeline this immutable snapshot; headless runs construct one directly.</para>
    /// <para>There is deliberately no default instance. A pipeline with no tuning is a
    /// configuration error that should fail at bootstrap with a message, not silently run on
    /// numbers somebody typed into a constructor once.</para>
    /// </remarks>
    public sealed class CombatTuning
    {
        public CombatTuning(
            float armorConstantPerLevel,
            float resistanceConstantPerLevel,
            float maximumMitigation)
        {
            if (armorConstantPerLevel <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(armorConstantPerLevel),
                    "Must be positive; it is the denominator of the mitigation curve.");
            }

            if (resistanceConstantPerLevel <= 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resistanceConstantPerLevel),
                    "Must be positive; it is the denominator of the mitigation curve.");
            }

            ArmorConstantPerLevel = armorConstantPerLevel;
            ResistanceConstantPerLevel = resistanceConstantPerLevel;

            // A cap at or above 1 would allow total immunity from rating alone, which no amount of
            // gear should achieve; immunity is an effect, not a stat threshold.
            MaximumMitigation = SimMath.Clamp(maximumMitigation, 0f, 0.99f);
        }

        /// <summary>
        /// Scales the armour mitigation curve by the attacker's level. Higher means armour is worth
        /// less against that attacker.
        /// </summary>
        public float ArmorConstantPerLevel { get; }

        /// <summary>As <see cref="ArmorConstantPerLevel"/>, for magical damage.</summary>
        public float ResistanceConstantPerLevel { get; }

        /// <summary>Hard ceiling on the fraction of damage that rating alone may remove.</summary>
        public float MaximumMitigation { get; }

        /// <summary>The mitigation constant for a damage type at a given attacker level.</summary>
        public float MitigationConstant(DamageType type, int attackerLevel)
        {
            float perLevel = type == DamageType.Magic
                ? ResistanceConstantPerLevel
                : ArmorConstantPerLevel;

            return perLevel * Math.Max(1, attackerLevel);
        }
    }
}
