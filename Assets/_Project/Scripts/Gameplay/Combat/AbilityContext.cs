using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.World;
using UnityEngine;

namespace EmberDepths.Gameplay.Combat
{
    /// <summary>
    /// The Gameplay-side implementation of <see cref="IAbilityContext"/>: the
    /// bridge that lets authored effect data act on the live simulation without
    /// the Content assembly knowing anything about it.
    ///
    /// One instance per resolved cast. Deliberately not pooled — a delayed effect
    /// holds on to its context for a second or more, and reusing the instance
    /// underneath it would silently retarget the delayed half of an ability.
    /// </summary>
    public sealed class AbilityContext : IAbilityContext
    {
        private readonly DungeonWorld _world;
        private readonly List<IActorHandle> _targets = new List<IActorHandle>(16);
        private readonly List<GridCoord> _cells = new List<GridCoord>(64);

        public Actor CasterActor { get; private set; }
        public Actor PrimaryTargetActor { get; private set; }
        public AbilityDefinition Ability { get; private set; }

        public IActorHandle Caster => CasterActor;
        public IActorHandle PrimaryTarget => PrimaryTargetActor;
        public GridCoord TargetCell { get; private set; }
        public IReadOnlyList<IActorHandle> Targets => _targets;
        public IReadOnlyList<GridCoord> Cells => _cells;
        public DeterministicRandom Rng => _world.Rng;
        public SimClock Clock => _world.Clock;

        public AbilityContext(DungeonWorld world)
        {
            _world = world;
        }

        public void Setup(AbilityDefinition ability, Actor caster, Actor primaryTarget, GridCoord targetCell,
                          List<Actor> targets, List<GridCoord> cells)
        {
            Ability = ability;
            CasterActor = caster;
            PrimaryTargetActor = primaryTarget;
            TargetCell = targetCell;

            _targets.Clear();
            if (targets != null)
                for (int i = 0; i < targets.Count; i++) _targets.Add(targets[i]);

            _cells.Clear();
            if (cells != null) _cells.AddRange(cells);
        }

        private static Actor Resolve(IActorHandle handle) => handle as Actor;

        private float ThreatMultiplier => Ability != null ? Ability.ThreatMultiplier : 1f;

        // --- verbs ---------------------------------------------------------------

        public void DealDamage(IActorHandle target, float amount, DamageType type, bool canCrit = true)
        {
            Actor t = Resolve(target);
            if (t == null) return;
            _world.Combat.DealDamage(CasterActor, t, amount, type, canCrit, ThreatMultiplier);
        }

        public void Heal(IActorHandle target, float amount, bool canCrit = true)
        {
            Actor t = Resolve(target);
            if (t == null) return;
            _world.Combat.Heal(CasterActor, t, amount, canCrit, ThreatMultiplier);
        }

        public void ApplyShield(IActorHandle target, float amount, float duration)
        {
            Actor t = Resolve(target);
            if (t == null) return;
            // Shields are authored as coefficients like healing, so scale by Power.
            t.ApplyShield(amount * (CasterActor != null ? CasterActor.Stats.Power : 1f), duration);
        }

        public void ApplyStatus(IActorHandle target, StatusEffectDefinition status, float duration, int stacks = 1)
        {
            Actor t = Resolve(target);
            if (t == null || status == null) return;

            t.Status.Apply(status, duration, stacks, CasterActor,
                CasterActor != null ? CasterActor.Stats.Power : 1f, _world.Clock.Tick);
        }

        public void RemoveStatus(IActorHandle target, StatusEffectDefinition status)
        {
            Resolve(target)?.Status.Remove(status);
        }

        public int Dispel(IActorHandle target, int count, bool debuffs)
        {
            Actor t = Resolve(target);
            return t == null ? 0 : t.Status.Dispel(count, debuffs);
        }

        public void Knockback(IActorHandle target, GridCoord from, int tiles)
        {
            Actor t = Resolve(target);
            if (t == null || tiles <= 0) return;
            if ((t.Flags & ActorFlags.Unmovable) != 0) return;

            // When the source and the target share a cell there is no direction to
            // push along; fall back to the target's own facing so the knockback
            // still happens rather than silently doing nothing.
            IsoDirection dir = IsoDirectionExtensions.FromGridStep(from, t.Cell, t.Facing);
            GridCoord step = GridCoord.Step(dir);

            // Walk the push out one tile at a time and stop at the first wall, so
            // a knockback into a corner pins the target instead of teleporting it
            // through the geometry.
            GridCoord landing = t.Cell;
            for (int i = 0; i < tiles; i++)
            {
                GridCoord next = landing + step;
                if (!_world.Map.IsWalkable(next)) break;
                landing = next;
            }

            if (landing != t.Cell) t.TeleportTo(landing);
        }

        public void Displace(IActorHandle target, GridCoord destination)
        {
            Resolve(target)?.TeleportTo(destination);
        }

        public void AddThreat(IActorHandle target, IActorHandle towards, float amount)
        {
            Actor enemy = Resolve(target);
            Actor source = Resolve(towards);
            if (enemy?.Threat == null || source == null) return;
            enemy.Threat.Add(source, amount);
        }

        public void Taunt(IActorHandle enemy, IActorHandle taunter, float duration)
        {
            Actor e = Resolve(enemy);
            Actor t = Resolve(taunter);
            if (e?.Threat == null || t == null) return;
            if ((e.Flags & ActorFlags.TauntImmune) != 0) return;

            e.Threat.ForceTarget(t, duration, _world.Clock.Tick);
        }

        public void SpawnHazard(HazardDefinition hazard, GridCoord cell, int radius, float duration)
        {
            _world.Hazards.Spawn(hazard, cell, radius, duration, CasterActor);
        }

        public void Summon(EnemyDefinition enemy, GridCoord near, int count)
        {
            if (enemy == null) return;
            for (int i = 0; i < count; i++)
            {
                if (!_world.Map.TryFindFreeCellNear(near, 5, out GridCoord cell)) break;
                _world.SpawnEnemy(enemy, cell);
            }
        }

        public void Telegraph(TargetShape shape, GridCoord origin, GridCoord target, float leadTime)
        {
            _world.Telegraphs.Show(CasterActor, shape, origin, target, leadTime);
        }

        public void PlayVfx(string vfxKey, GridCoord at) => _world.PlayVfx(vfxKey, at);

        public void Log(string message) => Debug.Log($"[Ability] {message}");
    }
}
