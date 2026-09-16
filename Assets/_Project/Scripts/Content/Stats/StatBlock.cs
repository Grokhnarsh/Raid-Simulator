using System;
using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>
    /// The complete numeric description of an actor. Authored on
    /// <see cref="ClassDefinition"/> and <see cref="EnemyDefinition"/>; copied into
    /// a mutable runtime block on spawn.
    ///
    /// Resistances are stored as a percentage rather than flat reduction so that a
    /// level-40 fire enemy and a level-1 one can share the same tuning curve.
    /// </summary>
    [Serializable]
    public struct StatBlock
    {
        [Header("Survivability")]
        [Tooltip("Full health pool at spawn.")]
        public float MaxHealth;

        [Tooltip("Flat reduction applied to Physical damage before resistances.")]
        public float Armor;

        [Range(-0.75f, 0.9f)]
        [Tooltip("Fraction of Fire damage ignored. Negative means extra vulnerable.")]
        public float FireResist;

        [Range(-0.75f, 0.9f)]
        public float FrostResist;

        [Range(-0.75f, 0.9f)]
        public float ArcaneResist;

        [Header("Offence")]
        [Tooltip("Scales every damage and healing number this actor produces.")]
        public float Power;

        [Range(0f, 1f)]
        public float CritChance;

        [Min(1f)]
        public float CritMultiplier;

        [Header("Tempo")]
        [Tooltip("Movement speed in tiles per second.")]
        public float MoveSpeed;

        [Tooltip("Multiplies cast speed and shortens cooldowns. 1 is baseline.")]
        public float Haste;

        [Header("Resource")]
        public ResourceKind ResourceKind;
        public float MaxResource;

        [Tooltip("Resource regenerated per second while out of combat cooldown.")]
        public float ResourceRegen;

        [Header("Threat")]
        [Tooltip("Multiplies threat generated. Tanks sit around 4, healers around 0.5.")]
        public float ThreatModifier;

        /// <summary>
        /// Sensible baseline so a freshly created asset is playable before anyone
        /// touches the numbers. Every field here is deliberately non-zero: a zero
        /// MaxHealth or MoveSpeed produces an actor that dies or freezes on spawn,
        /// which reads as a bug rather than as unfinished tuning.
        /// </summary>
        public static StatBlock Default => new StatBlock
        {
            MaxHealth = 100f,
            Armor = 0f,
            FireResist = 0f,
            FrostResist = 0f,
            ArcaneResist = 0f,
            Power = 1f,
            CritChance = 0.05f,
            CritMultiplier = 2f,
            MoveSpeed = 3.5f,
            Haste = 1f,
            ResourceKind = ResourceKind.None,
            MaxResource = 0f,
            ResourceRegen = 0f,
            ThreatModifier = 1f
        };

        public float ResistanceFor(DamageType type) => type switch
        {
            DamageType.Fire => FireResist,
            DamageType.Frost => FrostResist,
            DamageType.Arcane => ArcaneResist,
            _ => 0f
        };

        /// <summary>Multiplies every scalable field. Used for rank and depth scaling.</summary>
        public StatBlock Scaled(float healthMul, float powerMul)
        {
            StatBlock s = this;
            s.MaxHealth *= healthMul;
            s.Power *= powerMul;
            return s;
        }
    }
}
