using System;
using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.AI;
using EmberDepths.Gameplay.Combat;
using EmberDepths.Gameplay.Presentation;
using UnityEngine;

namespace EmberDepths.Gameplay.World
{
    /// <summary>
    /// The live dungeon: its map, its actors, and every system that acts on them.
    ///
    /// Deliberately a plain C# object rather than a MonoBehaviour singleton. One
    /// <see cref="DungeonRunner"/> component owns one of these and drives its
    /// tick; nothing reaches it through a static. That is what allows a second
    /// world to exist at the same time — for a headless balance simulation, for
    /// an editor preview, or eventually for a server running several parties.
    /// </summary>
    public sealed class DungeonWorld
    {
        public GridMap Map { get; }
        public SimClock Clock { get; }
        public DeterministicRandom Rng { get; }
        public ActorRegistry Actors { get; }
        public CombatSystem Combat { get; }
        public HazardField Hazards { get; }
        public TelegraphSystem Telegraphs { get; }
        public AbilityRunner AbilityRunner { get; }
        public AStar Pathfinder { get; }

        public BiomeDefinition Biome { get; set; }
        public DungeonLayout Layout { get; set; }

        /// <summary>
        /// The actor companions follow out of combat. Normally the locally
        /// controlled slot; reassigned when that slot dies so the party does not
        /// stand still around a corpse.
        /// </summary>
        public Actor PartyLeader { get; set; }

        public Transform SceneRoot { get; }
        public Transform ActorRoot { get; }

        /// <summary>Every brain that wants a decision tick. Populated on spawn.</summary>
        private readonly List<IBrain> _brains = new List<IBrain>(64);

        private readonly List<Actor> _tickBuffer = new List<Actor>(64);
        private readonly List<IBrain> _brainBuffer = new List<IBrain>(64);
        private readonly List<(int tick, Action action)> _scheduled = new List<(int, Action)>(32);
        private readonly List<Action> _dueScratch = new List<Action>(8);

        public event Action<Actor> ActorSpawned;
        public event Action<Actor, Actor> ActorDied;
        public event Action<string, GridCoord> VfxRequested;
        public event Action<AbilityDefinition, Actor, int> AbilityResolved;

        public DungeonWorld(int width, int height, int seed, Transform sceneRoot)
        {
            Map = new GridMap(width, height);
            Clock = new SimClock();
            Rng = new DeterministicRandom(seed);
            Actors = new ActorRegistry();

            SceneRoot = sceneRoot;

            var actorRoot = new GameObject("Actors");
            actorRoot.transform.SetParent(sceneRoot, false);
            ActorRoot = actorRoot.transform;

            Combat = new CombatSystem(this);
            Hazards = new HazardField(this);
            Telegraphs = new TelegraphSystem(this);
            AbilityRunner = new AbilityRunner(this);
            Pathfinder = new AStar();
        }

        // --- tick ------------------------------------------------------------------

        /// <summary>
        /// One simulation step. Order matters and is chosen so that a player never
        /// sees a stale frame of a system they just affected:
        /// scheduled payloads, then actors, then brains, then the world's own
        /// hazards and warnings.
        /// </summary>
        public void Tick(int tick)
        {
            RunScheduled(tick);

            // Copy first: an actor can die (and unregister) inside its own tick.
            _tickBuffer.Clear();
            _tickBuffer.AddRange(Actors.All);
            for (int i = 0; i < _tickBuffer.Count; i++)
            {
                Actor a = _tickBuffer[i];
                if (a != null) a.Tick(tick);
            }

            // Copied for the same reason as the actors above: a brain's decision
            // can kill something, and a death unregisters that actor's brain from
            // this very list.
            _brainBuffer.Clear();
            _brainBuffer.AddRange(_brains);
            for (int i = 0; i < _brainBuffer.Count; i++)
            {
                IBrain brain = _brainBuffer[i];
                if (brain != null && brain.Owner != null && brain.Owner.IsAlive) brain.Think(tick);
            }

            Hazards.Tick(tick);
            Telegraphs.Tick(tick);
        }

        /// <summary>Runs <paramref name="action"/> after a delay, on the simulation clock.</summary>
        public void Schedule(int ticksFromNow, Action action)
        {
            if (action == null) return;
            _scheduled.Add((Clock.Tick + Mathf.Max(1, ticksFromNow), action));
        }

        private void RunScheduled(int tick)
        {
            if (_scheduled.Count == 0) return;

            _dueScratch.Clear();
            for (int i = _scheduled.Count - 1; i >= 0; i--)
            {
                if (_scheduled[i].tick > tick) continue;
                _dueScratch.Add(_scheduled[i].action);
                _scheduled.RemoveAt(i);
            }

            // Invoked after the list is stable so a payload may schedule another.
            for (int i = 0; i < _dueScratch.Count; i++) _dueScratch[i]?.Invoke();
        }

