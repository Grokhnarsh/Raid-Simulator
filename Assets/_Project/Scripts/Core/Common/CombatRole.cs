namespace RaidSim.Core.Common
{
    /// <summary>
    /// Raid role of a group member. Drives AI profile selection, healer priority and raid-frame
    /// sorting; never the class itself.
    /// </summary>
    /// <remarks>
    /// Roles are intentionally separate from classes: one class may offer several roles through
    /// specialisations, and the group composition rules only ever reason about roles. The group
    /// size and the number of tanks, healers or damage dealers are data, never assumptions baked
    /// into code.
    /// </remarks>
    public enum CombatRole
    {
        None = 0,
        Tank = 1,
        Healer = 2,
        MeleeDamage = 3,
        RangedDamage = 4,
    }

    public static class CombatRoleExtensions
    {
        public static bool IsDamageDealer(this CombatRole role) =>
            role == CombatRole.MeleeDamage || role == CombatRole.RangedDamage;

        public static bool PrefersMeleeRange(this CombatRole role) =>
            role == CombatRole.Tank || role == CombatRole.MeleeDamage;
    }
}
