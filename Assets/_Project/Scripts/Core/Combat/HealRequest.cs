using RaidSim.Core.Entities;

namespace RaidSim.Core.Combat
{
    /// <summary>
    /// A request to restore health, before any rule has been applied.
    /// </summary>
    /// <remarks>
    /// Deliberately the mirror of <see cref="DamageRequest"/>. Healing is not a negative damage
    /// special case — it scales from different stats, is not mitigated, and reports overhealing
    /// rather than overkill — but it goes through an equivalent pipeline so both share one shape.
    /// </remarks>
    public readonly struct HealRequest
    {
        public readonly ICombatEntity Source;
        public readonly ICombatEntity Target;

        /// <summary>Healing before scaling or criticals.</summary>
        public readonly float BaseAmount;

        /// <summary>How much of the source's spell power is added to the base amount.</summary>
        public readonly float PowerCoefficient;

        /// <summary>Flat multiplier owned by the ability itself.</summary>
        public readonly float AbilityMultiplier;

        public readonly bool CanCritical;

        /// <summary>Presentation-only name of what caused this. Never a gameplay condition.</summary>
        public readonly string SourceLabel;

        public HealRequest(
            ICombatEntity source,
            ICombatEntity target,
            float baseAmount,
            float powerCoefficient = 0f,
            float abilityMultiplier = 1f,
            bool canCritical = true,
            string sourceLabel = null)
        {
            Source = source;
            Target = target;
            BaseAmount = baseAmount;
            PowerCoefficient = powerCoefficient;
            AbilityMultiplier = abilityMultiplier;
            CanCritical = canCritical;
            SourceLabel = sourceLabel;
        }

        public bool IsValid => Target != null && BaseAmount >= 0f;
    }
}
