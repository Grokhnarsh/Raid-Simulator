namespace RaidSim.Core.Stats
{
    /// <summary>
    /// Every numeric character property the simulation knows about.
    /// </summary>
    /// <remarks>
    /// Stats are addressed by this key everywhere: base values come from data, buffs and equipment
    /// add modifiers, and gameplay code only ever asks a <see cref="StatBlock"/> for a value. New
    /// stats are added here and in the authoring data; no system needs to change.
    /// <para>
    /// Values are explicit and stable because they are persisted in save files and referenced by
    /// authored assets. Never renumber an existing entry; append instead.
    /// </para>
    /// </remarks>
    public enum StatType
    {
        None = 0,

        /// <summary>Maximum health pool.</summary>
        MaxHealth = 1,

        /// <summary>Maximum class resource pool (mana, rage, energy, ...).</summary>
        MaxResource = 2,

        /// <summary>Resource regenerated per second while the pool is not full.</summary>
        ResourceRegen = 3,

        /// <summary>Metres per second of ground movement.</summary>
        MovementSpeed = 4,

        /// <summary>Scales physical ability damage.</summary>
        AttackPower = 5,

        /// <summary>Scales magical ability damage and healing.</summary>
        SpellPower = 6,

        /// <summary>Flat physical mitigation rating.</summary>
        Armor = 7,

        /// <summary>Flat magical mitigation rating.</summary>
        Resistance = 8,

        /// <summary>Chance in the 0..1 range for an ability to critically strike.</summary>
        CriticalChance = 9,

        /// <summary>Damage/healing multiplier applied on a critical strike.</summary>
        CriticalMultiplier = 10,

        /// <summary>Multiplier on all threat this entity generates.</summary>
        ThreatModifier = 11,

        /// <summary>Multiplier on incoming damage. Damage reduction cooldowns lower this.</summary>
        DamageTakenModifier = 12,

        /// <summary>Multiplier on all outgoing damage.</summary>
        DamageDoneModifier = 13,

        /// <summary>Multiplier on all outgoing healing.</summary>
        HealingDoneModifier = 14,

        /// <summary>Multiplier on cast speed. Higher is faster.</summary>
        CastSpeed = 15,
    }
}
