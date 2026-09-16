using System;
using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.AI;
using EmberDepths.Gameplay.Items;
using EmberDepths.Gameplay.Presentation;
using EmberDepths.Gameplay.World;
using UnityEngine;

namespace EmberDepths.Gameplay.Run
{
    public enum RunState
    {
        NotStarted = 0,
        Running = 1,
        Cleared = 2,
        Wiped = 3
    }

    /// <summary>
    /// Owns one run of one dungeon: generates it, populates it, and drives the
    /// simulation clock.
    ///
    /// This is the only MonoBehaviour in the gameplay layer that runs an Update
    /// loop. Everything else advances because this called it, which makes the
    /// order of operations in a frame something you can read in one place rather
    /// than infer from script execution order.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DungeonRunner : MonoBehaviour
    {
        [Header("Content")]
        public DungeonDefinition Dungeon;
        public PartyRoster Roster;

        [Header("Run")]
        [Tooltip("Overrides the dungeon's seed when non-zero. Handy for reproducing a bug report.")]
        public int SeedOverride;

        [Tooltip("Start the run automatically on Start. Turn off when a menu drives it.")]
        public bool AutoStart = true;

        public DungeonWorld World { get; private set; }
        public EncounterDirector Director { get; private set; }
        public DungeonBuilder Builder { get; private set; }
        public DungeonLayout Layout => World?.Layout;

        /// <summary>Loot on the floor, and what happens when someone walks over it.</summary>
        public LootSystem Loot { get; private set; }

        /// <summary>The party's shared stash and ember purse. Survives for the whole run.</summary>
        public Inventory Inventory { get; private set; }

        /// <summary>The slot the human is driving.</summary>
        public Actor LocalPlayer { get; private set; }

        public RunState State { get; private set; } = RunState.NotStarted;
        public int Seed { get; private set; }

        public event Action<DungeonWorld> RunStarted;
        public event Action<RunState> RunEnded;
        public event Action<Actor> LocalPlayerChanged;

        private readonly List<Actor> _party = new List<Actor>(8);
        public IReadOnlyList<Actor> Party => _party;

        private Transform _root;

        private void Start()
        {
            if (AutoStart) StartRun();
        }

        public void StartRun()
        {
            if (Dungeon == null)
            {
                Debug.LogError("[DungeonRunner] No DungeonDefinition assigned; nothing to run.");
                return;
            }

            EndRun(RunState.NotStarted, silent: true);

            Seed = SeedOverride != 0
                ? SeedOverride
                : Dungeon.UseRandomSeed ? Environment.TickCount : Dungeon.FixedSeed;

            _root = new GameObject($"Dungeon (seed {Seed})").transform;
            _root.SetParent(transform, false);

            World = new DungeonWorld(Dungeon.MapSize.x, Dungeon.MapSize.y, Seed, _root)
            {
                Biome = Dungeon.Biome
            };

            var generator = new DungeonGenerator();
            World.Layout = generator.Generate(Dungeon, World.Map, Seed);

            Builder = new DungeonBuilder();
            Builder.Build(World, Dungeon.Biome, _root);

            SpawnTerrainHazards();

            // The loot system exists before the party so that starting gear can
            // be handed out through exactly the same path a drop takes.
            Inventory = new Inventory();
            Loot = new LootSystem(World, Inventory);

            SpawnParty();
            GrantStartingGear();

            Director = new EncounterDirector(World, Dungeon);
            Director.DungeonCleared += OnDungeonCleared;

            World.ActorDied += OnActorDied;
            World.Clock.Ticked += OnTick;

            State = RunState.Running;
            RunStarted?.Invoke(World);
        }

        /// <summary>
        /// Lays the biome's standing-liquid hazard over every lava cell.
        ///
        /// One permanent hazard per contiguous cell rather than one giant one:
        /// they merge on spawn, and per-cell placement means a lava lake that is
        /// carved into an odd shape still matches the tiles exactly.
        /// </summary>
        private void SpawnTerrainHazards()
        {
            HazardDefinition liquid = Dungeon.Biome != null ? Dungeon.Biome.LiquidHazard : null;
            if (liquid == null) return;

            for (int x = 0; x < World.Map.Width; x++)
            {
                for (int y = 0; y < World.Map.Height; y++)
                {
                    var cell = new GridCoord(x, y);
                    if (World.Map.TerrainAt(cell) != CellTerrain.Lava) continue;
                    World.Hazards.Spawn(liquid, cell, 0, 0f, null);
                }
            }
        }

        private void SpawnParty()
        {
            _party.Clear();

            if (Roster == null || Roster.Count == 0)
            {
                Debug.LogError("[DungeonRunner] No PartyRoster assigned; the dungeon will be empty.");
                return;
            }

            GridCoord spawn = World.Layout.PartySpawn;
            int slots = Mathf.Min(Roster.Count, Dungeon.PartySize);
            int localSlot = Mathf.Clamp(Roster.LocalPlayerSlot, 0, slots - 1);

            for (int i = 0; i < slots; i++)
            {
                ClassDefinition classDef = Roster.ClassAt(i);
                if (classDef == null) continue;

                if (!World.Map.TryFindFreeCellNear(spawn, 6, out GridCoord cell))
                {
                    Debug.LogWarning("[DungeonRunner] Ran out of room at the entrance; party is partially spawned.");
                    break;
                }

                Actor member = World.SpawnPartyMember(classDef, Roster.NameAt(i), cell, i == localSlot);
                _party.Add(member);

                if (i != localSlot) continue;

                LocalPlayer = member;
                World.PartyLeader = member;
            }

            LocalPlayerChanged?.Invoke(LocalPlayer);
        }

        /// <summary>
        /// Hands each class its starting kit through the normal loot path.
        ///
        /// Going through <see cref="LootSystem.Assign"/> rather than equipping
        /// directly means starting gear exercises the same scoring and set
        /// tracking a drop does — so a broken set bonus shows up on the first
        /// frame of the first run rather than twenty minutes in.
        /// </summary>
        private void GrantStartingGear()
        {
            for (int i = 0; i < _party.Count; i++)
            {
                Actor member = _party[i];
                ClassDefinition classDef = member != null ? member.ClassDefinition : null;
                if (classDef == null) continue;

                for (int g = 0; g < classDef.StartingGear.Count; g++)
                {
                    ItemDefinition definition = classDef.StartingGear[g];
                    if (definition == null) continue;

                    // Equipped onto this member specifically: starting gear is
                    // part of the class, not a drop to be competed over.
                    ItemInstance displaced = member.Equipment.Equip(new ItemInstance(definition));
                    if (displaced != null) Inventory.Stow(displaced);
                }
            }
        }

        private void Update()
        {
            if (State != RunState.Running || World == null) return;
            World.Clock.Advance(Time.deltaTime);
        }

        private void OnTick(int tick)
        {
            World.Tick(tick);
            Director?.Tick(tick);
            Loot?.Tick(tick);

            if (!World.Actors.AnyPartyAlive()) EndRun(RunState.Wiped);
        }

        /// <summary>
        /// Spends embers on the cheapest worthwhile upgrade across the party.
        /// Bound to a key by the HUD; returns the item improved, or null when
        /// nothing is affordable.
        /// </summary>
        public ItemInstance UpgradeBestItem()
        {
            if (Inventory == null || _party.Count == 0) return null;

            var equipmentSets = new List<Equipment>(_party.Count);
            for (int i = 0; i < _party.Count; i++)
                if (_party[i] != null) equipmentSets.Add(_party[i].Equipment);

            ItemInstance candidate = Inventory.FindBestUpgradeCandidate(equipmentSets);
            if (candidate == null) return null;

            return Inventory.TryUpgrade(candidate) ? candidate : null;
        }

        private void OnActorDied(Actor actor, Actor killer)
        {
            if (actor == null || actor.Faction != Faction.Party) return;

            // The party keeps moving when the human's slot dies: control passes to
            // the next living member instead of leaving the player spectating a
            // corpse while four companions carry on without them.
            if (actor != LocalPlayer) return;

            Actor replacement = null;
            for (int i = 0; i < _party.Count; i++)
            {
                if (_party[i] == null || !_party[i].IsAlive) continue;
                replacement = _party[i];
                break;
            }

            LocalPlayer = replacement;
            World.PartyLeader = replacement;

            if (replacement != null) SetBrainEnabled(replacement, false);
            LocalPlayerChanged?.Invoke(LocalPlayer);
        }

        private void SetBrainEnabled(Actor actor, bool enabled)
        {
            IReadOnlyList<IBrain> brains = World.Brains;
            for (int i = 0; i < brains.Count; i++)
                if (brains[i].Owner == actor) brains[i].Enabled = enabled;
        }

        private void OnDungeonCleared() => EndRun(RunState.Cleared);

        public void EndRun(RunState finalState, bool silent = false)
        {
            if (World != null)
            {
                World.Clock.Ticked -= OnTick;
                World.ActorDied -= OnActorDied;
                World.Clear();
            }

            if (Director != null)
            {
                Director.DungeonCleared -= OnDungeonCleared;
                Director.Dispose();
                Director = null;
            }

            if (Loot != null)
            {
                Loot.Clear();
                Loot.Dispose();
                Loot = null;
            }

            if (_root != null) ViewObjects.Destroy(_root.gameObject);

            World = null;
            Inventory = null;
            LocalPlayer = null;
            _party.Clear();
            State = finalState;

            if (!silent && finalState != RunState.NotStarted) RunEnded?.Invoke(finalState);
        }

        private void OnDestroy() => EndRun(RunState.NotStarted, silent: true);

        /// <summary>Restarts with a fresh seed. Bound to R by the debug overlay.</summary>
        [ContextMenu("Restart Run")]
        public void RestartRun()
        {
            SeedOverride = 0;
            StartRun();
        }
    }
}
