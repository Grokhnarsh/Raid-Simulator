using System.Collections.Generic;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using UnityEngine;

namespace EmberDepths.Gameplay.Combat
{
    /// <summary>
    /// Per-enemy aggro bookkeeping.
    ///
    /// This is what makes a five-person party a party rather than five people
    /// standing near each other: the tank's job is to hold the top of this table,
    /// the healer's job is to stay off it, and every damage dealer plays against
    /// the margin between themselves and the tank.
    ///
    /// Threat decays slowly out of combat so a leashed enemy forgets, and the
    /// switch to a new target needs a real margin so bosses do not jitter between
    /// two players whose threat is nearly tied.
    /// </summary>
    public sealed class ThreatTable
    {
        /// <summary>How much more threat a challenger needs before the enemy switches targets.</summary>
        private const float SwitchMargin = 1.1f;

        private const float DecayPerSecond = 0.02f;

        private readonly Dictionary<Actor, float> _threat = new Dictionary<Actor, float>(8);
        private readonly List<Actor> _scratch = new List<Actor>(8);

        private Actor _forcedTarget;
        private int _forcedUntilTick;

        public Actor CurrentTarget { get; private set; }

        public int Count => _threat.Count;

        public bool HasAnyThreat => _threat.Count > 0;

        public void Add(Actor source, float amount)
        {
            if (source == null || amount == 0f) return;

            _threat.TryGetValue(source, out float current);
            _threat[source] = Mathf.Max(0f, current + amount);
        }

        public float ThreatFrom(Actor source) =>
            source != null && _threat.TryGetValue(source, out float v) ? v : 0f;

        /// <summary>
        /// Forces a target regardless of the table. The taunt also normally comes
        /// with a threat bump, so aggro does not immediately snap back when it ends.
        /// </summary>
        public void ForceTarget(Actor target, float durationSeconds, int currentTick)
        {
            _forcedTarget = target;
            _forcedUntilTick = currentTick + SimClock.SecondsToTicks(durationSeconds);
        }

        public bool IsTaunted(int currentTick) => _forcedTarget != null && currentTick < _forcedUntilTick;

        public void Remove(Actor actor)
        {
            if (actor == null) return;
            _threat.Remove(actor);
            if (CurrentTarget == actor) CurrentTarget = null;
            if (_forcedTarget == actor) _forcedTarget = null;
        }

        public void Clear()
        {
            _threat.Clear();
            CurrentTarget = null;
            _forcedTarget = null;
        }

        /// <summary>
        /// Recomputes the current target. Called once per decision tick rather than
        /// on every threat change, which is both cheaper and steadier.
        /// </summary>
        public Actor Evaluate(int currentTick)
        {
            PruneDead();

            if (IsTaunted(currentTick) && _forcedTarget != null && _forcedTarget.IsAlive)
            {
                CurrentTarget = _forcedTarget;
                return CurrentTarget;
            }

            Actor best = null;
            float bestThreat = 0f;

            foreach (KeyValuePair<Actor, float> pair in _threat)
            {
                if (pair.Key == null || !pair.Key.IsAlive) continue;
                if (pair.Key.Stats.Untargetable) continue;

                if (best == null || pair.Value > bestThreat)
                {
                    best = pair.Key;
                    bestThreat = pair.Value;
                }
            }

            if (best == null)
            {
                CurrentTarget = null;
                return null;
            }

            // Stickiness: keep the existing target unless the challenger clears the
            // margin. Without this a boss visibly stutters between two damage
            // dealers trading the lead every tick.
            if (CurrentTarget != null && CurrentTarget.IsAlive && !CurrentTarget.Stats.Untargetable)
            {
                float currentThreat = ThreatFrom(CurrentTarget);
                if (bestThreat < currentThreat * SwitchMargin) return CurrentTarget;
            }

            CurrentTarget = best;
            return CurrentTarget;
        }

        /// <summary>Bleeds threat off everyone. Called once per second while out of combat.</summary>
        public void Decay(float seconds)
        {
            if (_threat.Count == 0) return;

            float factor = 1f - DecayPerSecond * seconds;
            if (factor >= 1f) return;

            _scratch.Clear();
            foreach (Actor a in _threat.Keys) _scratch.Add(a);

            for (int i = 0; i < _scratch.Count; i++)
            {
                float v = _threat[_scratch[i]] * factor;
                if (v < 1f) _threat.Remove(_scratch[i]);
                else _threat[_scratch[i]] = v;
            }
        }

        private void PruneDead()
        {
            _scratch.Clear();
            foreach (KeyValuePair<Actor, float> pair in _threat)
                if (pair.Key == null || !pair.Key.IsAlive) _scratch.Add(pair.Key);

            for (int i = 0; i < _scratch.Count; i++)
            {
                if (_scratch[i] == null) continue;
                _threat.Remove(_scratch[i]);
            }

            if (CurrentTarget != null && !CurrentTarget.IsAlive) CurrentTarget = null;
            if (_forcedTarget != null && !_forcedTarget.IsAlive) _forcedTarget = null;
        }

        /// <summary>Snapshot for the debug overlay, ordered highest first.</summary>
        public void CopySorted(List<(Actor actor, float threat)> outList)
        {
            outList.Clear();
            foreach (KeyValuePair<Actor, float> pair in _threat)
                if (pair.Key != null && pair.Key.IsAlive) outList.Add((pair.Key, pair.Value));

            outList.Sort((a, b) => b.threat.CompareTo(a.threat));
        }
    }
}
