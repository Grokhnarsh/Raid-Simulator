using RaidSim.Core.Common;
using RaidSim.Core.Stats;
using RaidSim.Core.Vitals;

namespace RaidSim.Core.Entities
{
    /// <summary>
    /// An entity that participates in combat: it has stats, pools, and a role in the fight.
    /// </summary>
    /// <remarks>
    /// The damage pipeline, the healing pipeline, the threat table and the AI all take this
    /// interface. None of them can tell a player character from a boss, which is exactly the point:
    /// a mind-controlled raider, a friendly NPC or an add that heals its boss all work without a
    /// special case.
    /// </remarks>
    public interface ICombatEntity : ISimEntity
    {
        /// <summary>Final stat values including every active modifier.</summary>
        StatBlock Stats { get; }

        Health Health { get; }

        /// <summary>The entity's resource pool. Never null; classes that spend nothing use
        /// <see cref="ResourceKind.None"/>.</summary>
        ResourcePool Resource { get; }

        /// <summary>Role this entity fills. Drives AI profiles and healer priority, never identity.</summary>
        CombatRole Role { get; }

        /// <summary>Character level. Feeds the damage formula and level-scaled base stats.</summary>
        int Level { get; }
    }
}
