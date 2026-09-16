using System;
using System.Collections.Generic;
using RaidSim.Core.Common;
using RaidSim.Core.Entities;
using RaidSim.Core.Events;
using RaidSim.Core.Randomness;

namespace RaidSim.Core.Combat
{
    /// <summary>
    /// The single entry point for dealing damage and healing, and the only thing that reports death.
    /// </summary>
    /// <remarks>
    /// <para>Everything that wants to hurt or heal something calls <see cref="ApplyDamage"/> or
    /// <see cref="ApplyHealing"/>. Nothing else touches a health pool as a result of combat. That
    /// single funnel is what guarantees no damage source can skip mitigation, forget to publish its
    /// event, or fail to notice that it killed something.</para>
    /// <para><b>Death is reported once.</b> The system remembers who it has already declared dead,
    /// so a corpse taking a second hit in the same tick, or two sources landing simultaneously,
    /// still produces exactly one <see cref="EntityDiedEvent"/>. The record is cleared when an
    /// entity leaves the simulation or the encounter resets, so a later resurrection can die
    /// again.</para>
    /// <para>The system publishes events and does not listen for its own. Threat (Phase 6), the
    /// combat log, statistics and the UI all subscribe; none of them is referenced here.</para>
    /// </remarks>
    public sealed class CombatSystem : IDisposable
    {
        private readonly IEventBus _events;
        private readonly HashSet<EntityId> _reportedDead = new HashSet<EntityId>();
        private readonly IDisposable _unregisteredSubscription;

        public CombatSystem(IEventBus events, CombatTuning tuning, IRandomSource random)
        {
            _events = events ?? throw new ArgumentNullException(nameof(events));
            Damage = new DamagePipeline(tuning, random);
            Healing = new HealingPipeline(random);

            // An entity that leaves the simulation must not stay on the dead list, or a pooled
            // object reusing its id could never die again.
            _unregisteredSubscription = _events.Subscribe<EntityUnregisteredEvent>(
                evt => _reportedDead.Remove(evt.Entity));
        }

        /// <summary>The damage pipeline. Exposed so AI and UI can ask "how much would this hit for?".</summary>
        public DamagePipeline Damage { get; }

        /// <summary>The healing pipeline.</summary>
        public HealingPipeline Healing { get; }

        /// <summary>Number of entities currently recorded as dead.</summary>
        public int ReportedDeathCount => _reportedDead.Count;

        /// <summary>
        /// Resolves and applies a damage request, publishing the resulting events.
        /// </summary>
        /// <returns>What happened, or <see cref="DamageResult.None"/> if nothing did.</returns>
        public DamageResult ApplyDamage(in DamageRequest request)
        {
            if (!request.IsValid || !request.Target.IsAlive)
            {
                return DamageResult.None;
            }

            DamageResult result = Damage.Resolve(request);
            if (!result.DidAnything && !result.WasLethal)
            {
                // A fully mitigated or zero-amount hit still happened; publishing it keeps the
                // combat log honest about attacks that did nothing.
                _events.Publish(new DamageDealtEvent(result));
                _events.Publish(new DamageTakenEvent(result));
                return result;
            }

            _events.Publish(new DamageDealtEvent(result));
            _events.Publish(new DamageTakenEvent(result));

            if (result.WasLethal)
            {
                ReportDeath(request.Target, result.Source, result.SourceLabel);
            }

            return result;
        }

        /// <summary>Resolves and applies a healing request, publishing the resulting event.</summary>
        public HealResult ApplyHealing(in HealRequest request)
        {
            if (!request.IsValid)
            {
                return HealResult.None;
            }

            HealResult result = Healing.Resolve(request);
            _events.Publish(new HealAppliedEvent(result));
            return result;
        }

        /// <summary>
        /// Announces the start of an attack or cast. Called before the swing lands so animation,
        /// cast bars and interrupt logic can react.
        /// </summary>
        public void AnnounceAttack(ICombatEntity source, ISimEntity target, string sourceLabel)
        {
            if (source == null)
            {
                return;
            }

            _events.Publish(new AttackStartedEvent(
                source.Id,
                target?.Id ?? EntityId.None,
                sourceLabel));
        }

        /// <summary>
        /// Kills an entity outright, bypassing mitigation. For debug tooling and for mechanics whose
        /// whole purpose is to be unsurvivable.
        /// </summary>
        /// <remarks>
        /// Routed through the same funnel rather than emptying the pool directly, so the death is
        /// reported and logged exactly like any other.
        /// </remarks>
        public DamageResult Kill(ICombatEntity target, ICombatEntity killer = null, string sourceLabel = null)
        {
            if (target == null || !target.IsAlive)
            {
                return DamageResult.None;
            }

            return ApplyDamage(new DamageRequest(
                killer,
                target,
                target.Health.Current,
                DamageType.True,
                canCritical: false,
                sourceLabel: sourceLabel));
        }

        /// <summary>
        /// Forgets every recorded death. Called when an encounter resets, so entities revived for a
        /// new attempt can die again.
        /// </summary>
        public void ResetForEncounter() => _reportedDead.Clear();

        public void Dispose() => _unregisteredSubscription?.Dispose();

        private void ReportDeath(ICombatEntity entity, EntityId killer, string sourceLabel)
        {
            if (!_reportedDead.Add(entity.Id))
            {
                return;
            }

            _events.Publish(new EntityDiedEvent(entity.Id, entity.Faction, killer, sourceLabel));
        }
    }
}
