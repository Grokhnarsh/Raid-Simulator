using System.Collections.Generic;
using EmberDepths.Content;
using UnityEditor;
using UnityEngine;

namespace EmberDepths.EditorTools
{
    /// <summary>
    /// Generates the item, set and loot-table assets.
    ///
    /// Like the encounter content, this is authored in code while the numbers
    /// are still moving: three sets and twenty-four items spread across the
    /// inspector are almost impossible to review, and here the whole reward
    /// economy — every stat, every upgrade slope, every drop rate — is one file
    /// you can read end to end.
    ///
    /// The design it encodes, in short:
    ///
    ///  * Baseline items are Common, weak, and exist so no slot is ever empty.
    ///  * Three six-piece sets each answer one role, and each is *worse* per
    ///    piece than the best standalone gear. Wearing four of them is a choice
    ///    the player makes against their own item level, which is what makes it
    ///    a decision rather than an inevitability.
    ///  * Embers, not drops, are the main power curve. Trash always pays.
    /// </summary>
    public static class ItemContentBuilder
    {
        /// <summary>Everything this builder produced, so the caller can wire it up.</summary>
        public sealed class Catalogue
        {
            public readonly List<ItemDefinition> Baseline = new List<ItemDefinition>();
            public readonly List<ItemDefinition> SetItems = new List<ItemDefinition>();

            public ItemSetDefinition Emberward;
            public ItemSetDefinition Ashstrider;
            public ItemSetDefinition Tidecaller;

            public LootTable TrashLoot;
            public LootTable EliteLoot;
            public LootTable BossLoot;
            public LootTable TreasureLoot;

            public ItemDefinition Find(string displayName) =>
                Baseline.Find(i => i.DisplayName == displayName)
                ?? SetItems.Find(i => i.DisplayName == displayName);
        }

        private static readonly Color EmberwardTint = new Color(0.95f, 0.55f, 0.20f);
        private static readonly Color AshstriderTint = new Color(0.60f, 0.70f, 0.78f);
        private static readonly Color TidecallerTint = new Color(0.36f, 0.78f, 0.76f);

