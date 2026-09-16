using RaidSim.Core.Entities;

namespace RaidSim.Core.Targeting
{
    /// <summary>
    /// Something that can say what an entity is currently targeting.
    /// </summary>
    /// <remarks>
    /// Narrower than <see cref="TargetSelection"/> on purpose. A system that only needs to read a
    /// target — auto-attack, the target frame, an ability's default target — takes this and
    /// therefore cannot change the selection behind the owner's back.
    /// </remarks>
    public interface ITargetProvider
    {
        /// <summary>The current target, or null.</summary>
        ISimEntity CurrentTarget { get; }
    }
}
