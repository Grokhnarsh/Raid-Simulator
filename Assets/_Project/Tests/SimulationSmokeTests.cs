using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.Items;
using EmberDepths.Gameplay.Run;
using EmberDepths.Gameplay.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EmberDepths.Tests
{
    /// <summary>
    /// Runs the real dungeon, with the real content assets, headlessly.
    ///
    /// Unit tests cover the pieces. This covers the thing unit tests cannot: that
    /// the pieces, wired together and left running for a hundred simulated
    /// seconds with brains making decisions and abilities resolving, do not throw
    /// and do not deadlock. Most of the bugs that survive a component-level test
    /// suite live in exactly that interaction.
    ///
    /// No rendering, no play mode, no tilemap — just <see cref="DungeonWorld"/>
    /// being ticked, which is possible only because the simulation never depends
    /// on presentation.
    /// </summary>
    public sealed class SimulationSmokeTests
    {
        private const int TicksToRun = 2000; // 100 seconds at 20 Hz

        private GameObject _root;

        private static T Load<T>(string filter) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"{filter} t:{typeof(T).Name}");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        [SetUp]
        public void SetUp() => _root = new GameObject("TestDungeonRoot");

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        private DungeonWorld BuildWorld(int seed, out DungeonDefinition definition, out PartyRoster roster)
        {
            definition = Load<DungeonDefinition>("DG_EmberDepths");
            roster = Load<PartyRoster>("PR_Default");

            Assert.IsNotNull(definition, "DG_EmberDepths missing — run EmberDepths/Content/Build Volcano Dungeon");
            Assert.IsNotNull(roster, "PR_Default missing — run EmberDepths/Content/Build Volcano Dungeon");

            var world = new DungeonWorld(definition.MapSize.x, definition.MapSize.y, seed, _root.transform)
            {
                Biome = definition.Biome
            };

            world.Layout = new DungeonGenerator().Generate(definition, world.Map, seed);
            return world;
        }

        private static void SpawnParty(DungeonWorld world, DungeonDefinition definition, PartyRoster roster)
        {
            GridCoord spawn = world.Layout.PartySpawn;
            int slots = Mathf.Min(roster.Count, definition.PartySize);

            for (int i = 0; i < slots; i++)
            {
                ClassDefinition classDef = roster.ClassAt(i);
                if (classDef == null) continue;
                if (!world.Map.TryFindFreeCellNear(spawn, 6, out GridCoord cell)) break;

                Actor member = world.SpawnPartyMember(classDef, roster.NameAt(i), cell, i == 0);
                if (i == 0) world.PartyLeader = member;
            }
        }

        [Test]
        public void ContentAssetsAreCompleteAndLinked()
        {
            var roster = Load<PartyRoster>("PR_Default");
            Assert.IsNotNull(roster);
            Assert.AreEqual(5, roster.Count, "the party is designed around five slots");
            Assert.IsTrue(roster.IsBalanced(), "roster needs at least one tank and one healer");

            for (int i = 0; i < roster.Count; i++)
            {
                ClassDefinition c = roster.ClassAt(i);
                Assert.IsNotNull(c, $"slot {i} is empty");
                Assert.IsNotNull(c.BasicAttack, $"{c.DisplayName} has no basic attack");
                Assert.Greater(c.Abilities.Count, 0, $"{c.DisplayName} has no abilities");
                Assert.IsNotNull(c.Visuals, $"{c.DisplayName} has no visual set");
                Assert.Greater(c.BaseStats.MaxHealth, 0f, $"{c.DisplayName} spawns dead");
                Assert.Greater(c.BaseStats.MoveSpeed, 0f, $"{c.DisplayName} cannot move");
            }

            var dungeon = Load<DungeonDefinition>("DG_EmberDepths");
            Assert.IsNotNull(dungeon.Biome, "dungeon has no biome");
            Assert.IsNotNull(dungeon.Biome.Boss, "biome has no boss");
            Assert.AreEqual(EnemyRank.Boss, dungeon.Biome.Boss.Rank);
            Assert.IsNotNull(dungeon.Biome.Boss.BossProfile, "boss has no phase profile");
            Assert.Greater(dungeon.Biome.TrashEncounters.Count, 0, "no trash encounters");
            Assert.IsNotNull(dungeon.Biome.LiquidHazard, "no terrain hazard — the volcano is not hot");
        }

        [Test]
        public void BiomeTilesAreLinkedAndDistinct()
        {
            // AssetDatabase.FindAssets matches loosely, so a lookup for
            // "T_basalt_cracked" can also return "T_basalt_wall". If the content
            // builder took the first hit, the wall tile would end up in the floor
            // pool — which renders as a plausible-looking dungeon and is very hard
            // to spot by eye.
            BiomeDefinition biome = Load<DungeonDefinition>("DG_EmberDepths").Biome;

            Assert.Greater(biome.FloorTiles.Count, 0, "biome has no floor tiles");
            Assert.Greater(biome.WallTiles.Count, 0, "biome has no wall tiles");
            Assert.IsNotNull(biome.LiquidTile, "biome has no liquid tile");

            var seen = new HashSet<Object>();

            foreach (WeightedTile entry in biome.FloorTiles)
            {
                Assert.IsNotNull(entry.Tile, "a floor tile slot is empty");
                Assert.IsTrue(seen.Add(entry.Tile), $"'{entry.Tile.name}' appears twice in the floor pool");
                Assert.Greater(entry.Weight, 0f, $"'{entry.Tile.name}' has zero weight and can never be picked");
            }

            foreach (WeightedTile entry in biome.WallTiles)
            {
                Assert.IsNotNull(entry.Tile, "a wall tile slot is empty");
                Assert.IsFalse(seen.Contains(entry.Tile),
                    $"'{entry.Tile.name}' is used as both floor and wall — a fuzzy asset lookup");
            }

            Assert.IsFalse(seen.Contains(biome.LiquidTile),
                "the liquid tile is also in the floor pool");
        }

        [Test]
        public void EveryEnemyHasLootAndEveryClassHasStartingGear()
        {
            string[] enemyGuids = AssetDatabase.FindAssets("t:EnemyDefinition");
            Assert.Greater(enemyGuids.Length, 0);

            foreach (string guid in enemyGuids)
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (enemy == null) continue;

                // An enemy with no table pays nothing, which quietly breaks the
                // ember economy for whichever room it happens to fill.
                Assert.IsNotNull(enemy.Loot, $"{enemy.DisplayName} drops nothing at all");
                Assert.Greater(enemy.Loot.MaxEmbers, 0, $"{enemy.DisplayName} pays no embers");
            }

            var roster = Load<PartyRoster>("PR_Default");
            for (int i = 0; i < roster.Count; i++)
            {
                ClassDefinition c = roster.ClassAt(i);
                Assert.Greater(c.StartingGear.Count, 0, $"{c.DisplayName} starts naked");

                var slots = new HashSet<EquipmentSlot>();
                foreach (ItemDefinition item in c.StartingGear)
                {
                    Assert.IsNotNull(item, $"{c.DisplayName} has an empty starting-gear slot");
                    Assert.IsTrue(slots.Add(item.Slot),
                        $"{c.DisplayName} starts with two items for {item.Slot}; one would be discarded");
                }
            }
        }

        [Test]
        public void SetsAreCompleteAndTieredCoherently()
        {
            string[] guids = AssetDatabase.FindAssets("t:ItemSetDefinition");
            Assert.Greater(guids.Length, 0, "no sets were built");

            foreach (string guid in guids)
            {
                var set = AssetDatabase.LoadAssetAtPath<ItemSetDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (set == null) continue;

                Assert.Greater(set.Tiers.Count, 0, $"{set.DisplayName} has no bonuses");

                var slots = new HashSet<EquipmentSlot>();
                foreach (ItemDefinition member in set.Members)
                {
                    Assert.IsNotNull(member, $"{set.DisplayName} lists an empty member");
                    Assert.AreSame(set, member.Set,
                        $"{member.DisplayName} is listed in {set.DisplayName} but does not point back at it");
                    Assert.IsTrue(slots.Add(member.Slot),
                        $"{set.DisplayName} has two pieces for {member.Slot}; the tier is unreachable");
                }

                foreach (SetBonusTier tier in set.Tiers)
                {
                    Assert.LessOrEqual(tier.RequiredPieces, set.Members.Count,
                        $"{set.DisplayName} has a ({tier.RequiredPieces}) tier but only {set.Members.Count} pieces");
                    Assert.IsTrue(tier.Modifiers.Count > 0 || tier.Aura != null || tier.GrantedAbility != null,
                        $"{set.DisplayName}'s ({tier.RequiredPieces}) tier does nothing");
                }
            }
        }

        [Test]
        public void BossPhasesDescendAndCarryContent()
        {
            BossDefinition boss = Load<DungeonDefinition>("DG_EmberDepths").Biome.Boss.BossProfile;

            Assert.GreaterOrEqual(boss.Phases.Count, 2, "a one-phase boss is just a big trash mob");
            Assert.AreEqual(1f, boss.Phases[0].EnterAtHealthFraction, 0.001f,
                "the first phase must be active at full health");

            for (int i = 1; i < boss.Phases.Count; i++)
            {
                Assert.Less(boss.Phases[i].EnterAtHealthFraction,
                    boss.Phases[i - 1].EnterAtHealthFraction,
                    $"phase {i} does not come after phase {i - 1}");
            }

            foreach (BossPhase phase in boss.Phases)
                Assert.Greater(phase.Abilities.Count, 0, $"phase '{phase.Name}' has no abilities");
        }

        [Test]
        public void EveryEnemyAbilityWithAnAreaShapeIsTelegraphed()
        {
            // An untelegraphed area effect is indistinguishable from a bug: the
            // player sees health vanish with no visible cause.
            string[] guids = AssetDatabase.FindAssets("t:EnemyDefinition");
            int checkedCount = 0;

            foreach (string guid in guids)
            {
                var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (enemy == null) continue;

                foreach (AbilityDefinition ability in enemy.AllAbilities())
                {
                    if (ability == null || !ability.IsAreaEffect) continue;
                    checkedCount++;
                    Assert.Greater(ability.TelegraphLeadTime, 0f,
                        $"{enemy.DisplayName}'s '{ability.DisplayName}' hits an area with no warning");
                }
            }

            Assert.Greater(checkedCount, 0, "expected at least one enemy area ability to check");
        }

        [Test]
        public void AFullRunTicksWithoutThrowing()
        {
            DungeonWorld world = BuildWorld(20260910, out DungeonDefinition definition, out PartyRoster roster);
            SpawnParty(world, definition, roster);

            var director = new EncounterDirector(world, definition);
            int partyCount = world.Actors.Party.Count;
            Assert.AreEqual(5, partyCount, "the party did not fully spawn");

            for (int tick = 1; tick <= TicksToRun; tick++)
            {
                world.Clock.Advance(SimClock.TickDuration);
                world.Tick(tick);
                director.Tick(tick);
            }

            director.Dispose();

            // The party wandering into a room and picking a fight is the whole
            // loop; if nothing ever spawned, the director never fired.
            Assert.Pass($"ran {TicksToRun} ticks; " +
                        $"{world.Actors.Hostiles.Count} hostiles alive, " +
                        $"{director.RoomsCleared} rooms cleared");
        }

        [Test]
        public void PartyEngagesAndFightsWhenPlacedOnAPack()
        {
            DungeonWorld world = BuildWorld(555, out DungeonDefinition definition, out PartyRoster roster);

            // Drop the party straight into a combat room rather than waiting for
            // them to walk there, so the test exercises combat rather than pathing.
            Room target = null;
            foreach (Room room in world.Layout.Rooms)
            {
                if (room.Kind != RoomKind.Combat || room.Encounter == null) continue;
                target = room;
                break;
            }

            Assert.IsNotNull(target, "generator produced no populated combat room");
            world.Layout.PartySpawn = target.Centre;
            SpawnParty(world, definition, roster);

            var director = new EncounterDirector(world, definition);

            int hostilesSeen = 0;
            float damageDealt = 0f;
            world.Combat.DamageDealt += result => damageDealt += result.Amount;

            for (int tick = 1; tick <= 600; tick++)
            {
                world.Clock.Advance(SimClock.TickDuration);
                world.Tick(tick);
                director.Tick(tick);
                hostilesSeen = Mathf.Max(hostilesSeen, world.Actors.Hostiles.Count);
            }

            director.Dispose();

            Assert.Greater(hostilesSeen, 0, "the encounter never activated");
            Assert.Greater(damageDealt, 0f, "30 seconds in a packed room and nobody hit anybody");
        }

        [Test]
        public void KillingThingsProducesLootAndEmbers()
        {
            DungeonWorld world = BuildWorld(8801, out DungeonDefinition definition, out PartyRoster roster);

            // Start inside a populated room so the test measures the reward loop
            // rather than how long the party takes to find a fight.
            Room target = null;
            foreach (Room room in world.Layout.Rooms)
            {
                if (room.Kind != RoomKind.Combat || room.Encounter == null) continue;
                target = room;
                break;
            }

            Assert.IsNotNull(target, "generator produced no populated combat room");
            world.Layout.PartySpawn = target.Centre;
            SpawnParty(world, definition, roster);

            var inventory = new Inventory();
            var loot = new LootSystem(world, inventory);
            var director = new EncounterDirector(world, definition);

            int itemsLooted = 0;
            loot.ItemLooted += (item, receiver) => itemsLooted++;

            for (int tick = 1; tick <= 2400; tick++)
            {
                world.Clock.Advance(SimClock.TickDuration);
                world.Tick(tick);
                director.Tick(tick);
                loot.Tick(tick);
            }

            int partyAlive = 0;
            foreach (Actor member in world.Actors.Party) if (member.IsAlive) partyAlive++;

            string diagnostics =
                $"party alive {partyAlive}/5, hostiles alive {world.Actors.Hostiles.Count}, " +
                $"rooms cleared {director.RoomsCleared}, drops on floor {loot.ActiveDropCount}, " +
                $"items dropped {loot.ItemsDropped}, embers earned {inventory.TotalEmbersEarned}";

            director.Dispose();
            loot.Dispose();

            Assert.Greater(loot.ItemsDropped + inventory.TotalEmbersEarned, 0,
                $"two minutes of fighting produced no rewards at all — {diagnostics}");
            Assert.Greater(inventory.TotalEmbersEarned, 0, $"no embers were ever picked up — {diagnostics}");

            // Auto-assignment is what makes drops matter without an inventory
            // screen; if nothing was ever equipped, the loop is broken even
            // though items technically dropped.
            if (itemsLooted > 0)
                Assert.Greater(loot.ItemsEquipped, 0, "items were looted but nobody wore any");
        }

        [Test]
        public void StartingGearIsEquippedAndCountsTowardsSets()
        {
            DungeonWorld world = BuildWorld(5150, out DungeonDefinition definition, out PartyRoster roster);
            SpawnParty(world, definition, roster);

            var inventory = new Inventory();
            var loot = new LootSystem(world, inventory);

            IReadOnlyList<Actor> party = world.Actors.Party;
            Assert.AreEqual(5, party.Count);

            for (int i = 0; i < party.Count; i++)
            {
                Actor member = party[i];
                ClassDefinition classDef = member.ClassDefinition;
                Assert.IsNotNull(classDef);

                foreach (ItemDefinition definitionItem in classDef.StartingGear)
                    member.Equipment.Equip(new ItemInstance(definitionItem));

                Assert.AreEqual(classDef.StartingGear.Count, member.Equipment.EquippedCount,
                    $"{member.DisplayName} did not end up wearing its whole kit");
            }

            // Now hand the tank a full Emberward set through the normal path and
            // check the six-piece aura actually lands on the actor.
            Actor tank = null;
            for (int i = 0; i < party.Count; i++)
                if (party[i].Role == ActorRole.Tank) tank = party[i];

            Assert.IsNotNull(tank, "roster has no tank");

            var set = Load<ItemSetDefinition>("SET_Emberward");
            Assert.IsNotNull(set, "SET_Emberward missing — run Content/Build Volcano Dungeon");

            foreach (ItemDefinition piece in set.Members)
                tank.Equipment.Equip(new ItemInstance(piece));

            Assert.AreEqual(set.Members.Count, tank.Equipment.PiecesOf(set));

            SetBonusTier capstone = set.Tiers[set.Tiers.Count - 1];
            Assert.IsNotNull(capstone.Aura, "the capstone tier should grant an aura");
            Assert.IsTrue(tank.HasStatus(capstone.Aura),
                "wearing the full set did not apply its aura to the actor");

            // And taking one piece off must take the aura with it.
            tank.Equipment.Unequip(set.Members[0].Slot);
            Assert.IsFalse(tank.HasStatus(capstone.Aura),
                "the set aura outlived the set");

            loot.Dispose();
        }

        [Test]
        public void AStartedRunEquipsThePartysStartingGear()
        {
            // Drives DungeonRunner itself rather than assembling a world by
            // hand, because the bug this guards against — gear that is authored
            // correctly but never handed out — lives in the wiring, not in any
            // of the pieces.
            var go = new GameObject("TestRunner");
            go.transform.SetParent(_root.transform, false);

            var runner = go.AddComponent<DungeonRunner>();
            runner.AutoStart = false;
            runner.Dungeon = Load<DungeonDefinition>("DG_EmberDepths");
            runner.Roster = Load<PartyRoster>("PR_Default");
            runner.SeedOverride = 24601;

            runner.StartRun();

            try
            {
                Assert.AreEqual(RunState.Running, runner.State);
                Assert.AreEqual(5, runner.Party.Count);
                Assert.IsNotNull(runner.Inventory, "the run has no inventory");
                Assert.IsNotNull(runner.Loot, "the run has no loot system");

                for (int i = 0; i < runner.Party.Count; i++)
                {
                    Actor member = runner.Party[i];
                    int expected = member.ClassDefinition.StartingGear.Count;

                    Assert.Greater(expected, 0, $"{member.DisplayName}'s class defines no starting gear");
                    Assert.AreEqual(expected, member.Equipment.EquippedCount,
                        $"{member.DisplayName} was handed {expected} items but is wearing " +
                        $"{member.Equipment.EquippedCount}");

                    // And the stats must actually reflect it, or the gear is
                    // decorative.
                    Assert.Greater(member.Equipment.AverageItemLevel, 0f,
                        $"{member.DisplayName} reports item level zero while wearing gear");
                }
            }
            finally
            {
                runner.EndRun(RunState.NotStarted, silent: true);
            }
        }

        [Test]
        public void ActorsNeverShareACell()
        {
            // Occupancy is the only thing stopping five party members from
            // collapsing onto one tile, and it is easy to break with a teleport.
            DungeonWorld world = BuildWorld(31337, out DungeonDefinition definition, out PartyRoster roster);
            SpawnParty(world, definition, roster);

            var director = new EncounterDirector(world, definition);
            var occupied = new Dictionary<GridCoord, Actor>();

            for (int tick = 1; tick <= 800; tick++)
            {
                world.Clock.Advance(SimClock.TickDuration);
                world.Tick(tick);
                director.Tick(tick);

                occupied.Clear();
                foreach (Actor actor in world.Actors.All)
                {
                    if (!actor.IsAlive) continue;
                    if ((actor.Flags & ActorFlags.Incorporeal) != 0) continue;

                    Assert.IsFalse(occupied.ContainsKey(actor.Cell),
                        $"tick {tick}: {actor.DisplayName} and " +
                        $"{(occupied.TryGetValue(actor.Cell, out Actor other) ? other.DisplayName : "?")} " +
                        $"both occupy {actor.Cell}");

                    occupied[actor.Cell] = actor;
                }
            }

            director.Dispose();
        }
    }
}
