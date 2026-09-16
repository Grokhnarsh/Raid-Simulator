using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Gameplay.Actors;
using UnityEngine;

namespace EmberDepths.Gameplay.AI
{
    /// <summary>
    /// Standard hostile behaviour: notice the party, hold a preferred distance,
    /// use whatever is off cooldown, and go home if dragged too far.
    ///
    /// Target selection goes through the threat table, not through "nearest
    /// player". That single choice is what makes the tank's job real — without
    /// it, taunts and threat abilities would be decoration.
    /// </summary>
    public class EnemyBrain : BrainBase
    {
        protected enum State { Idle, Combat, Leashing }

        protected State CurrentState = State.Idle;

        private readonly EnemyAiProfile _profile;
        private int _nextWanderTick;

        public EnemyBrain(Actor owner) : base(owner)
        {
            _profile = owner.EnemyDefinition != null ? owner.EnemyDefinition.Ai : EnemyAiProfile.Melee;
            DecisionInterval = Mathf.Max(1, _profile.RepathIntervalTicks);
        }

        protected EnemyAiProfile Profile => _profile;

        protected override void Decide(int tick)
        {
            switch (CurrentState)
            {
                case State.Idle: DecideIdle(tick); break;
                case State.Combat: DecideCombat(tick); break;
                case State.Leashing: DecideLeashing(tick); break;
            }
        }

        // --- idle ---------------------------------------------------------------

        private void DecideIdle(int tick)
        {
            Actor spotted = FindTargetInAggroRange();
            if (spotted != null)
            {
                Engage(spotted);
                return;
            }

            if (!_profile.WandersWhileIdle || tick < _nextWanderTick) return;

            // Idle drift keeps a room from looking like a set of statues, but it
            // must stay near the spawn or packs slowly dissolve across the map.
            _nextWanderTick = tick + World.Rng.Range(40, 90);

            var offset = new GridCoord(World.Rng.Range(-2, 3), World.Rng.Range(-2, 3));
            GridCoord wander = Owner.SpawnCell + offset;
            if (World.Map.IsFree(wander)) MoveTowards(wander, stopAdjacent: false);
        }

        protected Actor FindTargetInAggroRange()
        {
            Actor nearest = World.Actors.NearestEnemyOf(Owner, _profile.AggroRadius);
            if (nearest == null) return null;
            if (!World.Map.HasLineOfSight(Owner.Cell, nearest.Cell)) return null;
            return nearest;
        }

        protected void Engage(Actor target)
        {
            CurrentState = State.Combat;
            // A small seed of threat so the first evaluation has something to pick.
            Owner.Threat?.Add(target, 1f);
        }

        // --- combat -------------------------------------------------------------

        protected virtual void DecideCombat(int tick)
        {
            if (_profile.LeashRadius > 0 &&
                GridCoord.Chebyshev(Owner.Cell, Owner.SpawnCell) > _profile.LeashRadius)
            {
                CurrentState = State.Leashing;
                return;
            }

            Actor target = Owner.Threat != null ? Owner.Threat.Evaluate(tick) : null;
            if (target == null) target = FindTargetInAggroRange();

            if (target == null || !target.IsAlive)
            {
                CurrentState = State.Idle;
                Owner.Motor.Stop();
                Owner.Threat?.Clear();
                return;
            }

            Owner.FaceTowards(target.Cell);

            if (TryStepOutOfDanger(_profile.HazardAvoidance)) return;
            if (Owner.Abilities.IsCasting) return;

            if (TryUseBestAbility(Owner.EnemyDefinition != null ? Owner.EnemyDefinition.Abilities : null, target, tick))
                return;

            int distance = GridCoord.Chebyshev(Owner.Cell, target.Cell);

            if (distance <= _profile.PreferredRange)
            {
                if (TryBasicAttack(target, tick)) return;

                // Kiters open the gap again once they have taken their shot.
                if (_profile.Kites && distance < _profile.PreferredRange)
                    MoveAwayFrom(target.Cell, _profile.PreferredRange);
                else
                    Owner.Motor.Stop();

                return;
            }

            Approach(target);
        }

        protected void Approach(Actor target)
        {
            if (_profile.PreferredRange <= 1)
            {
                // Melee aims at a free tile beside the target; the target's own
                // cell is occupied and pathing to it would always fail.
                if (World.TryFindMeleeSlot(Owner, target, out GridCoord slot))
                {
                    MoveTowards(slot, stopAdjacent: false);
                    return;
                }
            }

            MoveTowards(target.Cell, stopAdjacent: true);
        }

        // --- leashing -----------------------------------------------------------

        private void DecideLeashing(int tick)
        {
            if (GridCoord.Chebyshev(Owner.Cell, Owner.SpawnCell) <= 1)
            {
                CurrentState = State.Idle;
                Owner.Threat?.Clear();
                Owner.Status.Clear();

                // Full reset on leash. Anything else invites pulling a pack,
                // running away, and coming back to a half-dead room.
                Owner.ReceiveHeal(Owner.MaxHealth, null);
                return;
            }

            MoveTowards(Owner.SpawnCell, stopAdjacent: false);
        }
    }
}
