using RaidSim.Core.Common;

namespace RaidSim.Core.Events
{
    /// <summary>
    /// Raised when an entity's current target changes.
    /// </summary>
    /// <remarks>
    /// The target frame, the ability bar's range indicator and the selection highlight all listen to
    /// this instead of polling a selection every frame.
    /// </remarks>
    public readonly struct TargetChangedEvent
    {
        /// <summary>Who changed target.</summary>
        public readonly EntityId Selector;

        /// <summary>The new target, or <see cref="EntityId.None"/> when the target was cleared.</summary>
        public readonly EntityId NewTarget;

        /// <summary>The target that was replaced, or <see cref="EntityId.None"/>.</summary>
        public readonly EntityId PreviousTarget;

        public TargetChangedEvent(EntityId selector, EntityId newTarget, EntityId previousTarget)
        {
            Selector = selector;
            NewTarget = newTarget;
            PreviousTarget = previousTarget;
        }
    }
}
