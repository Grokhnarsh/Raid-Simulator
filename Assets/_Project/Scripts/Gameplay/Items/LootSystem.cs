using System;
using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.Presentation;
using EmberDepths.Gameplay.World;
using UnityEngine;

namespace EmberDepths.Gameplay.Items
{
    /// <summary>
    /// Turns dead enemies into loot on the floor, and loot on the floor into
    /// equipped gear.
    ///
    /// Drops are rolled from the run's seeded stream and picked up on the
    /// simulation tick, so a replayed seed produces the same items in the same
    /// order. Pickup is generous — walking near a drop is enough — because
    /// precise positioning is not a skill this game is testing.
    ///
    /// Items auto-equip onto whichever party member gains most from them. That
    /// is a deliberate stand-in for an inventory screen, not a permanent design:
    /// the scoring lives in <see cref="Equipment.UpgradeDelta"/> and the
    /// displaced item goes to the stash, so adding a manual swap later changes
    /// this class only.
    /// </summary>
    public sealed class LootSystem
    {
        /// <summary>Tiles within which a party member sweeps up a drop.</summary>
        private const int PickupRadius = 1;

        private readonly DungeonWorld _world;
        private readonly List<LootDrop> _drops = new List<LootDrop>(32);
        private readonly List<LootRoll> _rollScratch = new List<LootRoll>(4);
        private readonly List<LootDrop> _collected = new List<LootDrop>(4);

        private Transform _root;
        private int _nextCheckTick;

        public Inventory Inventory { get; }

        public int ItemsDropped { get; private set; }
        public int ItemsEquipped { get; private set; }

        /// <summary>Item, and the party member who ended up wearing it (null if stashed).</summary>
        public event Action<ItemInstance, Actor> ItemLooted;

        public event Action<int> EmbersLooted;

        public LootSystem(DungeonWorld world, Inventory inventory)
        {
            _world = world;
            Inventory = inventory;
            _world.ActorDied += OnActorDied;
        }

        public void Dispose() => _world.ActorDied -= OnActorDied;

        private Transform Root
        {
            get
            {
                if (_root != null) return _root;
                var go = new GameObject("Loot");
                go.transform.SetParent(_world.SceneRoot, false);
                _root = go.transform;
                return _root;
            }
        }

        // --- dropping ---------------------------------------------------------------

        private void OnActorDied(Actor actor, Actor killer)
        {
            if (actor == null || actor.Faction != Faction.Hostile) return;

            LootTable table = actor.EnemyDefinition != null ? actor.EnemyDefinition.Loot : null;
            if (table == null) return;

            // A per-kill stream keyed on the actor's id, forked from the run seed.
            // Without the id, every enemy in a pack would roll identically.
            DeterministicRandom rng = _world.Rng.Fork($"loot{actor.Id}");

            float depth = _world.Layout?.RoomContaining(actor.Cell)?.Depth ?? 0f;

            _rollScratch.Clear();
            table.Roll(rng, _rollScratch, out int embers, depth);

            if (embers > 0) SpawnEmbers(embers, actor.Cell);

            for (int i = 0; i < _rollScratch.Count; i++)
            {
                LootRoll roll = _rollScratch[i];
                if (roll.Item == null) continue;
                SpawnItem(new ItemInstance(roll.Item, roll.UpgradeLevel), actor.Cell);
            }
        }

        public LootDrop SpawnItem(ItemInstance item, GridCoord near)
        {
            if (item?.Definition == null) return null;
            if (!_world.Map.TryFindFreeCellNear(near, 4, out GridCoord cell)) cell = near;

            ItemsDropped++;
            return CreateDrop(item, 0, cell);
        }

        public LootDrop SpawnEmbers(int amount, GridCoord near)
        {
            if (amount <= 0) return null;
            if (!_world.Map.TryFindFreeCellNear(near, 3, out GridCoord cell)) cell = near;

            return CreateDrop(null, amount, cell);
        }

        private LootDrop CreateDrop(ItemInstance item, int embers, GridCoord cell)
        {
            var go = new GameObject(item != null ? $"Loot_{item.Definition.name}" : "Loot_Embers");
            var drop = go.AddComponent<LootDrop>();
            drop.Initialise(item, embers, cell, Root);
            _drops.Add(drop);
            return drop;
        }

        // --- picking up -------------------------------------------------------------

        public void Tick(int tick)
        {
            if (_drops.Count == 0 || tick < _nextCheckTick) return;

            // Five times a second. A party cannot cross a tile faster than that,
            // so nothing is ever stepped over without being collected.
            _nextCheckTick = tick + Mathf.Max(1, SimClock.TicksPerSecond / 5);

            _collected.Clear();
            IReadOnlyList<Actor> party = _world.Actors.Party;

            for (int i = 0; i < _drops.Count; i++)
            {
                LootDrop drop = _drops[i];
                if (drop == null) continue;

                Actor collector = FindCollector(party, drop.Cell);
                if (collector == null) continue;

                _collected.Add(drop);
            }

            for (int i = 0; i < _collected.Count; i++) Collect(_collected[i]);
        }

        private static Actor FindCollector(IReadOnlyList<Actor> party, GridCoord cell)
        {
            for (int i = 0; i < party.Count; i++)
            {
                Actor member = party[i];
                if (member == null || !member.IsAlive) continue;
                if (GridCoord.Chebyshev(member.Cell, cell) <= PickupRadius) return member;
            }
            return null;
        }

        private void Collect(LootDrop drop)
        {
            _drops.Remove(drop);

            if (drop.IsEmbers)
            {
                Inventory.AddEmbers(drop.Embers);
                EmbersLooted?.Invoke(drop.Embers);
            }
            else if (drop.Item != null)
            {
                Actor receiver = Assign(drop.Item);
                ItemLooted?.Invoke(drop.Item, receiver);
            }

            ViewObjects.Destroy(drop.gameObject);
        }

        /// <summary>
        /// Gives the item to whoever gains most, or stashes it if nobody does.
        /// Returns the new wearer, or null when it went to the stash.
        /// </summary>
        public Actor Assign(ItemInstance item)
        {
            Actor best = null;
            float bestDelta = 0f;

            IReadOnlyList<Actor> party = _world.Actors.Party;
            for (int i = 0; i < party.Count; i++)
            {
                Actor member = party[i];
                if (member == null || !member.IsAlive) continue;

                float delta = member.Equipment.UpgradeDelta(item);
                if (delta <= bestDelta) continue;

                best = member;
                bestDelta = delta;
            }

            if (best == null)
            {
                Inventory.Stow(item);
                return null;
            }

            ItemInstance displaced = best.Equipment.Equip(item);
            ItemsEquipped++;

            // The old piece is not destroyed: another member may want it, and a
            // future inventory screen certainly will.
            if (displaced != null) Inventory.Stow(displaced);

            return best;
        }

        public void Clear()
        {
            for (int i = 0; i < _drops.Count; i++)
                if (_drops[i] != null) ViewObjects.Destroy(_drops[i].gameObject);

            _drops.Clear();
        }

        public int ActiveDropCount => _drops.Count;
    }
}
