using System;
using RaidSim.Core.Common;
using RaidSim.Core.Randomness;
using RaidSim.Core.Stats;

namespace RaidSim.Core.Combat
{
    /// <summary>
    /// Resolves a <see cref="HealRequest"/> into a <see cref="HealResult"/>.
    /// </summary>
    /// <remarks>
    /// <para>The mirror of <see cref="DamagePipeline"/>, with the differences that matter: healing
    /// always scales from spell power regardless of who casts it, nothing mitigates it, and the
    /// wasted part is overhealing rather than overkill.</para>
    /// <para>Healing does not resurrect. <see cref="Vitals.Health"/> refuses to restore a dead pool,
    /// so a heal landing on a corpse resolves to zero applied and the whole amount as overhealing —
    /// which is exactly what a healer's statistics should show.</para>
    /// </remarks>
    public sealed class HealingPipeline
    {
        private readonly IRandomSource _random;

        public HealingPipeline(IRandomSource random)
        {
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>Computes the healing a request would do, without touching the target.</summary>
        public float Calculate(in HealRequest request, bool isCritical)
        {
            if (!request.IsValid)
            {
                return 0f;
            }

            float raw = request.BaseAmount;

            if (request.Source != null && request.PowerCoefficient > 0f)
            {
                raw += request.Source.Stats.Get(StatType.SpellPower) * request.PowerCoefficient;
            }

            raw *= Math.Max(0f, request.AbilityMultiplier);

            if (isCritical && request.Source != null)
            {
                raw *= request.Source.Stats.Get(StatType.CriticalMultiplier);
            }

            if (request.Source != null)
            {
                raw *= request.Source.Stats.Get(StatType.HealingDoneModifier);
            }

            return Math.Max(0f, raw);
        }

        /// <summary>Rolls, calculates and applies healing to the target's health.</summary>
        public HealResult Resolve(in HealRequest request)
        {
            if (!request.IsValid)
            {
                return HealResult.None;
            }

            bool isCritical = request.CanCritical
                && request.Source != null
                && _random.Chance(request.Source.Stats.Get(StatType.CriticalChance));

            float amount = Calculate(request, isCritical);
            float applied = request.Target.Health.Add(amount);
            float overhealing = Math.Max(0f, amount - applied);

            return new HealResult(
                request.Source?.Id ?? EntityId.None,
                request.Target.Id,
                amount,
                applied,
                overhealing,
                isCritical,
                request.SourceLabel);
        }
    }
}
