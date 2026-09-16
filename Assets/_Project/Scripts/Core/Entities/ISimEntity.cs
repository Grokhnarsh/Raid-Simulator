using RaidSim.Core.Common;
using RaidSim.Core.Mathematics;

namespace RaidSim.Core.Entities
{
    /// <summary>
    /// Anything the simulation can locate, target or reason about in space.
    /// </summary>
    /// <remarks>
    /// <para>This is the narrowest useful contract: identity, allegiance and a position. Targeting,
    /// range checks and area-of-effect shapes need nothing more, so they work unchanged for players,
    /// enemies, bosses, pets, and later for non-combat props such as soak orbs or pillars that block
    /// line of sight.</para>
    /// <para><see cref="DisplayName"/> is for the combat log and the UI only. Gameplay code branching
    /// on it is a defect — see the architecture rule in <c>CLAUDE.md</c>.</para>
    /// </remarks>
    public interface ISimEntity
    {
        EntityId Id { get; }

        /// <summary>Presentation-only label. Never a gameplay condition.</summary>
        string DisplayName { get; }

        Faction Faction { get; }

        /// <summary>World position of the entity's base, in metres.</summary>
        Vec3 Position { get; }

        /// <summary>Normalised horizontal facing direction. Used by cone abilities and flanking rules.</summary>
        Vec3 Facing { get; }

        /// <summary>
        /// Horizontal radius of the entity's footprint, in metres. Range checks measure between
        /// footprints, so a large boss is reachable from further out than a trash mob.
        /// </summary>
        float Radius { get; }

        /// <summary>Whether the entity can currently be targeted or interacted with.</summary>
        bool IsAlive { get; }
    }
}
