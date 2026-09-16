using System;
using RaidSim.Core.Combat;
using UnityEngine;

namespace RaidSim.Combat.Data
{
    /// <summary>
    /// Authored description of a class's basic attack.
    /// </summary>
    /// <remarks>
    /// <para>A tank's slow heavy swing, a ranged attacker's shot from twenty-five metres and a
    /// caster's magical bolt are the same system with different numbers. A practice target that
    /// never attacks is simply a swing interval of zero.</para>
    /// <para>Lives on <c>ClassDefinition</c>, so every character of a class shares it and a new
    /// enemy type needs no code.</para>
    /// </remarks>
    [Serializable]
    public struct AutoAttackData
    {
        [Tooltip("Seconds between swings. Zero means this class has no basic attack at all.")]
        [Min(0f)]
        public float SwingInterval;

        [Tooltip("Damage before power scaling and mitigation.")]
        [Min(0f)]
        public float BaseDamage;

        [Tooltip("How much of the attacker's attack or spell power is added to the base damage.")]
        [Min(0f)]
        public float PowerCoefficient;

        [Tooltip("Physical scales from attack power and is reduced by armour; magic from spell power, reduced by resistance.")]
        public DamageType DamageType;

        [Tooltip("Maximum distance between footprints at which the swing lands, in metres.")]
        [Min(0f)]
        public float Range;

        [Tooltip("Name shown in the combat log. Presentation only.")]
        public string Label;

        /// <summary>Converts to the immutable profile the simulation uses.</summary>
        public AutoAttackProfile ToRuntime() => new AutoAttackProfile(
            SwingInterval,
            BaseDamage,
            PowerCoefficient,
            DamageType,
            Range,
            string.IsNullOrWhiteSpace(Label) ? "Attack" : Label);
    }
}
