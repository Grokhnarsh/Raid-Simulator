using RaidSim.Core.Common;

namespace RaidSim.Core.Combat
{
    /// <summary>
    /// What actually happened when a <see cref="DamageRequest"/> was resolved.
    /// </summary>
    /// <remarks>
    /// <para>The combat log, the statistics collector and (from Phase 6) the threat system all read
    /// this rather than recomputing anything. That is why it reports the intermediate numbers as
    /// well as the final one: a combat log that cannot show how much a cooldown prevented cannot
    /// teach a player whether the cooldown helped.</para>
    /// <para><see cref="Applied"/> is what the health pool actually removed, so
    /// <see cref="Overkill"/> is the part of a killing blow that landed on an already-empty pool.
    /// Both come from the pool, never from a second calculation.</para>
    /// </remarks>
    public readonly struct DamageResult
    {
        public readonly EntityId Source;
        public readonly EntityId Target;
        public readonly DamageType Type;

        /// <summary>Damage after every rule, before the health pool clamped it.</summary>
        public readonly float Amount;

        /// <summary>Damage actually removed from health. Less than <see cref="Amount"/> on a kill.</summary>
        public readonly float Applied;

        /// <summary>How much mitigation removed. Reported so the log can show it.</summary>
        public readonly float Mitigated;

        /// <summary>The part of a killing blow that exceeded the remaining health.</summary>
        public readonly float Overkill;

        public readonly bool WasCritical;

        /// <summary>Whether this hit is what took the target's health to zero.</summary>
        public readonly bool WasLethal;

        /// <summary>Presentation-only name of what caused this.</summary>
        public readonly string SourceLabel;

        public DamageResult(
            EntityId source,
            EntityId target,
            DamageType type,
            float amount,
            float applied,
            float mitigated,
            float overkill,
            bool wasCritical,
            bool wasLethal,
            string sourceLabel)
        {
            Source = source;
            Target = target;
            Type = type;
            Amount = amount;
            Applied = applied;
            Mitigated = mitigated;
            Overkill = overkill;
            WasCritical = wasCritical;
            WasLethal = wasLethal;
            SourceLabel = sourceLabel;
        }

        /// <summary>A resolution that did nothing. Returned when a request could not be applied.</summary>
        public static DamageResult None => default;

        public bool DidAnything => Applied > 0f;
    }
}
