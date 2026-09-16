using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.World;

namespace EmberDepths.Gameplay.Combat
{
    /// <summary>
    /// Turns an <see cref="AbilityDefinition"/> into things that happen.
    ///
    /// Resolution is: work out which cells the shape covers, work out who is
    /// standing in them, then run the authored effects against that set. Every
    /// caster in the game — the player's action bar, a trash mob, the boss's
    /// phase script — arrives here, which is why an ability behaves identically
    /// no matter who fires it.
    /// </summary>
    public sealed class AbilityRunner
    {
        private readonly DungeonWorld _world;
        private readonly List<GridCoord> _cells = new List<GridCoord>(128);
        private readonly List<Actor> _targets = new List<Actor>(16);

        public AbilityRunner(DungeonWorld world)
        {
            _world = world;
        }

        public void Execute(AbilityDefinition ability, Actor caster, Actor primaryTarget, GridCoord targetCell)
        {
            if (ability == null || caster == null) return;

            CollectCells(ability, caster, targetCell, _cells);
            CollectTargets(ability, caster, primaryTarget, targetCell, _cells, _targets);

            var ctx = new AbilityContext(_world);
            ctx.Setup(ability, caster, primaryTarget, targetCell, _targets, _cells);

            RunEffectList(ability.Effects, ctx);

            if (!string.IsNullOrEmpty(ability.ImpactVfxKey))
                _world.PlayVfx(ability.ImpactVfxKey, targetCell);

            _world.NotifyAbilityResolved(ability, caster, _targets.Count);
        }

        /// <summary>
        /// Runs a bare effect list with no ability wrapper. Used by status expiry
        /// payloads and boss phase-transition scripts, which have effects but no
        /// cast, cooldown or shape of their own.
        /// </summary>
        public void RunEffects(List<AbilityEffect> effects, Actor caster, Actor target, GridCoord cell)
        {
            if (effects == null || effects.Count == 0 || caster == null) return;

            _cells.Clear();
            _cells.Add(cell);

            _targets.Clear();
            if (target != null && target.IsAlive) _targets.Add(target);

            var ctx = new AbilityContext(_world);
            ctx.Setup(null, caster, target, cell, _targets, _cells);
            RunEffectList(effects, ctx);
        }

        private void RunEffectList(List<AbilityEffect> effects, AbilityContext ctx)
        {
            if (effects == null) return;

            for (int i = 0; i < effects.Count; i++)
            {
                AbilityEffect effect = effects[i];
                if (effect == null) continue;

                if (effect.Probability < 1f && !_world.Rng.Chance(effect.Probability)) continue;

                if (effect.Delay <= 0f)
                {
                    effect.Apply(ctx);
                    continue;
                }

                // Delayed effects keep their own captured context, so a staggered
                // three-hit ability still resolves against the set of targets the
                // cast was aimed at rather than whoever wandered in afterwards.
                AbilityEffect captured = effect;
                _world.Schedule(SimClock.SecondsToTicks(effect.Delay), () =>
                {
                    if (ctx.CasterActor == null || !ctx.CasterActor.IsAlive) return;
                    captured.Apply(ctx);
                });
            }
        }

        private void CollectCells(AbilityDefinition ability, Actor caster, GridCoord targetCell, List<GridCoord> outCells)
        {
            outCells.Clear();

            if (ability.Shape.Kind == ShapeKind.Global) return;

            ability.Shape.CollectCells(caster.Cell, targetCell, outCells);

            for (int i = outCells.Count - 1; i >= 0; i--)
            {
                GridCoord c = outCells[i];

                if (!_world.Map.InBounds(c))
                {
                    outCells.RemoveAt(i);
                    continue;
                }

                // A cone that clips a pillar should stop at the pillar rather than
                // wrapping around it — otherwise line of sight stops meaning
                // anything for area abilities.
                if (ability.ShapeRespectsLineOfSight && !_world.Map.HasLineOfSight(caster.Cell, c))
                    outCells.RemoveAt(i);
            }
        }

        private void CollectTargets(
            AbilityDefinition ability,
            Actor caster,
            Actor primaryTarget,
            GridCoord targetCell,
            List<GridCoord> cells,
            List<Actor> outTargets)
        {
            outTargets.Clear();

            if (ability.TargetKind == TargetKind.Self)
            {
                outTargets.Add(caster);
                return;
            }

            if (ability.Shape.Kind == ShapeKind.Global)
            {
                IReadOnlyList<Actor> all = _world.Actors.All;
                for (int i = 0; i < all.Count; i++)
                    if (Matches(ability, caster, all[i])) outTargets.Add(all[i]);
                return;
            }

            if (ability.Shape.Kind == ShapeKind.Single)
            {
                Actor single = primaryTarget ?? _world.Actors.AtCell(targetCell, _world.Map);
                if (Matches(ability, caster, single)) outTargets.Add(single);
                return;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                Actor occupant = _world.Actors.AtCell(cells[i], _world.Map);
                if (occupant == null || outTargets.Contains(occupant)) continue;
                if (Matches(ability, caster, occupant)) outTargets.Add(occupant);
            }
        }

        private static bool Matches(AbilityDefinition ability, Actor caster, Actor candidate)
        {
            if (candidate == null || !candidate.IsAlive) return false;
            if (candidate.Stats.Untargetable && ability.AffectsFaction == AffectsFaction.Enemies) return false;

            return ability.AffectsFaction switch
            {
                AffectsFaction.Enemies => caster.IsHostileTo(candidate),
                AffectsFaction.Allies => candidate.Faction == caster.Faction,
                _ => true
            };
        }

        /// <summary>
        /// How many valid targets an ability would hit right now. The AI uses this
        /// to honour <see cref="AbilityDefinition.AiMinTargets"/> so enemies do not
        /// waste a big cooldown on one straggler.
        /// </summary>
        public int CountTargets(AbilityDefinition ability, Actor caster, Actor primaryTarget, GridCoord targetCell)
        {
            if (ability == null || caster == null) return 0;

            CollectCells(ability, caster, targetCell, _cells);
            CollectTargets(ability, caster, primaryTarget, targetCell, _cells, _targets);
            return _targets.Count;
        }
    }
}
