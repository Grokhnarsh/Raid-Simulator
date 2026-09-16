using System;
using RaidSim.Core.Common;
using RaidSim.Core.Entities;
using RaidSim.Core.Events;

namespace RaidSim.Core.Targeting
{
    /// <summary>
    /// Holds one entity's current target and publishes changes.
    /// </summary>
    /// <remarks>
    /// <para>Every targeting owner uses this — the player, each group member's AI, each enemy. The
    /// player's selection is not a special case; the UI simply happens to display the one belonging
    /// to the controlled character.</para>
    /// <para>The selection clears itself when its target dies or leaves the simulation, so no
    /// listener has to defend against a stale reference and no ability can fire at a corpse that
    /// was despawned three ticks ago.</para>
    /// </remarks>
    public sealed class TargetSelection : IDisposable
    {
        private readonly EntityRegistry _registry;
        private readonly IEventBus _eventBus;
        private readonly IDisposable _unregisteredSubscription;
        private ISimEntity _target;

        public TargetSelection(EntityId owner, EntityRegistry registry, IEventBus eventBus)
        {
            Owner = owner;
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _unregisteredSubscription = _eventBus.Subscribe<EntityUnregisteredEvent>(OnEntityUnregistered);
        }

        public EntityId Owner { get; }

        /// <summary>The current target, or null.</summary>
        public ISimEntity Target => _target;

        public EntityId TargetId => _target?.Id ?? EntityId.None;

        public bool HasTarget => _target != null;

        /// <summary>True when a target is set but no longer alive. The UI greys the frame out.</summary>
        public bool HasDeadTarget => _target != null && !_target.IsAlive;

        /// <summary>
        /// Sets the target. Passing null clears it. Publishes <see cref="TargetChangedEvent"/> only
        /// when the target actually changed, so listeners can assume every event is meaningful.
        /// </summary>
        public void Set(ISimEntity target)
        {
            if (target != null && !_registry.Contains(target.Id))
            {
                // Refuse targets the simulation does not know about; they cannot be validated,
                // ranged or cleaned up.
                return;
            }

            EntityId previous = TargetId;
            EntityId next = target?.Id ?? EntityId.None;
            if (previous == next)
            {
                return;
            }

            _target = target;
            _eventBus.Publish(new TargetChangedEvent(Owner, next, previous));
        }

        public void Clear() => Set(null);

        /// <summary>Drops the target if it no longer satisfies <paramref name="filter"/>.</summary>
        public void ClearIfInvalid(ISimEntity observer, in TargetFilter filter)
        {
            if (_target != null && !filter.Matches(observer, _target))
            {
                Clear();
            }
        }

        public void Dispose() => _unregisteredSubscription?.Dispose();

        private void OnEntityUnregistered(EntityUnregisteredEvent evt)
        {
            if (_target != null && _target.Id == evt.Entity)
            {
                Clear();
            }
        }
    }
}
