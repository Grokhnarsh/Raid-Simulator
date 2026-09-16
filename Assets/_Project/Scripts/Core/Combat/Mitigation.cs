using RaidSim.Core.Entities;
using RaidSim.Core.Mathematics;
using RaidSim.Core.Stats;

namespace RaidSim.Core.Combat
{
    /// <summary>
    /// Turns a mitigation rating into the fraction of damage it removes.
    /// </summary>
    /// <remarks>
    /// <para>The curve is <c>rating / (rating + K)</c>, where <c>K</c> scales with the attacker's
    /// level. It is chosen for two properties the design needs: it has diminishing returns, so
    /// stacking armour never reaches immunity, and it is scale-free, so the same rating means
    /// proportionally less against a higher-level attacker without any special-casing.</para>
    /// <para><c>K</c> and the ceiling are authored in <see cref="CombatTuning"/>. The shape of the
    /// curve is a system rule and lives here; the numbers that position it do not.</para>
    /// </remarks>
    public static class Mitigation
    {
        /// <summary>
        /// Fraction of damage removed, in the range 0..<see cref="CombatTuning.MaximumMitigation"/>.
        /// </summary>
        public static float Fraction(float rating, DamageType type, int attackerLevel, CombatTuning tuning)
        {
            if (type == DamageType.True || rating <= 0f || tuning == null)
            {
                return 0f;
            }

            float constant = tuning.MitigationConstant(type, attackerLevel);
            float fraction = rating / (rating + constant);
            return SimMath.Clamp(fraction, 0f, tuning.MaximumMitigation);
        }

        /// <summary>Fraction of damage <paramref name="target"/> removes from an incoming hit.</summary>
        public static float FractionFor(
            ICombatEntity target,
            DamageType type,
            int attackerLevel,
            CombatTuning tuning)
        {
            StatType stat = type.MitigationStat();
            if (target == null || stat == StatType.None)
            {
                return 0f;
            }

            return Fraction(target.Stats.Get(stat), type, attackerLevel, tuning);
        }
    }
}
