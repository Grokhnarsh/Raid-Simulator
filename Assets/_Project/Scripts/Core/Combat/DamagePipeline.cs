using System;
using RaidSim.Core.Entities;
using RaidSim.Core.Randomness;
using RaidSim.Core.Stats;

namespace RaidSim.Core.Combat
{
    /// <summary>
    /// Resolves a <see cref="DamageRequest"/> into a <see cref="DamageResult"/>.
    /// </summary>
    /// <remarks>
    /// <para>The design brief's formula is
    /// <c>BaseDamage × AbilityMultiplier × CritMultiplier × Mitigation</c>. This is that formula,
    /// written as an ordered sequence of steps rather than one expression, because every later
    /// addition — resistance penetration, absorption shields, reflects, a damage-over-time
    /// snapshot — is a step inserted at a known point rather than a rewrite of an equation.</para>
    /// <para>The order below is not arbitrary. Mitigation applies after the attacker's own
    /// multipliers so that armour reduces the whole hit including its critical bonus, and the
    /// target's incoming multiplier applies after mitigation so a damage-reduction cooldown is
    /// worth the same proportion whatever the target's armour happens to be.</para>
    /// <para>The pipeline is pure: it reads stats and rolls randomness, but the only thing it
    /// mutates is the target's health at the final step. That is what lets every intermediate rule
    /// be unit tested without an encounter running.</para>
    /// </remarks>
    public sealed class DamagePipeline
    {
        private readonly CombatTuning _tuning;
        private readonly IRandomSource _random;

        public DamagePipeline(CombatTuning tuning, IRandomSource random)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Computes the damage a request would deal, without touching the target.
        /// </summary>
        /// <remarks>
        /// Separate from <see cref="Resolve"/> so AI and UI can ask "how much would this hit for?"
        /// — and so the arithmetic can be tested without a health pool in the way.
        /// </remarks>
        public float Calculate(in DamageRequest request, bool isCritical, out float mitigated)
        {
            mitigated = 0f;
            if (!request.IsValid)
            {
                return 0f;
            }

            // 1. Scale the base amount by the source's power.
            float raw = request.BaseAmount + PowerContribution(request);

            // 2. The ability's own multiplier.
            raw *= Math.Max(0f, request.AbilityMultiplier);

            // 3. Critical strike.
            if (isCritical && request.Source != null)
            {
                raw *= request.Source.Stats.Get(StatType.CriticalMultiplier);
            }

            // 4. The source's outgoing damage modifier.
            if (request.Source != null)
            {
                raw *= request.Source.Stats.Get(StatType.DamageDoneModifier);
            }

            // 5. Mitigation from armour or resistance. True damage skips this entirely.
            int attackerLevel = request.Source?.Level ?? 1;
            float mitigationFraction = Mitigation.FractionFor(request.Target, request.Type, attackerLevel, _tuning);
            mitigated = raw * mitigationFraction;
            float afterMitigation = raw - mitigated;

            // 6. The target's incoming damage modifier, applied last so a reduction cooldown is
            //    worth the same proportion regardless of the target's mitigation rating.
            afterMitigation *= request.Target.Stats.Get(StatType.DamageTakenModifier);

            return Math.Max(0f, afterMitigation);
        }

        /// <summary>
        /// Rolls, calculates and applies damage to the target's health.
        /// </summary>
        /// <remarks>
        /// This is the only place in the project that removes health as a result of an attack. The
        /// result's <c>Applied</c> and <c>Overkill</c> come from what the pool actually removed, not
        /// from a second calculation, so they cannot drift apart from the pool's own clamping.
        /// </remarks>
        public DamageResult Resolve(in DamageRequest request)
        {
            if (!request.IsValid || !request.Target.IsAlive)
            {
                return DamageResult.None;
            }

            bool isCritical = RollCritical(request);
            float amount = Calculate(request, isCritical, out float mitigated);

            float applied = request.Target.Health.Remove(amount);
            float overkill = Math.Max(0f, amount - applied);
            bool wasLethal = !request.Target.IsAlive;

            return new DamageResult(
                request.Source?.Id ?? Common.EntityId.None,
                request.Target.Id,
                request.Type,
                amount,
                applied,
                mitigated,
                overkill,
                isCritical,
                wasLethal,
                request.SourceLabel);
        }

        private float PowerContribution(in DamageRequest request)
        {
            if (request.Source == null || request.PowerCoefficient <= 0f)
            {
                return 0f;
            }

            StatType powerStat = request.Type.PowerStat();
            if (powerStat == StatType.None)
            {
                // True damage does not scale with gear; that is what makes it usable for mechanics
                // whose lethality must stay predictable.
                return 0f;
            }

            return request.Source.Stats.Get(powerStat) * request.PowerCoefficient;
        }

        private bool RollCritical(in DamageRequest request)
        {
            if (!request.CanCritical || request.Source == null)
            {
                return false;
            }

            return _random.Chance(request.Source.Stats.Get(StatType.CriticalChance));
        }
    }
}
