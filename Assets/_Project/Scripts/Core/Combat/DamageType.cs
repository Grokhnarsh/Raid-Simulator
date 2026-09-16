using RaidSim.Core.Stats;

namespace RaidSim.Core.Combat
{
    /// <summary>
    /// What kind of damage an attack deals, and therefore what reduces it.
    /// </summary>
    /// <remarks>
    /// Values are persisted in authored assets; append, never renumber.
    /// </remarks>
    public enum DamageType
    {
        /// <summary>Weapon attacks and most melee abilities. Reduced by <see cref="StatType.Armor"/>.</summary>
        Physical = 0,

        /// <summary>Spells and most boss abilities. Reduced by <see cref="StatType.Resistance"/>.</summary>
        Magic = 1,

        /// <summary>
        /// Unreduced. Reserved for mechanics that must not be mitigated away — a soak that has to
        /// hurt, or an enrage that has to end the fight.
        /// </summary>
        True = 2,
    }

    public static class DamageTypeExtensions
    {
        /// <summary>
        /// Which stat mitigates this damage type, or <see cref="StatType.None"/> when nothing does.
        /// </summary>
        public static StatType MitigationStat(this DamageType type)
        {
            switch (type)
            {
                case DamageType.Physical:
                    return StatType.Armor;
                case DamageType.Magic:
                    return StatType.Resistance;
                default:
                    return StatType.None;
            }
        }

        /// <summary>
        /// Which stat scales this damage type's power coefficient, or
        /// <see cref="StatType.None"/> when it does not scale.
        /// </summary>
        public static StatType PowerStat(this DamageType type)
        {
            switch (type)
            {
                case DamageType.Physical:
                    return StatType.AttackPower;
                case DamageType.Magic:
                    return StatType.SpellPower;
                default:
                    return StatType.None;
            }
        }
    }
}
