using System;
using System.Collections.Generic;
using RaidSim.Core.Combat;
using RaidSim.Core.Common;
using RaidSim.Core.Entities;
using RaidSim.Core.Events;

namespace RaidSim.DebugTools.Runtime
{
    /// <summary>
    /// Keeps the most recent combat events as readable lines, for the development overlay.
    /// </summary>
    /// <remarks>
    /// <para>This is <b>debug tooling, not the combat log</b>. The real combat log is Phase 9: it is
    /// player-facing, filterable, scrollable and part of the UI. This exists so that Phase 2's
    /// damage pipeline can be verified by looking at the screen rather than by reading a console,
    /// and it is compiled out of release builds entirely.</para>
    /// <para>It demonstrates the property the event architecture exists for: it reads damage,
    /// healing and death without a single line of combat code knowing it is here. Phase 9's log
    /// subscribes the same way.</para>
    /// <para>Lines are kept in a fixed-size ring buffer so a long fight cannot grow it without
    /// bound.</para>
    /// </remarks>
    public sealed class CombatEventFeed : IDisposable
    {
        private readonly EntityRegistry _entities;
        private readonly Queue<string> _lines;
        private readonly int _capacity;
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        public CombatEventFeed(IEventBus events, EntityRegistry entities, int capacity = 8)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            _entities = entities ?? throw new ArgumentNullException(nameof(entities));
            _capacity = Math.Max(1, capacity);
            _lines = new Queue<string>(_capacity);

            _subscriptions.Add(events.Subscribe<DamageDealtEvent>(OnDamage));
            _subscriptions.Add(events.Subscribe<HealAppliedEvent>(OnHeal));
            _subscriptions.Add(events.Subscribe<EntityDiedEvent>(OnDeath));
        }

        /// <summary>The buffered lines, oldest first.</summary>
        public IReadOnlyCollection<string> Lines => _lines;

        public void Clear() => _lines.Clear();

        public void Dispose()
        {
            for (int i = 0; i < _subscriptions.Count; i++)
            {
                _subscriptions[i].Dispose();
            }

            _subscriptions.Clear();
        }

        private void OnDamage(DamageDealtEvent evt)
        {
            DamageResult result = evt.Result;
            string source = Name(result.Source);
            string target = Name(result.Target);
            string label = string.IsNullOrEmpty(result.SourceLabel) ? "attack" : result.SourceLabel;

            if (result.Amount <= 0f)
            {
                Add($"{source}'s {label} does nothing to {target}.");
                return;
            }

            string critical = result.WasCritical ? " (critical)" : string.Empty;
            string overkill = result.Overkill > 0f ? $" [{result.Overkill:0} overkill]" : string.Empty;
            Add($"{source} hits {target} with {label} for {result.Applied:0} damage{critical}{overkill}.");
        }

        private void OnHeal(HealAppliedEvent evt)
        {
            HealResult result = evt.Result;
            string overheal = result.Overhealing > 0f ? $" [{result.Overhealing:0} overheal]" : string.Empty;
            string critical = result.WasCritical ? " (critical)" : string.Empty;
            Add($"{Name(result.Source)} heals {Name(result.Target)} for {result.Applied:0}{critical}{overheal}.");
        }

        private void OnDeath(EntityDiedEvent evt)
        {
            Add(evt.Killer.IsValid
                ? $"{Name(evt.Entity)} dies to {Name(evt.Killer)}."
                : $"{Name(evt.Entity)} dies.");
        }

        private void Add(string line)
        {
            if (_lines.Count >= _capacity)
            {
                _lines.Dequeue();
            }

            _lines.Enqueue(line);
        }

        /// <summary>
        /// Resolves a display name for the log. This is the one place a name is read, and it is read
        /// only to print it — never to decide anything.
        /// </summary>
        private string Name(EntityId id)
        {
            if (!id.IsValid)
            {
                return "Something";
            }

            return _entities.TryGet(id, out ISimEntity entity) ? entity.DisplayName : "Unknown";
        }
    }
}
