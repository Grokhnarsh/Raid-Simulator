namespace RaidSim.Core.Common
{
    /// <summary>
    /// Who an entity fights for.
    /// </summary>
    /// <remarks>
    /// Hostility is asked through <see cref="FactionRelations"/> rather than compared inline, so
    /// mind-control, neutral critters or faction-swap mechanics become a relation change instead of
    /// a rewrite of every targeting call site.
    /// </remarks>
    public enum Faction
    {
        /// <summary>Unassigned. Treated as hostile to nobody.</summary>
        Neutral = 0,

        /// <summary>The player's raid group.</summary>
        Raid = 1,

        /// <summary>Encounter enemies: trash, adds and bosses.</summary>
        Enemy = 2,
    }

    /// <summary>Resolves whether two factions are hostile, friendly or indifferent.</summary>
    public static class FactionRelations
    {
        public static bool IsHostile(Faction observer, Faction other)
        {
            if (observer == Faction.Neutral || other == Faction.Neutral)
            {
                return false;
            }

            return observer != other;
        }

        public static bool IsFriendly(Faction observer, Faction other)
        {
            return observer != Faction.Neutral && observer == other;
        }
    }
}
