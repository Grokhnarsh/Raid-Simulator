using System;
using System.Collections.Generic;
using RaidSim.Core.Common;
using RaidSim.Core.Entities;
using RaidSim.Core.Events;
using RaidSim.Core.Simulation;
using RaidSim.Core.Targeting;

namespace RaidSim.Core.Combat
{
    /// <summary>
    /// Swings the basic attack of every entity that has one and a valid target in range.
    /// </summary>
    /// <remarks>
    /// <para>One system serves every attacker in the encounter rather than each entity running its
    /// own timer. A forty-player raid plus thirty adds is one loop over a list, which is the
    /// performance shape the project committed to in <c>CLAUDE.md</c>, architecture rule 7.</para>
    /// <para><b>Swing timing.</b> The timer only advances while a legal target is in range, and a
    /// swing is ready the moment an entity first engages. Losing the target — by walking out of
    /// range, by it dying, or by switching — <i>pauses</i> the timer rather than resetting it, so
    /// the first attack of an encounter is immediate but rapid target-swapping cannot be used to
    /// swing faster than the interval allows.</para>
    /// <para>The system never decides <i>who</i> to attack. It reads whatever the entity's
    /// <see cref="ITargetProvider"/> reports, so the player's click and an AI's choice arrive here
    /// identically.</para>
    /// </remarks>
    public sealed class AutoAttackSystem : ISimulationSystem, IDisposable
    {
        private readonly CombatSystem _combat;
        private readonly List<Attacker> _attackers = new List<Attacker>();
        private readonly Dictionary<EntityId, int> _indexById = new Dictionary<EntityId, int>();
        private readonly IDisposable _unregisteredSubscription;

        public AutoAttackSystem(CombatSystem combat, IEventBus events)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            _unregisteredSubscription = events.Subscribe<EntityUnregisteredEvent>(
                evt => Unregister(evt.Entity));
        }

        /// <inheritdoc />
        public int Order => SystemOrder.Abilities;

        /// <summary>Number of entities currently swinging.</summary>
        public int AttackerCount => _attackers.Count;

        /// <summary>
        /// Starts swinging for <paramref name="attacker"/>. A profile that is not enabled is
        /// ignored, so registering a practice target or a non-combatant is harmless.
        /// </summary>
        /// <returns>True if the entity was added.</returns>
        public bool Register(ICombatEntity attacker, in AutoAttackProfile profile, ITargetProvider targets)
        {
            if (attacker == null || targets == null || !profile.IsEnabled)
            {
                return false;
            }

            if (_indexById.ContainsKey(attacker.Id))
            {
                return false;
            }

            _indexById[attacker.Id] = _attackers.Count;
            _attackers.Add(new Attacker(attacker, profile, targets));
            return true;
        }

        public bool Unregister(EntityId id)
        {
            if (!_indexById.TryGetValue(id, out int index))
            {
                return false;
            }

            // Swap-remove keeps the tick loop over a dense list; the moved entry's index is
            // repaired so lookups stay correct.
            int last = _attackers.Count - 1;
            if (index != last)
            {
                _attackers[index] = _attackers[last];
                _indexById[_attackers[index].Entity.Id] = index;
            }

            _attackers.RemoveAt(last);
            _indexById.Remove(id);
            return true;
        }

        /// <inheritdoc />
        public void Tick(float deltaSeconds)
        {
            for (int i = 0; i < _attackers.Count; i++)
            {
                _attackers[i].Tick(deltaSeconds, _combat);
            }
        }

        /// <summary>Returns every swing timer to ready. Called when an encounter resets.</summary>
        public void ResetForEncounter()
        {
            for (int i = 0; i < _attackers.Count; i++)
            {
                _attackers[i].ResetTimer();
            }
        }

        public void Clear()
        {
            _attackers.Clear();
            _indexById.Clear();
        }

        public void Dispose() => _unregisteredSubscription?.Dispose();

        /// <summary>
        /// One entity's swing state. A class rather than a struct so the list holds references and
        /// the timer survives being read out of the list.
        /// </summary>
        private sealed class Attacker
        {
            private readonly AutoAttackProfile _profile;
            private readonly ITargetProvider _targets;
            private float _secondsUntilSwing;

            public Attacker(ICombatEntity entity, in AutoAttackProfile profile, ITargetProvider targets)
            {
                Entity = entity;
                _profile = profile;
                _targets = targets;
                _secondsUntilSwing = 0f;
            }

            public ICombatEntity Entity { get; }

            public void ResetTimer() => _secondsUntilSwing = 0f;

            public void Tick(float deltaSeconds, CombatSystem combat)
            {
                if (!Entity.IsAlive)
                {
                    return;
                }

                ICombatEntity target = ResolveTarget();
                if (target == null)
                {
                    // Pause rather than reset: swapping targets must not refresh the swing timer.
                    return;
                }

                _secondsUntilSwing -= deltaSeconds;
                if (_secondsUntilSwing > 0f)
                {
                    return;
                }

                combat.AnnounceAttack(Entity, target, _profile.Label);
                combat.ApplyDamage(new DamageRequest(
                    Entity,
                    target,
                    _profile.BaseDamage,
                    _profile.DamageType,
                    _profile.PowerCoefficient,
                    sourceLabel: _profile.Label));

                _secondsUntilSwing = _profile.SwingInterval;
            }

            /// <summary>
            /// The entity's target if it is a living hostile combatant within swing range,
            /// otherwise null.
            /// </summary>
            private ICombatEntity ResolveTarget()
            {
                if (!(_targets.CurrentTarget is ICombatEntity target) || !target.IsAlive)
                {
                    return null;
                }

                if (!FactionRelations.IsHostile(Entity.Faction, target.Faction))
                {
                    return null;
                }

                return TargetFilter.EdgeDistance(Entity, target) <= _profile.Range ? target : null;
            }
        }
    }
}
