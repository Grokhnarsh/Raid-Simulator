using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.Items;
using NUnit.Framework;
using UnityEngine;

namespace EmberDepths.Tests
{
    /// <summary>
    /// Equipment, set bonuses and upgrades, tested against throwaway assets
    /// rather than the shipped content — so these keep passing when the
    /// designers retune every number in the game.
    /// </summary>
    public sealed class ItemSystemTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            _created.Clear();
        }

        private T Make<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _created.Add(asset);
            return asset;
        }

        private ItemDefinition MakeItem(string name, EquipmentSlot slot, ItemSetDefinition set = null,
            params StatModifier[] modifiers)
        {
            var item = Make<ItemDefinition>(name);
            item.DisplayName = name;
            item.Slot = slot;
            item.Set = set;
            item.ItemLevel = 5;
            item.MaxUpgradeLevel = 5;
            item.BaseUpgradeCost = 100;
            item.UpgradeCostGrowth = 2f;
            item.Modifiers = new List<StatModifier>(modifiers);
            return item;
        }

        // --- modifiers ------------------------------------------------------------

        [Test]
        public void ModifierValueScalesWithUpgradeLevel()
        {
            StatModifier mod = StatModifier.Flat(StatKind.Armor, 10f, 4f);

            Assert.AreEqual(10f, mod.ValueAt(0), 1e-4f);
            Assert.AreEqual(14f, mod.ValueAt(1), 1e-4f);
            Assert.AreEqual(30f, mod.ValueAt(5), 1e-4f);
        }

        [Test]
        public void AccumulatorAppliesFlatBeforePercent()
        {
            var accumulator = StatAccumulator.Create();
            accumulator.Add(StatModifier.Flat(StatKind.Armor, 50f), 0);
            accumulator.Add(StatModifier.Percent(StatKind.Armor, 0.20f), 0);

            // (100 + 50) * 1.20, not 100 * 1.20 + 50. A percentage trinket should
            // multiply the armour you are actually wearing.
            Assert.AreEqual(180f, accumulator.Apply(StatKind.Armor, 100f), 1e-3f);
        }

        [Test]
        public void AccumulatorIsOrderIndependent()
        {
            var a = StatAccumulator.Create();
            a.Add(StatModifier.Percent(StatKind.Power, 0.1f), 0);
            a.Add(StatModifier.Flat(StatKind.Power, 5f), 0);

            var b = StatAccumulator.Create();
            b.Add(StatModifier.Flat(StatKind.Power, 5f), 0);
            b.Add(StatModifier.Percent(StatKind.Power, 0.1f), 0);

            Assert.AreEqual(a.Apply(StatKind.Power, 10f), b.Apply(StatKind.Power, 10f), 1e-4f);
        }

        // --- equipment -------------------------------------------------------------

        [Test]
        public void EquippingReturnsWhatItDisplaced()
        {
            var equipment = new Equipment();
            var first = new ItemInstance(MakeItem("first", EquipmentSlot.Head));
            var second = new ItemInstance(MakeItem("second", EquipmentSlot.Head));

            Assert.IsNull(equipment.Equip(first), "nothing was in the slot");
            Assert.AreSame(first, equipment.Equip(second), "the old helmet must come back, not vanish");
            Assert.AreSame(second, equipment.Get(EquipmentSlot.Head));
        }

        [Test]
        public void EquipmentFeedsRuntimeStats()
        {
            StatBlock baseStats = StatBlock.Default;
            baseStats.Armor = 100f;
            baseStats.Power = 10f;

            var equipment = new Equipment();
            equipment.Equip(new ItemInstance(MakeItem("plate", EquipmentSlot.Chest, null,
                StatModifier.Flat(StatKind.Armor, 50f))));
            equipment.Equip(new ItemInstance(MakeItem("ring", EquipmentSlot.Trinket, null,
                StatModifier.Percent(StatKind.Power, 0.5f))));

            var stats = new RuntimeStats(baseStats);
            stats.Recompute(null, equipment);

            Assert.AreEqual(150f, stats.Armor, 1e-3f);
            Assert.AreEqual(15f, stats.Power, 1e-3f);
        }

        [Test]
        public void UnequippingRemovesItsContribution()
        {
            StatBlock baseStats = StatBlock.Default;
            baseStats.Armor = 100f;

            var equipment = new Equipment();
            equipment.Equip(new ItemInstance(MakeItem("plate", EquipmentSlot.Chest, null,
                StatModifier.Flat(StatKind.Armor, 50f))));

            var stats = new RuntimeStats(baseStats);
            stats.Recompute(null, equipment);
            Assert.AreEqual(150f, stats.Armor, 1e-3f);

            equipment.Unequip(EquipmentSlot.Chest);
            stats.Recompute(null, equipment);

            // The classic gear bug is a bonus that never comes off. Recomputing
            // from scratch is what prevents it, and this is the test that says so.
            Assert.AreEqual(100f, stats.Armor, 1e-3f);
        }

        [Test]
        public void UpgradeLevelRaisesTheContribution()
        {
            StatBlock baseStats = StatBlock.Default;
            baseStats.Armor = 0f;

            ItemDefinition definition = MakeItem("plate", EquipmentSlot.Chest, null,
                StatModifier.Flat(StatKind.Armor, 10f, 5f));

            var item = new ItemInstance(definition, upgradeLevel: 3);
            var equipment = new Equipment();
            equipment.Equip(item);

            var stats = new RuntimeStats(baseStats);
            stats.Recompute(null, equipment);

            Assert.AreEqual(25f, stats.Armor, 1e-3f, "10 base + 3 levels x 5");
        }

        // --- sets --------------------------------------------------------------------

        private ItemSetDefinition MakeSet(string name, out ItemDefinition[] pieces)
        {
            var set = Make<ItemSetDefinition>(name);
            set.DisplayName = name;

            set.Tiers = new List<SetBonusTier>
            {
                new SetBonusTier
                {
                    RequiredPieces = 2,
                    Modifiers = new List<StatModifier> { StatModifier.Flat(StatKind.Armor, 20f) }
                },
                new SetBonusTier
                {
                    RequiredPieces = 4,
                    Modifiers = new List<StatModifier> { StatModifier.Flat(StatKind.Power, 7f) }
                }
            };

            pieces = new ItemDefinition[Equipment.SlotCount];
            for (int i = 0; i < Equipment.SlotCount; i++)
                pieces[i] = MakeItem($"{name}_{i}", (EquipmentSlot)i, set);

            set.Members = new List<ItemDefinition>(pieces);
            return set;
        }

        [Test]
        public void SetPiecesAreCountedAsTheyGoOnAndOff()
        {
            ItemSetDefinition set = MakeSet("Testguard", out ItemDefinition[] pieces);
            var equipment = new Equipment();

            Assert.AreEqual(0, equipment.PiecesOf(set));

            equipment.Equip(new ItemInstance(pieces[0]));
            equipment.Equip(new ItemInstance(pieces[1]));
            Assert.AreEqual(2, equipment.PiecesOf(set));

            equipment.Unequip(pieces[0].Slot);
            Assert.AreEqual(1, equipment.PiecesOf(set));
        }

        [Test]
        public void SwappingASetPieceForAPlainOneDropsTheCount()
        {
            ItemSetDefinition set = MakeSet("Testguard", out ItemDefinition[] pieces);
            var equipment = new Equipment();

            equipment.Equip(new ItemInstance(pieces[0]));
            equipment.Equip(new ItemInstance(pieces[1]));
            Assert.AreEqual(2, equipment.PiecesOf(set));

            // Replacing in-place, rather than unequipping first, is the path that
            // an auto-equipping loot system actually takes.
            equipment.Equip(new ItemInstance(MakeItem("plain", pieces[0].Slot)));
            Assert.AreEqual(1, equipment.PiecesOf(set));
        }

        [Test]
        public void SetTiersActivateAtTheirThresholdAndAreCumulative()
        {
            ItemSetDefinition set = MakeSet("Testguard", out ItemDefinition[] pieces);
            var equipment = new Equipment();

            StatBlock baseStats = StatBlock.Default;
            baseStats.Armor = 0f;
            baseStats.Power = 0f;
            var stats = new RuntimeStats(baseStats);

            equipment.Equip(new ItemInstance(pieces[0]));
            stats.Recompute(null, equipment);
            Assert.AreEqual(0f, stats.Armor, 1e-3f, "one piece grants nothing");

            equipment.Equip(new ItemInstance(pieces[1]));
            stats.Recompute(null, equipment);
            Assert.AreEqual(20f, stats.Armor, 1e-3f, "the (2) tier");
            Assert.AreEqual(0f, stats.Power, 1e-3f, "the (4) tier is not active yet");

            equipment.Equip(new ItemInstance(pieces[2]));
            equipment.Equip(new ItemInstance(pieces[3]));
            stats.Recompute(null, equipment);

            Assert.AreEqual(20f, stats.Armor, 1e-3f, "(2) must still apply at four pieces");
            Assert.AreEqual(7f, stats.Power, 1e-3f, "(4) is now active too");
        }

        [Test]
        public void SetTierDeactivatesWhenAPieceComesOff()
        {
            ItemSetDefinition set = MakeSet("Testguard", out ItemDefinition[] pieces);
            var equipment = new Equipment();

            for (int i = 0; i < 4; i++) equipment.Equip(new ItemInstance(pieces[i]));

            var stats = new RuntimeStats(StatBlock.Default);
            stats.Recompute(null, equipment);
            float withFour = stats.Power;

            equipment.Unequip(pieces[3].Slot);
            stats.Recompute(null, equipment);

            Assert.Less(stats.Power, withFour, "losing the fourth piece must lose the (4) bonus");
        }

        [Test]
        public void SetBonusesDoNotScaleWithItemUpgradeLevel()
        {
            // A set bonus rewards breadth, not depth. If it scaled with upgrades
            // the cheapest way to a big bonus would be to pour embers into one
            // piece, which is the opposite of the intent.
            ItemSetDefinition set = MakeSet("Testguard", out ItemDefinition[] pieces);

            var plain = new Equipment();
            var upgraded = new Equipment();

            for (int i = 0; i < 2; i++)
            {
                plain.Equip(new ItemInstance(pieces[i]));
                upgraded.Equip(new ItemInstance(pieces[i], upgradeLevel: 5));
            }

            var a = StatAccumulator.Create();
            var b = StatAccumulator.Create();
            plain.CollectModifiers(ref a);
            upgraded.CollectModifiers(ref b);

            // The pieces here carry no modifiers of their own, so any difference
            // could only come from the set tier.
            Assert.AreEqual(a.Flat(StatKind.Armor), b.Flat(StatKind.Armor), 1e-4f);
        }

        [Test]
        public void NextThresholdReportsTheGapToTheNextTier()
        {
            ItemSetDefinition set = MakeSet("Testguard", out _);

            Assert.AreEqual(2, set.NextThreshold(0));
            Assert.AreEqual(2, set.NextThreshold(1));
            Assert.AreEqual(4, set.NextThreshold(2));
            Assert.AreEqual(0, set.NextThreshold(4), "all tiers earned");
        }

        [Test]
        public void AWeakerSetPieceStillWinsWhenItCompletesATier()
        {
            // The whole point of a set: the second piece is worth more than its
            // stats, or nobody would ever assemble one.
            ItemSetDefinition set = MakeSet("Testguard", out ItemDefinition[] pieces);

            var equipment = new Equipment();
            equipment.Equip(new ItemInstance(pieces[0]));

            ItemDefinition strongPlain = MakeItem("plain", pieces[1].Slot, null,
                StatModifier.Flat(StatKind.Armor, 6f));
            strongPlain.ItemLevel = 9;

            var setPiece = new ItemInstance(pieces[1]);
            var plainPiece = new ItemInstance(strongPlain);

            Assert.Greater(equipment.UpgradeDelta(setPiece), equipment.UpgradeDelta(plainPiece),
                "the piece that completes the (2) tier should be preferred");
        }

        // --- upgrading and currency ----------------------------------------------------

        [Test]
        public void UpgradeCostsCompound()
        {
            ItemDefinition definition = MakeItem("thing", EquipmentSlot.Head);

            Assert.AreEqual(100, definition.UpgradeCost(0));
            Assert.AreEqual(200, definition.UpgradeCost(1));
            Assert.AreEqual(400, definition.UpgradeCost(2));
            Assert.AreEqual(0, definition.UpgradeCost(definition.MaxUpgradeLevel), "maxed items cost nothing");
        }

        [Test]
        public void UpgradingSpendsEmbersAndStopsAtTheCap()
        {
            var inventory = new Inventory();
            ItemDefinition definition = MakeItem("thing", EquipmentSlot.Head);
            definition.MaxUpgradeLevel = 1;

            var item = new ItemInstance(definition);

            Assert.IsFalse(inventory.TryUpgrade(item), "no embers, no upgrade");
            Assert.AreEqual(0, item.UpgradeLevel);

            inventory.AddEmbers(100);
            Assert.IsTrue(inventory.TryUpgrade(item));
            Assert.AreEqual(1, item.UpgradeLevel);
            Assert.AreEqual(0, inventory.Embers);

            inventory.AddEmbers(10000);
            Assert.IsFalse(inventory.TryUpgrade(item), "already at MaxUpgradeLevel");
            Assert.AreEqual(10000, inventory.Embers, "a refused upgrade must not charge");
        }

        [Test]
        public void FailedUpgradeDoesNotConsumeEmbers()
        {
            var inventory = new Inventory();
            inventory.AddEmbers(99);

            var item = new ItemInstance(MakeItem("thing", EquipmentSlot.Head));

            Assert.IsFalse(inventory.TryUpgrade(item), "one ember short");
            Assert.AreEqual(99, inventory.Embers);
            Assert.AreEqual(0, item.UpgradeLevel);
        }

        [Test]
        public void UpgradeCandidateIsTheCheapestLowestLevelItem()
        {
            var inventory = new Inventory();
            inventory.AddEmbers(1000);

            var equipment = new Equipment();
            var fresh = new ItemInstance(MakeItem("fresh", EquipmentSlot.Head));
            var invested = new ItemInstance(MakeItem("invested", EquipmentSlot.Chest), upgradeLevel: 2);

            equipment.Equip(fresh);
            equipment.Equip(invested);

            ItemInstance candidate = inventory.FindBestUpgradeCandidate(new[] { equipment });
            Assert.AreSame(fresh, candidate, "the +0 item is both cheaper and a bigger relative gain");
        }

        // --- loot rolling ----------------------------------------------------------------

        private LootTable MakeTable(params ItemDefinition[] items)
        {
            var table = Make<LootTable>("table");
            table.MinEmbers = 5;
            table.MaxEmbers = 5;
            table.AlwaysDrops = true;
            table.Rolls = 1;
            table.Entries = new List<LootEntry>();

            foreach (ItemDefinition item in items)
                table.Entries.Add(new LootEntry { Item = item, Weight = 1f, MinUpgrade = 0, MaxUpgrade = 4 });

            return table;
        }

        [Test]
        public void LootRollsAreDeterministicForASeed()
        {
            LootTable table = MakeTable(
                MakeItem("a", EquipmentSlot.Head),
                MakeItem("b", EquipmentSlot.Chest),
                MakeItem("c", EquipmentSlot.Feet));
            table.Rolls = 5;

            var first = new List<LootRoll>();
            var second = new List<LootRoll>();

            table.Roll(new DeterministicRandom(4242), first, out int embersA);
            table.Roll(new DeterministicRandom(4242), second, out int embersB);

            Assert.AreEqual(embersA, embersB);
            Assert.AreEqual(first.Count, second.Count);

            for (int i = 0; i < first.Count; i++)
            {
                Assert.AreSame(first[i].Item, second[i].Item, $"roll {i} differed");
                Assert.AreEqual(first[i].UpgradeLevel, second[i].UpgradeLevel);
            }
        }

        [Test]
        public void DepthRaisesTheFloorOfWhatDrops()
        {
            LootTable table = MakeTable(MakeItem("a", EquipmentSlot.Head));
            table.Rolls = 12;

            var shallow = new List<LootRoll>();
            var deep = new List<LootRoll>();

            table.Roll(new DeterministicRandom(7), shallow, out _, depth: 0f);
            table.Roll(new DeterministicRandom(7), deep, out _, depth: 1f);

            int shallowMin = int.MaxValue, deepMin = int.MaxValue;
            foreach (LootRoll roll in shallow) shallowMin = Mathf.Min(shallowMin, roll.UpgradeLevel);
            foreach (LootRoll roll in deep) deepMin = Mathf.Min(deepMin, roll.UpgradeLevel);

            Assert.AreEqual(4, deepMin, "at full depth every roll should be at the entry's ceiling");
            Assert.Less(shallowMin, deepMin, "a shallow room should be able to roll worse");
        }

        [Test]
        public void RollNeverExceedsTheItemsOwnUpgradeCap()
        {
            ItemDefinition definition = MakeItem("capped", EquipmentSlot.Head);
            definition.MaxUpgradeLevel = 1;

            LootTable table = MakeTable(definition);
            table.Rolls = 20;

            var rolls = new List<LootRoll>();
            table.Roll(new DeterministicRandom(11), rolls, out _, depth: 1f);

            foreach (LootRoll roll in rolls)
                Assert.LessOrEqual(roll.UpgradeLevel, 1, "a table must not out-roll the item's own cap");
        }

        [Test]
        public void EmptyTableDropsNothingButStillPaysEmbers()
        {
            var table = Make<LootTable>("empty");
            table.MinEmbers = 3;
            table.MaxEmbers = 3;
            table.AlwaysDrops = true;
            table.Entries = new List<LootEntry>();

            var rolls = new List<LootRoll>();
            table.Roll(new DeterministicRandom(1), rolls, out int embers);

            Assert.IsEmpty(rolls);
            Assert.AreEqual(3, embers);
        }
    }
}