        // --- spawning ----------------------------------------------------------------

        public Actor SpawnPartyMember(ClassDefinition classDef, string displayName, GridCoord cell, bool localPlayer)
        {
            var go = new GameObject($"Party_{displayName}");
            go.transform.SetParent(ActorRoot, false);

            var actor = go.AddComponent<Actor>();
            actor.InitialiseAsPartyMember(this, Actors.AllocateId(), classDef, displayName, cell);

            AttachView(actor);
            Actors.Register(actor);

            // Every party slot gets a companion brain. The locally controlled slot
            // keeps it too but leaves it disabled, so handing control back and
            // forth — on death, or when a human joins later — needs no re-wiring.
            var brain = new CompanionBrain(actor) { Enabled = !localPlayer };
            _brains.Add(brain);
            actor.Died += OnActorDiedInternal;

            ActorSpawned?.Invoke(actor);
            return actor;
        }

        public Actor SpawnEnemy(EnemyDefinition def, GridCoord cell)
        {
            if (def == null) return null;

            var go = new GameObject($"Enemy_{def.name}");
            go.transform.SetParent(ActorRoot, false);

            var actor = go.AddComponent<Actor>();
            actor.InitialiseAsEnemy(this, Actors.AllocateId(), def, cell);

            AttachView(actor, def.VisualScale);
            Actors.Register(actor);

            IBrain brain = def.Rank == EnemyRank.Boss && def.BossProfile != null
                ? new BossBrain(actor, def.BossProfile)
                : new EnemyBrain(actor);

            _brains.Add(brain);
            actor.Died += OnActorDiedInternal;

            ActorSpawned?.Invoke(actor);
            return actor;
        }

        private void AttachView(Actor actor, float scale = 1f)
        {
            var viewGo = new GameObject("View");
            viewGo.transform.SetParent(actor.transform, false);

            var view = viewGo.AddComponent<ActorView>();
            view.Bind(actor, scale);
        }

        private void OnActorDiedInternal(Actor actor, Actor killer)
        {
            ActorDied?.Invoke(actor, killer);
        }

        public void NotifyActorDied(Actor actor, Actor killer)
        {
            // The corpse stays in the scene for its death animation; only the
            // simulation-facing registration goes away immediately, so nothing can
            // target or path around a dead body.
            Actors.Unregister(actor);

            for (int i = _brains.Count - 1; i >= 0; i--)
                if (_brains[i]?.Owner == actor) _brains.RemoveAt(i);
        }

        public void NotifyAbilityResolved(AbilityDefinition ability, Actor caster, int targetCount) =>
            AbilityResolved?.Invoke(ability, caster, targetCount);

        public void PlayVfx(string key, GridCoord at)
        {
            if (string.IsNullOrEmpty(key)) return;
            VfxRequested?.Invoke(key, at);
        }

        // --- queries -----------------------------------------------------------------

        /// <summary>
        /// A cell adjacent to <paramref name="target"/> that <paramref name="mover"/>
        /// can stand on. Melee AI aims at one of these rather than at the target's
        /// own occupied cell.
        /// </summary>
        public bool TryFindMeleeSlot(Actor mover, Actor target, out GridCoord slot)
        {
            slot = target.Cell;
            GridCoord best = default;
            int bestDistance = int.MaxValue;
            bool found = false;

            for (int i = 0; i < GridCoord.AllNeighbours.Length; i++)
            {
                GridCoord candidate = target.Cell + GridCoord.AllNeighbours[i];
                if (!Map.IsWalkable(candidate)) continue;
                if (Map.TryGetOccupant(candidate, out int occupant) && occupant != mover.Id) continue;

                int d = GridCoord.Chebyshev(mover.Cell, candidate);

                // Prefer a slot that is not on fire, even if it means walking further.
                if (Hazards.IsDangerousFor(mover, candidate)) d += 6;

                if (d >= bestDistance) continue;
                best = candidate;
                bestDistance = d;
                found = true;
            }

            if (found) slot = best;
            return found;
        }

        public void RegisterBrain(IBrain brain)
        {
            if (brain != null && !_brains.Contains(brain)) _brains.Add(brain);
        }

        public IReadOnlyList<IBrain> Brains => _brains;

        public void Clear()
        {
            Telegraphs.Clear();
            Hazards.Clear();
            _brains.Clear();
            _scheduled.Clear();
            Actors.Clear();

            if (ActorRoot != null)
                for (int i = ActorRoot.childCount - 1; i >= 0; i--)
                    Presentation.ViewObjects.Destroy(ActorRoot.GetChild(i).gameObject);
        }
    }
}
