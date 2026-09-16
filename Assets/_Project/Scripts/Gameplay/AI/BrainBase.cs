using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.Combat;
using EmberDepths.Gameplay.World;
using UnityEngine;

namespace EmberDepths.Gameplay.AI
{
    /// <summary>Anything that makes decisions for an actor on the simulation clock.</summary>
    public interface IBrain
    {
        Actor Owner { get; }
        bool Enabled { get; set; }
        void Think(int tick);
    }

    /// <summary>
    /// Shared decision-making for every AI in the game: how to walk somewhere, how
    /// to pick an ability, and — the part that makes fights readable — how to get
    /// out of a fire.
    ///
    /// Brains run on a decision interval rather than every tick. Twenty enemies
    /// re-pathing twenty times a second is both wasteful and, oddly, worse to
    /// play against: perfect reaction time reads as cheating. A few decisions a
    /// second looks deliberate.
    /// </summary>
    public abstract class BrainBase : IBrain
    {
        protected readonly List<GridCoord> Path = new List<GridCoord>(48);

        public Actor Owner { get; }
        public bool Enabled { get; set; } = true;

        protected DungeonWorld World => Owner.World;

        /// <summary>Ticks between decisions. Movement still runs every tick.</summary>
        protected int DecisionInterval = 4;

        private int _nextDecisionTick;

        protected BrainBase(Actor owner)
        {
            Owner = owner;
        }

        public void Think(int tick)
        {
            if (!Enabled || Owner == null || !Owner.IsAlive) return;
            if (tick < _nextDecisionTick) return;

            _nextDecisionTick = tick + Mathf.Max(1, DecisionInterval);
            Decide(tick);
        }

        protected abstract void Decide(int tick);

        // --- movement helpers -------------------------------------------------------

        /// <summary>Paths towards a cell, stopping when adjacent if asked.</summary>
        protected bool MoveTowards(GridCoord destination, bool stopAdjacent)
        {
            if (!Owner.Stats.CanMove) return false;

            if (World.Pathfinder.TryFindPath(World.Map, Owner.Cell, destination, Path, stopAdjacent))
            {
                Owner.Motor.SetPath(Path);
                return true;
            }

            Owner.Motor.Stop();
            return false;
        }

        /// <summary>Steps directly away from a cell. Used for kiting and for backing out of melee.</summary>
        protected bool MoveAwayFrom(GridCoord threat, int desiredDistance)
        {
            if (!Owner.Stats.CanMove) return false;

            GridCoord best = Owner.Cell;
            int bestScore = GridCoord.Chebyshev(Owner.Cell, threat);

            for (int i = 0; i < GridCoord.AllNeighbours.Length; i++)
            {
                GridCoord candidate = Owner.Cell + GridCoord.AllNeighbours[i];
                if (!World.Map.IsFree(candidate)) continue;
                if (World.Hazards.IsDangerousFor(Owner, candidate)) continue;

                int d = GridCoord.Chebyshev(candidate, threat);
                if (d <= bestScore || d > desiredDistance + 2) continue;

                best = candidate;
                bestScore = d;
            }

            if (best == Owner.Cell) return false;

            Path.Clear();
            Path.Add(best);
            Owner.Motor.SetPath(Path);
            return true;
        }

        /// <summary>
        /// Steps out of a hazard or telegraph the actor is standing in.
        ///
        /// <paramref name="willingness"/> below 1 means the actor sometimes fails
        /// to react. That is intentional for enemies: an AI that dodges perfectly
        /// every time makes its own telegraphs pointless.
        /// </summary>
        protected bool TryStepOutOfDanger(float willingness)
        {
            if (!Owner.Stats.CanMove) return false;

            bool inDanger = World.Hazards.IsDangerousFor(Owner, Owner.Cell)
                            || World.Telegraphs.IsThreatened(Owner.Cell);

            if (!inDanger) return false;
            if (willingness < 1f && !World.Rng.Chance(willingness)) return false;

            GridCoord safest = Owner.Cell;
            bool found = false;

            // Search two rings out: one step is often still inside a large pool.
            for (int radius = 1; radius <= 2 && !found; radius++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue;

                        var candidate = new GridCoord(Owner.Cell.X + dx, Owner.Cell.Y + dy);
                        if (!World.Map.IsFree(candidate)) continue;
                        if (World.Hazards.IsDangerousFor(Owner, candidate)) continue;
                        if (World.Telegraphs.IsThreatened(candidate)) continue;

                        safest = candidate;
                        found = true;
                        break;
                    }
                    if (found) break;
                }
            }

            if (!found) return false;
            return MoveTowards(safest, stopAdjacent: false);
        }

        // --- ability helpers ---------------------------------------------------------

        /// <summary>
        /// Picks the best ability that is ready, in range and worth casting, then
        /// casts it. Returns true if something was cast.
        /// </summary>
        protected bool TryUseBestAbility(IReadOnlyList<AbilityDefinition> abilities, Actor target, int tick)
        {
            if (abilities == null || abilities.Count == 0 || !Owner.Stats.CanCast) return false;

            AbilityDefinition best = null;
            float bestPriority = float.MinValue;

            for (int i = 0; i < abilities.Count; i++)
            {
                AbilityDefinition ability = abilities[i];
                if (ability == null) continue;
                if (!IsWorthCasting(ability, target, tick)) continue;
                if (ability.AiPriority <= bestPriority) continue;

                best = ability;
                bestPriority = ability.AiPriority;
            }

            if (best == null) return false;

            GridCoord aim = best.TargetKind == TargetKind.Self ? Owner.Cell
                          : target != null ? target.Cell
                          : Owner.Cell;

            return Owner.Abilities.TryCast(best, target, aim, tick) == CastRefusal.None;
        }

        protected bool IsWorthCasting(AbilityDefinition ability, Actor target, int tick)
        {
            if (ability.AiUseBelowSelfHealth < 1f && Owner.HealthFraction > ability.AiUseBelowSelfHealth)
                return false;

            if (ability.AiUseBelowTargetHealth < 1f &&
                (target == null || target.HealthFraction > ability.AiUseBelowTargetHealth))
                return false;

            GridCoord aim = ability.TargetKind == TargetKind.Self ? Owner.Cell
                          : target != null ? target.Cell
                          : Owner.Cell;

            if (Owner.Abilities.CanCast(ability, target, aim, tick) != CastRefusal.None) return false;

            // Do not burn a big area cooldown on a single straggler.
            if (ability.AiMinTargets > 1)
            {
                int count = World.AbilityRunner.CountTargets(ability, Owner, target, aim);
                if (count < ability.AiMinTargets) return false;
            }

            return true;
        }

        protected bool TryBasicAttack(Actor target, int tick)
        {
            AbilityDefinition basic = Owner.Abilities.BasicAttack;
            if (basic == null || target == null) return false;
            if (!Owner.Abilities.IsReady(basic, tick)) return false;

            return Owner.Abilities.TryCast(basic, target, target.Cell, tick) == CastRefusal.None;
        }
    }
}