        [MenuItem("EmberDepths/Content/Build Items and Sets", priority = 61)]
        public static void BuildStandalone()
        {
            Catalogue catalogue = Build();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EmberDepths] Built {catalogue.Baseline.Count} baseline items, " +
                      $"{catalogue.SetItems.Count} set pieces, 3 sets and 4 loot tables.");
        }

        public static Catalogue Build()
        {
            foreach (string folder in new[] { "Items", "Sets", "Loot", "Status" })
                ContentAssetUtil.EnsureFolder($"{ContentAssetUtil.Root}/{folder}");

            var catalogue = new Catalogue();

            BuildBaseline(catalogue);
            BuildEmberwardSet(catalogue);
            BuildAshstriderSet(catalogue);
            BuildTidecallerSet(catalogue);
            BuildLootTables(catalogue);

            return catalogue;
        }

        // ======================================================================
        // Baseline gear
        // ======================================================================

        private static void BuildBaseline(Catalogue catalogue)
        {
            // Deliberately unexciting. These exist so that every slot has
            // something in it from the first minute, which makes the first real
            // drop read as an upgrade rather than as "finally, an item".
            catalogue.Baseline.Add(Item("IT_ChippedBlade", "Chipped Basalt Blade", EquipmentSlot.Weapon,
                ItemRarity.Common, 1, null,
                StatModifier.Flat(StatKind.Power, 2f, 0.6f)));

            catalogue.Baseline.Add(Item("IT_SootHood", "Soot-Stained Hood", EquipmentSlot.Head,
                ItemRarity.Common, 1, null,
                StatModifier.Flat(StatKind.MaxHealth, 12f, 4f),
                StatModifier.Flat(StatKind.FireResist, 0.03f, 0.01f)));

            catalogue.Baseline.Add(Item("IT_ScorchedVest", "Scorched Vest", EquipmentSlot.Chest,
                ItemRarity.Common, 1, null,
                StatModifier.Flat(StatKind.MaxHealth, 20f, 6f),
                StatModifier.Flat(StatKind.Armor, 8f, 3f)));

            catalogue.Baseline.Add(Item("IT_AshGloves", "Ash-Worn Gloves", EquipmentSlot.Hands,
                ItemRarity.Common, 1, null,
                StatModifier.Flat(StatKind.CritChance, 0.02f, 0.005f)));

            catalogue.Baseline.Add(Item("IT_CinderBoots", "Cinder-Tread Boots", EquipmentSlot.Feet,
                ItemRarity.Common, 1, null,
                StatModifier.Flat(StatKind.MoveSpeed, 0.2f, 0.05f)));

            catalogue.Baseline.Add(Item("IT_EmberShard", "Ember Shard", EquipmentSlot.Trinket,
                ItemRarity.Uncommon, 2, null,
                StatModifier.Flat(StatKind.Power, 1f, 0.4f),
                StatModifier.Flat(StatKind.FireResist, 0.05f, 0.015f)));
        }

        // ======================================================================
        // Emberward — the tank set, and the biome's intended answer to fire
        // ======================================================================

        private static void BuildEmberwardSet(Catalogue catalogue)
        {
            StatusEffectDefinition mantle = Aura("ST_EmberwardMantle", "Emberward Mantle",
                "Six pieces of the Emberward. The heat parts around you.",
                EmberwardTint,
                damageTaken: 0.88f, threatFriendly: true);

            var set = ContentAssetUtil.Asset<ItemSetDefinition>("Sets/SET_Emberward");
            set.DisplayName = "The Emberward";
            set.Flavour = "Plate quenched in the lake it was forged beside.";
            set.Tint = EmberwardTint;
            catalogue.Emberward = set;

            set.Tiers = new List<SetBonusTier>
            {
                new SetBonusTier
                {
                    RequiredPieces = 2,
                    Description = "+12% Armor",
                    Modifiers = new List<StatModifier> { StatModifier.Percent(StatKind.Armor, 0.12f) }
                },
                new SetBonusTier
                {
                    RequiredPieces = 4,
                    Description = "+20% Fire Resistance",
                    Modifiers = new List<StatModifier> { StatModifier.Flat(StatKind.FireResist, 0.20f) }
                },
                new SetBonusTier
                {
                    RequiredPieces = 6,
                    Description = "Emberward Mantle: 12% less damage taken, and far more threat",
                    Aura = mantle
                }
            };

            set.Members = new List<ItemDefinition>
            {
                SetItem(catalogue, set, "EW_Bulwark", "Emberward Bulwark", EquipmentSlot.Weapon,
                    StatModifier.Flat(StatKind.Armor, 18f, 5f),
                    StatModifier.Flat(StatKind.ThreatModifier, 0.5f, 0.15f)),

                SetItem(catalogue, set, "EW_Helm", "Emberward Helm", EquipmentSlot.Head,
                    StatModifier.Flat(StatKind.MaxHealth, 30f, 9f),
                    StatModifier.Flat(StatKind.Armor, 10f, 3f)),

                SetItem(catalogue, set, "EW_Cuirass", "Emberward Cuirass", EquipmentSlot.Chest,
                    StatModifier.Flat(StatKind.MaxHealth, 55f, 16f),
                    StatModifier.Flat(StatKind.Armor, 22f, 6f)),

                SetItem(catalogue, set, "EW_Gauntlets", "Emberward Gauntlets", EquipmentSlot.Hands,
                    StatModifier.Flat(StatKind.Armor, 12f, 4f),
                    StatModifier.Flat(StatKind.ThreatModifier, 0.3f, 0.1f)),

                SetItem(catalogue, set, "EW_Greaves", "Emberward Greaves", EquipmentSlot.Feet,
                    StatModifier.Flat(StatKind.Armor, 10f, 3f),
                    StatModifier.Flat(StatKind.MaxHealth, 18f, 5f)),

                SetItem(catalogue, set, "EW_Sigil", "Emberward Sigil", EquipmentSlot.Trinket,
                    StatModifier.Flat(StatKind.FireResist, 0.08f, 0.02f),
                    StatModifier.Flat(StatKind.DamageTaken, -0.03f, -0.01f))
            };

            ContentAssetUtil.Save(set);
        }

        // ======================================================================
        // Ashstrider — crit and movement, for whoever is doing the killing
        // ======================================================================

        private static void BuildAshstriderSet(Catalogue catalogue)
        {
            StatusEffectDefinition quickened = Aura("ST_Quickened", "Quickened",
                "Six pieces of the Ashstrider. Nothing in the depths is faster.",
                AshstriderTint,
                haste: 1.15f);

            var set = ContentAssetUtil.Asset<ItemSetDefinition>("Sets/SET_Ashstrider");
            set.DisplayName = "The Ashstrider";
            set.Flavour = "Leathers cut for someone who intends to leave.";
            set.Tint = AshstriderTint;
            catalogue.Ashstrider = set;

            set.Tiers = new List<SetBonusTier>
            {
                new SetBonusTier
                {
                    RequiredPieces = 2,
                    Description = "+8% Move Speed",
                    Modifiers = new List<StatModifier> { StatModifier.Percent(StatKind.MoveSpeed, 0.08f) }
                },
                new SetBonusTier
                {
                    RequiredPieces = 4,
                    Description = "+10% Crit Chance",
                    Modifiers = new List<StatModifier> { StatModifier.Flat(StatKind.CritChance, 0.10f) }
                },
                new SetBonusTier
                {
                    RequiredPieces = 6,
                    Description = "Quickened: +15% Haste",
                    Aura = quickened
                }
            };

            set.Members = new List<ItemDefinition>
            {
                SetItem(catalogue, set, "AS_Fangs", "Ashstrider Fangs", EquipmentSlot.Weapon,
                    StatModifier.Flat(StatKind.Power, 5f, 1.5f),
                    StatModifier.Flat(StatKind.CritChance, 0.04f, 0.01f)),

                SetItem(catalogue, set, "AS_Cowl", "Ashstrider Cowl", EquipmentSlot.Head,
                    StatModifier.Flat(StatKind.CritChance, 0.03f, 0.008f),
                    StatModifier.Flat(StatKind.MaxHealth, 14f, 4f)),

                SetItem(catalogue, set, "AS_Jerkin", "Ashstrider Jerkin", EquipmentSlot.Chest,
                    StatModifier.Flat(StatKind.MaxHealth, 22f, 7f),
                    StatModifier.Flat(StatKind.MoveSpeed, 0.15f, 0.04f)),

                SetItem(catalogue, set, "AS_Wraps", "Ashstrider Wraps", EquipmentSlot.Hands,
                    StatModifier.Flat(StatKind.Power, 3f, 1f),
                    StatModifier.Flat(StatKind.Haste, 0.05f, 0.015f)),

                SetItem(catalogue, set, "AS_Treads", "Ashstrider Treads", EquipmentSlot.Feet,
                    StatModifier.Flat(StatKind.MoveSpeed, 0.35f, 0.08f)),

                SetItem(catalogue, set, "AS_Charm", "Ashstrider Charm", EquipmentSlot.Trinket,
                    StatModifier.Flat(StatKind.CritMultiplier, 0.25f, 0.08f))
            };

            ContentAssetUtil.Save(set);
        }

        // ======================================================================
        // Tidecaller — power and resource, for the back line
        // ======================================================================

        private static void BuildTidecallerSet(Catalogue catalogue)
        {
            StatusEffectDefinition focus = Aura("ST_DeepFocus", "Deep Focus",
                "Six pieces of the Tidecaller. The cold keeps its shape here.",
                TidecallerTint,
                damageDealt: 1.20f);

            var set = ContentAssetUtil.Asset<ItemSetDefinition>("Sets/SET_Tidecaller");
            set.DisplayName = "The Tidecaller's Vigil";
            set.Flavour = "Vestments that remember water.";
            set.Tint = TidecallerTint;
            catalogue.Tidecaller = set;

            set.Tiers = new List<SetBonusTier>
            {
                new SetBonusTier
                {
                    RequiredPieces = 2,
                    Description = "+12% Power",
                    Modifiers = new List<StatModifier> { StatModifier.Percent(StatKind.Power, 0.12f) }
                },
                new SetBonusTier
                {
                    RequiredPieces = 4,
                    Description = "+25% Max Resource and +30% Resource Regeneration",
                    Modifiers = new List<StatModifier>
                    {
                        StatModifier.Percent(StatKind.MaxResource, 0.25f),
                        StatModifier.Percent(StatKind.ResourceRegen, 0.30f)
                    }
                },
                new SetBonusTier
                {
                    RequiredPieces = 6,
                    Description = "Deep Focus: +20% damage and healing",
                    Aura = focus
                }
            };

            set.Members = new List<ItemDefinition>
            {
                SetItem(catalogue, set, "TC_Focus", "Tidecaller Focus", EquipmentSlot.Weapon,
                    StatModifier.Flat(StatKind.Power, 6f, 1.8f)),

                SetItem(catalogue, set, "TC_Diadem", "Tidecaller Diadem", EquipmentSlot.Head,
                    StatModifier.Flat(StatKind.MaxResource, 25f, 8f),
                    StatModifier.Flat(StatKind.MaxHealth, 10f, 3f)),

                SetItem(catalogue, set, "TC_Vestment", "Tidecaller Vestment", EquipmentSlot.Chest,
                    StatModifier.Flat(StatKind.MaxHealth, 18f, 6f),
                    StatModifier.Flat(StatKind.ArcaneResist, 0.05f, 0.015f)),

                SetItem(catalogue, set, "TC_Gloves", "Tidecaller Gloves", EquipmentSlot.Hands,
                    StatModifier.Flat(StatKind.Haste, 0.08f, 0.02f)),

                SetItem(catalogue, set, "TC_Slippers", "Tidecaller Slippers", EquipmentSlot.Feet,
                    StatModifier.Flat(StatKind.ResourceRegen, 3f, 1f),
                    StatModifier.Flat(StatKind.MoveSpeed, 0.1f, 0.03f)),

                SetItem(catalogue, set, "TC_Tidestone", "Tidestone", EquipmentSlot.Trinket,
                    StatModifier.Flat(StatKind.Power, 4f, 1.2f),
                    StatModifier.Flat(StatKind.FrostResist, 0.08f, 0.02f))
            };

            ContentAssetUtil.Save(set);
        }

        // ======================================================================
        // Loot tables
        // ======================================================================

        private static void BuildLootTables(Catalogue catalogue)
        {
            // Trash rarely drops gear but always pays embers. That is the whole
            // economy: clearing a room reliably buys an upgrade, while finding
            // an actual item stays an event.
            catalogue.TrashLoot = LootTableFor("Loot/LT_Trash", 3, 8, dropChance: 0.16f, rolls: 1,
                alwaysDrops: false, baselineWeight: 5f, setWeight: 1f, maxUpgrade: 1, catalogue);

            catalogue.EliteLoot = LootTableFor("Loot/LT_Elite", 18, 34, dropChance: 0.65f, rolls: 1,
                alwaysDrops: false, baselineWeight: 1f, setWeight: 4f, maxUpgrade: 2, catalogue);

            catalogue.TreasureLoot = LootTableFor("Loot/LT_Treasure", 45, 80, dropChance: 1f, rolls: 2,
                alwaysDrops: true, baselineWeight: 1f, setWeight: 3f, maxUpgrade: 2, catalogue);

            // The boss is the only guaranteed source of several set pieces at
            // once, which is what makes a four-piece bonus reachable in one run.
            catalogue.BossLoot = LootTableFor("Loot/LT_Boss", 90, 150, dropChance: 1f, rolls: 3,
                alwaysDrops: true, baselineWeight: 0f, setWeight: 1f, maxUpgrade: 3, catalogue);
        }

        private static LootTable LootTableFor(
            string path, int minEmbers, int maxEmbers, float dropChance, int rolls, bool alwaysDrops,
            float baselineWeight, float setWeight, int maxUpgrade, Catalogue catalogue)
        {
            var table = ContentAssetUtil.Asset<LootTable>(path);
            table.MinEmbers = minEmbers;
            table.MaxEmbers = maxEmbers;
            table.DropChance = dropChance;
            table.Rolls = rolls;
            table.AlwaysDrops = alwaysDrops;
            table.Entries = new List<LootEntry>();

            if (baselineWeight > 0f)
                foreach (ItemDefinition item in catalogue.Baseline)
                    table.Entries.Add(Entry(item, baselineWeight, maxUpgrade));

            if (setWeight > 0f)
                foreach (ItemDefinition item in catalogue.SetItems)
                    table.Entries.Add(Entry(item, setWeight, maxUpgrade));

            ContentAssetUtil.Save(table);
            return table;
        }

        private static LootEntry Entry(ItemDefinition item, float weight, int maxUpgrade) => new LootEntry
        {
            Item = item,
            Weight = weight,
            MinUpgrade = 0,
            MaxUpgrade = Mathf.Min(maxUpgrade, item.MaxUpgradeLevel)
        };

        // ======================================================================
        // Helpers
        // ======================================================================

        private static ItemDefinition Item(
            string assetName, string display, EquipmentSlot slot, ItemRarity rarity,
            int itemLevel, ItemSetDefinition set, params StatModifier[] modifiers)
        {
            var item = ContentAssetUtil.Asset<ItemDefinition>($"Items/{assetName}");
            item.DisplayName = display;
            item.Slot = slot;
            item.Rarity = rarity;
            item.ItemLevel = itemLevel;
            item.Set = set;
            item.Modifiers = new List<StatModifier>(modifiers);
            item.MaxUpgradeLevel = 5;

            // Rarer gear costs more to improve, so the cheap early upgrade is
            // always the common item — which is exactly when the player has the
            // fewest embers.
            item.BaseUpgradeCost = 25 + (int)rarity * 18;
            item.UpgradeCostGrowth = 1.55f;
            item.Icon = IconFor(slot, assetName);

            ContentAssetUtil.Save(item);
            return item;
        }

        private static ItemDefinition SetItem(
            Catalogue catalogue, ItemSetDefinition set, string assetName, string display,
            EquipmentSlot slot, params StatModifier[] modifiers)
        {
            ItemDefinition item = Item(assetName, display, slot, ItemRarity.Rare, 6, set, modifiers);
            catalogue.SetItems.Add(item);
            return item;
        }

        /// <summary>
        /// Picks the generated icon for a slot, with per-set overrides for the
        /// weapons so the three sets are distinguishable on the floor. Returns
        /// null when the art has not been fetched yet — items must still build
        /// on a clone with no Art folder.
        /// </summary>
        private static Sprite IconFor(EquipmentSlot slot, string assetName)
        {
            string specific = assetName switch
            {
                "EW_Bulwark" => "icon_shield",
                "AS_Fangs" => "icon_daggers",
                "TC_Focus" => "icon_staff",
                _ => null
            };

            if (specific != null)
            {
                Sprite special = ContentAssetUtil.FindExact<Sprite>(specific);
                if (special != null) return special;
            }

            return ContentAssetUtil.FindExact<Sprite>($"icon_{slot.ToString().ToLowerInvariant()}");
        }

        /// <summary>
        /// A permanent, undispellable buff used as a set-bonus payload.
        ///
        /// Expressing the six-piece as a status rather than as more stat lines
        /// means it can show up in the buff list, tint the character, and later
        /// carry behaviour that numbers cannot.
        /// </summary>
        private static StatusEffectDefinition Aura(
            string assetName, string display, string description, Color tint,
            float damageTaken = 1f, float damageDealt = 1f, float haste = 1f, bool threatFriendly = false)
        {
            var aura = ContentAssetUtil.Asset<StatusEffectDefinition>($"Status/{assetName}");
            aura.DisplayName = display;
            aura.Description = description;
            aura.Tint = tint;
            aura.IsDebuff = false;
            aura.Dispellable = false;
            aura.MaxStacks = 1;
            aura.TickInterval = 0f;
            aura.DamageTakenMultiplier = damageTaken;
            aura.DamageDealtMultiplier = damageDealt;
            aura.HasteMultiplier = haste;
            aura.MoveSpeedMultiplier = 1f;

            // A tank set that reduces damage but not threat would make the tank
            // harder to kill and no better at holding aggro, which is the wrong
            // half of the job.
            if (threatFriendly) aura.ArmorBonus = 15f;

            ContentAssetUtil.Save(aura);
            return aura;
        }
    }
}
