using RaidSim.Core.Entities;

namespace RaidSim.Core.Combat
{
    /// <summary>
    /// A request to deal damage, before any rule has been applied.
    /// </summary>
    /// <remarks>
    /// <para>Everything that deals damage produces one of these: an auto-attack, an ability, a
    /// damage-over-time tick, a boss mechanic, an environmental hazard. The pipeline is then the
    /// single place the rules live, so a new damage source can never accidentally skip mitigation
    /// or forget to report overkill.</para>
    /// <para><see cref="SourceLabel"/> is what the combat log prints. It is a label, never a
    /// condition — see the naming rule in <c>CLAUDE.md</c>.</para>
    /// </remarks>
    public readonly struct DamageRequest
    {
        public readonly ICombatEntity Source;
        public readonly ICombatEntity Target;

        /// <summary>Damage before scaling, mitigation or criticals.</summary>
        public readonly float BaseAmount;

        public readonly DamageType Type;

        /// <summary>
        /// How much of the source's attack or spell power is added to the base amount. Zero means
        /// the damage is flat and does not grow with gear.
        /// </summary>
        public readonly float PowerCoefficient;

        /// <summary>Flat multiplier owned by the ability itself.</summary>
        public readonly float AbilityMultiplier;

        /// <summary>Whether this hit is allowed to critically strike.</summary>
        public readonly bool CanCritical;

        /// <summary>Presentation-only name of what caused this. Never a gameplay condition.</summary>
        public readonly string SourceLabel;

        public DamageRequest(
            ICombatEntity source,
            ICombatEntity target,
            float baseAmount,
            DamageType type,
            float powerCoefficient = 0f,
            float abilityMultiplier = 1f,
            bool canCritical = true,
            string sourceLabel = null)
        {
            Source = source;
            Target = target;
            BaseAmount = baseAmount;
            Type = type;
            PowerCoefficient = powerCoefficient;
            AbilityMultiplier = abilityMultiplier;
            CanCritical = canCritical;
            SourceLabel = sourceLabel;
        }

        /// <summary>Whether this request can be resolved at all.</summary>
        public bool IsValid => Target != null && BaseAmount >= 0f;
    }
}
