using System;
using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using UnityEngine;

namespace EmberDepths.Gameplay.AI
{
    /// <summary>
    /// Runs a boss fight: phase transitions on health thresholds, a timed special
    /// rotation on top of the normal attack loop, and an enrage that eventually
    /// ends a fight the party is not winning.
    ///
    /// A boss is still an <see cref="EnemyBrain"/> underneath. It walks, holds
    /// threat and gets knocked around by the same code as a trash mob; the only
    /// thing added here is the script that makes the fight a fight rather than a
    /// large health bar.
    /// </summary>
    public sealed class BossBrain : EnemyBrain
    {
        private readonly BossDefinition _definition;
        private readonly List<float> _weights = new List<float>(8);

        private int _phaseIndex = -1;
        private int _combatStartTick = -1;
        private int _nextSpecialTick;
        private int _nextEnrageStackTick;
        private bool _enraged;

        public BossPhase CurrentPhase => _definition.PhaseAt(_phaseIndex);
        public int PhaseIndex => _phaseIndex;
        public bool IsEnraged => _enraged;

        /// <summary>Raised when a phase begins, so the UI can show its banner.</summary>
        public event Action<BossPhase, int> PhaseChanged;

        public event Action Enraged;

        public BossBrain(Actor owner, BossDefinition definition) : base(owner)
        {
            _definition = definition;
            DecisionInterval = 3;
        }

        protected override void DecideCombat(int tick)
        {
            if (_combatStartTick < 0) _combatStartTick = tick;

            UpdatePhase(tick);
            UpdateEnrage(tick);

            Actor target = Owner.Threat != null ? Owner.Threat.Evaluate(tick) : null;
            if (target == null) target = FindTargetInAggroRange();

            if (target == null || !target.IsAlive)
            {
                // A boss does not leash and does not reset. It waits, which keeps
                // a wipe recovery honest: the party comes back to a boss at the
                // health they left it.
                Owner.Motor.Stop();
                return;
            }

            Owner.FaceTowards(target.Cell);

            if (Owner.Abilities.IsCasting) return;

            if (tick >= _nextSpecialTick && TryCastPhaseAbility(target, tick)) return;

            int distance = GridCoord.Chebyshev(Owner.Cell, target.Cell);
            if (distance <= Profile.PreferredRange)
            {
                if (TryBasicAttack(target, tick)) return;
                Owner.Motor.Stop();
                return;
            }

            Approach(target);
        }

        private void UpdatePhase(int tick)
        {
            int desired = _definition.PhaseIndexFor(Owner.HealthFraction);
            if (desired <= _phaseIndex) return;

            // Phases only ever advance. Healing a boss back above a threshold must
            // not replay its transition script.
            BossPhase previous = CurrentPhase;
            if (previous?.Aura != null) Owner.Status.Remove(previous.Aura);

            _phaseIndex = desired;
            BossPhase phase = CurrentPhase;
            if (phase == null) return;

            // "For the rest of the fight", expressed as a day. float.MaxValue here
            // would overflow SecondsToTicks and produce an already-expired status.
            const float PhaseAuraDuration = 86400f;
            if (phase.Aura != null)
                Owner.Status.Apply(phase.Aura, PhaseAuraDuration, 1, Owner, Owner.Stats.Power, tick);

            if (phase.OnEnter != null && phase.OnEnter.Count > 0)
                World.AbilityRunner.RunEffects(phase.OnEnter, Owner, Owner, Owner.Cell);

            _nextSpecialTick = tick + SimClock.SecondsToTicks(phase.AbilityInterval);

            Owner.Abilities.CancelCast();
            PhaseChanged?.Invoke(phase, _phaseIndex);
        }

        private void UpdateEnrage(int tick)
        {
            if (_definition.EnrageAfterSeconds <= 0f || _definition.EnrageStatus == null) return;

            int enrageAtTick = _combatStartTick + SimClock.SecondsToTicks(_definition.EnrageAfterSeconds);
            if (tick < enrageAtTick) return;

            if (!_enraged)
            {
                _enraged = true;
                Enraged?.Invoke();
            }

            if (tick < _nextEnrageStackTick) return;
            _nextEnrageStackTick = tick + SimClock.TicksPerSecond;

            // One stack per second, forever. The party either finishes the fight
            // or it does not.
            Owner.Status.Apply(_definition.EnrageStatus, 3600f, 1, Owner, Owner.Stats.Power, tick);
        }

        private bool TryCastPhaseAbility(Actor target, int tick)
        {
            BossPhase phase = CurrentPhase;
            if (phase == null || phase.Abilities == null || phase.Abilities.Count == 0) return false;

            // Weighted random rather than strict priority, so the same fight does
            // not play out in the identical order every attempt.
            _weights.Clear();
            for (int i = 0; i < phase.Abilities.Count; i++)
            {
                AbilityDefinition a = phase.Abilities[i];
                _weights.Add(a != null && IsWorthCasting(a, target, tick) ? Mathf.Max(0.01f, a.AiPriority) : 0f);
            }

            int index = World.Rng.PickWeighted(_weights);
            if (index < 0) return false;

            AbilityDefinition ability = phase.Abilities[index];

            GridCoord aim = ability.TargetKind switch
            {
                TargetKind.Self => Owner.Cell,
                TargetKind.Ground => PickGroundTarget(target),
                _ => target.Cell
            };

            if (Owner.Abilities.TryCast(ability, target, aim, tick) != Combat.CastRefusal.None) return false;

            _nextSpecialTick = tick + SimClock.SecondsToTicks(phase.AbilityInterval);
            return true;
        }

        /// <summary>
        /// Ground-targeted boss abilities aim at a random party member rather than
        /// the tank. Otherwise every meteor lands on the one player already
        /// standing in melee, and the mechanic never asks anything of the group.
        /// </summary>
        private GridCoord PickGroundTarget(Actor fallback)
        {
            IReadOnlyList<Actor> party = World.Actors.Party;
            if (party.Count == 0) return fallback.Cell;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                Actor candidate = party[World.Rng.Range(0, party.Count)];
                if (candidate != null && candidate.IsAlive) return candidate.Cell;
            }

            return fallback.Cell;
        }
    }
}
