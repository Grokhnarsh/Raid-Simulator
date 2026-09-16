namespace RaidSim.Core.Combat
{
    /// <summary>
    /// The authored description of an entity's basic attack.
    /// </summary>
    /// <remarks>
    /// <para>Auto-attack is data, not code. A tank's slow heavy swing, a ranged attacker's shot from
    /// twenty-five metres and a caster's small magical bolt are the same system with different
    /// numbers — and a practice target that never attacks is simply a profile with no swing
    /// interval.</para>
    /// <para>Authored on <c>ClassDefinition</c> in Unity and converted here, so nothing in the
    /// simulation knows where the numbers came from.</para>
    /// </remarks>
    public readonly struct AutoAttackProfile
    {
        /// <summary>Seconds between swings. Zero or less means this entity does not auto-attack.</summary>
        public readonly float SwingInterval;

        /// <summary>Damage before power scaling and mitigation.</summary>
        public readonly float BaseDamage;

        /// <summary>How much of the attacker's attack or spell power is added to the base damage.</summary>
        public readonly float PowerCoefficient;

        public readonly DamageType DamageType;

        /// <summary>Maximum distance between footprints at which the swing can land, in metres.</summary>
        public readonly float Range;

        /// <summary>Presentation-only name for the combat log. Never a gameplay condition.</summary>
        public readonly string Label;

        public AutoAttackProfile(
            float swingInterval,
            float baseDamage,
            float powerCoefficient,
            DamageType damageType,
            float range,
            string label)
        {
            SwingInterval = swingInterval;
            BaseDamage = baseDamage;
            PowerCoefficient = powerCoefficient;
            DamageType = damageType;
            Range = range;
            Label = label;
        }

        /// <summary>An entity with no basic attack at all.</summary>
        public static AutoAttackProfile None => default;

        /// <summary>Whether this profile describes an attack that can actually happen.</summary>
        public bool IsEnabled => SwingInterval > 0f && Range > 0f;
    }
}
