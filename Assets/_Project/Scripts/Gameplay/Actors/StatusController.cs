using System;
using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Sim;
using UnityEngine;

namespace EmberDepths.Gameplay.Actors
{
    /// <summary>One buff or debuff currently riding on an actor.</summary>
    public sealed class ActiveStatus
    {
        public StatusEffectDefinition Definition;
        public int Stacks;
        public int ExpiresAtTick;
        public int NextPeriodicTick;

        /// <summary>Who applied it. May have died since, so always null-check.</summary>
        public Actor Source;

        /// <summary>
        /// The applier's Power at the moment of application. Snapshotting means a
        /// damage-over-time keeps the strength it was cast with even if the caster
        /// later loses a buff — the standard, and far less surprising, behaviour.
        /// </summary>
        public float SourcePower;

        public float RemainingSeconds(int currentTick) =>
            Mathf.Max(0f, SimClock.TicksToSeconds(ExpiresAtTick - currentTick));
    }

    /// <summary>
    /// Tracks every status on one actor and drives their periodic ticks.
    ///
    /// Owned by <see cref="Actor"/>; ticks from the simulation clock, never from
    /// Update, so a burn deals the same total damage regardless of frame rate.
    /// </summary>
    public sealed class StatusController
    {
        private readonly List<ActiveStatus> _active = new List<ActiveStatus>(8);
        private readonly List<ActiveStatus> _expired = new List<ActiveStatus>(4);

        private readonly Actor _owner;

        public IReadOnlyList<ActiveStatus> Active => _active;

        /// <summary>Raised whenever the set or stack count changes, so stats can be recomputed.</summary>
        public event Action Changed;

        /// <summary>Raised when a status runs out, so its OnExpire effects can fire.</summary>
        public event Action<ActiveStatus> Expired;

        public StatusController(Actor owner)
        {
            _owner = owner;
        }

        public bool Has(StatusEffectDefinition def)
        {
            if (def == null) return false;
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Definition == def) return true;
            return false;
        }

        public int StacksOf(StatusEffectDefinition def)
        {
            if (def == null) return 0;
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Definition == def) return _active[i].Stacks;
            return 0;
        }

        public ActiveStatus Find(StatusEffectDefinition def)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Definition == def) return _active[i];
            return null;
        }

        public void Apply(
            StatusEffectDefinition def,
            float durationSeconds,
            int stacks,
            Actor source,
            float sourcePower,
            int currentTick)
        {
            if (def == null || durationSeconds <= 0f) return;

            int durationTicks = SimClock.SecondsToTicks(durationSeconds);
            ActiveStatus existing = Find(def);

            if (existing != null)
            {
                existing.Stacks = Mathf.Min(def.MaxStacks, existing.Stacks + Mathf.Max(1, stacks));

                if (def.ReapplyRefreshesDuration)
                    existing.ExpiresAtTick = currentTick + durationTicks;
                else
                    existing.ExpiresAtTick = Mathf.Max(existing.ExpiresAtTick, currentTick + durationTicks);

                // Refresh the snapshot so a stronger re-application actually matters.
                if (sourcePower > existing.SourcePower)
                {
                    existing.SourcePower = sourcePower;
                    existing.Source = source;
                }

                Changed?.Invoke();
                return;
            }

            var status = new ActiveStatus
            {
                Definition = def,
                Stacks = Mathf.Clamp(stacks, 1, def.MaxStacks),
                ExpiresAtTick = currentTick + durationTicks,
                Source = source,
                SourcePower = sourcePower,
                NextPeriodicTick = def.IsPeriodic
                    ? currentTick + SimClock.SecondsToTicks(def.TickInterval)
                    : int.MaxValue
            };

            _active.Add(status);
            Changed?.Invoke();
        }

        public bool Remove(StatusEffectDefinition def)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Definition != def) continue;
                _active.RemoveAt(i);
                Changed?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes up to <paramref name="count"/> dispellable debuffs, oldest first.
        /// Returns how many actually went — the cleric's dispel reports that back
        /// so a wasted cast is visible to the player.
        /// </summary>
        public int Dispel(int count, bool debuffs = true)
        {
            int removed = 0;
            for (int i = _active.Count - 1; i >= 0 && removed < count; i--)
            {
                StatusEffectDefinition d = _active[i].Definition;
                if (d == null || !d.Dispellable || d.IsDebuff != debuffs) continue;
                _active.RemoveAt(i);
                removed++;
            }

            if (removed > 0) Changed?.Invoke();
            return removed;
        }

        public void Clear()
        {
            if (_active.Count == 0) return;
            _active.Clear();
            Changed?.Invoke();
        }

        /// <summary>
        /// Advances every status one simulation tick: fires periodic damage and
        /// healing, then retires anything that has run out.
        /// </summary>
        public void Tick(int tick)
        {
            if (_active.Count == 0) return;

            bool dirty = false;
            _expired.Clear();

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ActiveStatus s = _active[i];
                StatusEffectDefinition d = s.Definition;

                if (d == null)
                {
                    _active.RemoveAt(i);
                    dirty = true;
                    continue;
                }

                if (d.IsPeriodic && tick >= s.NextPeriodicTick)
                {
                    s.NextPeriodicTick = tick + SimClock.SecondsToTicks(d.TickInterval);

                    if (d.DamagePerTick != 0f)
                        _owner.TakePeriodicDamage(d.DamagePerTick * s.Stacks * s.SourcePower, d.TickDamageType, s.Source);

                    if (d.HealPerTick != 0f)
                        _owner.ReceivePeriodicHeal(d.HealPerTick * s.Stacks * s.SourcePower, s.Source);
                }

                if (tick < s.ExpiresAtTick) continue;

                _active.RemoveAt(i);
                _expired.Add(s);
                dirty = true;
            }

            // Expiry callbacks run after the list is stable, so an OnExpire effect
            // that applies another status cannot corrupt the iteration above.
            for (int i = 0; i < _expired.Count; i++) Expired?.Invoke(_expired[i]);

            if (dirty) Changed?.Invoke();
        }
    }
}
