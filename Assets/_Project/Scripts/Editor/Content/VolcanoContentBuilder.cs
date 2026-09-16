using System.Collections.Generic;
using EmberDepths.Content;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EmberDepths.EditorTools
{
    /// <summary>
    /// Generates the whole volcano dungeon as ScriptableObject assets.
    ///
    /// Why build content in code rather than hand-authoring it? Because at this
    /// stage the *shape* of the content is still changing, and forty inspector-
    /// authored assets are the hardest thing in a Unity project to keep
    /// consistent or to review. Here the entire encounter design — every
    /// coefficient, every telegraph time, every phase threshold — is one file you
    /// can read top to bottom and diff.
    ///
    /// This is scaffolding, not a permanent fixture. Once the numbers settle,
    /// designers own the .asset files and this becomes a one-time bootstrap. It
    /// updates existing assets in place rather than replacing them, so hand
    /// tweaks to fields it does not set are preserved, and every reference from
    /// prefabs and scenes survives a re-run.
    /// </summary>
    public static class VolcanoContentBuilder
    {
        private const string Root = "Assets/_Project/Content";

        private static readonly Color Ember = new Color(1f, 0.48f, 0.11f);
        private static readonly Color Teal = new Color(0.36f, 0.78f, 0.76f);

        [MenuItem("EmberDepths/Content/Build Volcano Dungeon", priority = 60)]
        public static void Build()
        {
            foreach (string folder in new[]
                     {
                         "Status", "Abilities", "Enemies", "Classes",
                         "Encounters", "Biomes", "Dungeons", "Tiles", "Visuals"
                     })
            {
                ArtTools.EnsureFolder($"{Root}/{folder}");
            }

            var statuses = BuildStatuses();
            var hazards = BuildHazards(statuses);

            // Items first: enemies need loot tables and classes need starting
            // gear, and both are cheaper to wire here than to re-link later.
            ItemContentBuilder.Catalogue items = ItemContentBuilder.Build();

            var enemies = BuildEnemies(statuses, hazards);
            AssignLoot(enemies, items);

            var classes = BuildClasses(statuses);
            AssignStartingGear(classes, items);

            var encounters = BuildEncounters(enemies, hazards);
            BiomeDefinition biome = BuildBiome(statuses, hazards, encounters, enemies);
            DungeonDefinition dungeon = BuildDungeon(biome);
            BuildRoster(classes);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[EmberDepths] Volcano content built under {Root}. " +
                      $"Dungeon asset: {AssetDatabase.GetAssetPath(dungeon)}");
        }

        // ==========================================================================
        // Statuses
        // ==========================================================================

        private sealed class Statuses
        {
            public StatusEffectDefinition Burning;
            public StatusEffectDefinition Molten;
            public StatusEffectDefinition Chilled;
            public StatusEffectDefinition Stunned;
            public StatusEffectDefinition EmberWard;
            public StatusEffectDefinition Enrage;
            public StatusEffectDefinition PhaseAura;
        }

        private static Statuses BuildStatuses()
        {
            var s = new Statuses();

            // The signature debuff of the biome. Stacks hard, so standing in the
            // wrong place twice costs much more than standing in it once.
            s.Burning = Asset<StatusEffectDefinition>("Status/ST_Burning");
            s.Burning.DisplayName = "Burning";
            s.Burning.Description = "Searing damage over time. Stacks.";
            s.Burning.Tint = Ember;
            s.Burning.IsDebuff = true;
            s.Burning.Dispellable = true;
            s.Burning.MaxStacks = 5;
            s.Burning.ReapplyRefreshesDuration = true;
            s.Burning.TickInterval = 1f;
            s.Burning.DamagePerTick = 1.2f;
            s.Burning.TickDamageType = DamageType.Fire;
            Save(s.Burning);

            s.Molten = Asset<StatusEffectDefinition>("Status/ST_Molten");
            s.Molten.DisplayName = "Molten";
            s.Molten.Description = "Armour softened by heat. Takes more damage.";
            s.Molten.Tint = new Color(1f, 0.3f, 0.1f);
            s.Molten.MaxStacks = 3;
            s.Molten.TickInterval = 0f;
            s.Molten.DamageTakenMultiplier = 1.12f;
            Save(s.Molten);

            s.Chilled = Asset<StatusEffectDefinition>("Status/ST_Chilled");
            s.Chilled.DisplayName = "Chilled";
            s.Chilled.Description = "Slowed by biting frost.";
            s.Chilled.Tint = new Color(0.45f, 0.8f, 0.95f);
            s.Chilled.MaxStacks = 2;
            s.Chilled.TickInterval = 0f;
            s.Chilled.MoveSpeedMultiplier = 0.72f;
            Save(s.Chilled);

            s.Stunned = Asset<StatusEffectDefinition>("Status/ST_Stunned");
            s.Stunned.DisplayName = "Stunned";
            s.Stunned.Tint = new Color(1f, 0.9f, 0.4f);
            s.Stunned.Dispellable = false;
            s.Stunned.TickInterval = 0f;
            s.Stunned.Stuns = true;
            Save(s.Stunned);

            // The party's structural answer to a fire dungeon. Resistance is the
            // stat the whole biome is balanced against.
            s.EmberWard = Asset<StatusEffectDefinition>("Status/ST_EmberWard");
            s.EmberWard.DisplayName = "Ember Ward";
            s.EmberWard.Description = "Warded against flame.";
            s.EmberWard.Tint = Teal;
            s.EmberWard.IsDebuff = false;
            s.EmberWard.MaxStacks = 1;
            s.EmberWard.TickInterval = 0f;
            s.EmberWard.FireResistBonus = 0.35f;
            Save(s.EmberWard);

            s.Enrage = Asset<StatusEffectDefinition>("Status/ST_Enrage");
            s.Enrage.DisplayName = "Enrage";
            s.Enrage.Description = "Growing fury. The fight has gone on too long.";
            s.Enrage.Tint = new Color(1f, 0.15f, 0.05f);
            s.Enrage.IsDebuff = false;
            s.Enrage.Dispellable = false;
            s.Enrage.MaxStacks = 99;
            s.Enrage.TickInterval = 0f;
            s.Enrage.DamageDealtMultiplier = 1.06f;
            Save(s.Enrage);

            s.PhaseAura = Asset<StatusEffectDefinition>("Status/ST_MoltenCore");
            s.PhaseAura.DisplayName = "Molten Core";
            s.PhaseAura.Description = "The tyrant's heart burns hotter.";
            s.PhaseAura.Tint = Ember;
            s.PhaseAura.IsDebuff = false;
            s.PhaseAura.Dispellable = false;
            s.PhaseAura.MaxStacks = 1;
            s.PhaseAura.TickInterval = 0f;
            s.PhaseAura.DamageDealtMultiplier = 1.15f;
            Save(s.PhaseAura);

            return s;
        }

        // ==========================================================================
        // Hazards
        // ==========================================================================

        private sealed class Hazards
        {
            public HazardDefinition StandingLava;
            public HazardDefinition FirePool;
            public HazardDefinition MeteorScar;
        }

        private static Hazards BuildHazards(Statuses statuses)
        {
            var h = new Hazards();

            // Terrain lava. Deliberately survivable for a couple of seconds: it
            // should shape where the party stands, not delete anyone who clips it.
            h.StandingLava = Asset<HazardDefinition>("Status/HZ_StandingLava");
            h.StandingLava.DisplayName = "Standing Lava";
            h.StandingLava.Tint = new Color(1f, 0.45f, 0.1f, 0.55f);
            h.StandingLava.TickInterval = 0.5f;
            h.StandingLava.PercentDamagePerTick = 0.015f;
            h.StandingLava.FlatDamagePerTick = 1f;
            h.StandingLava.DamageType = DamageType.Fire;
            h.StandingLava.AppliesStatus = statuses.Burning;
            h.StandingLava.StatusDuration = 3f;
            h.StandingLava.FriendlyFire = true;
            h.StandingLava.LavaWalkersImmune = true;

            // Terrain lava is spawned as one radius-0 hazard per lava cell, so
            // merging must be off: a merge only refreshes the existing hazard's
            // duration, it does not widen its cell list, which would leave a 3x3
            // pool with exactly one dangerous tile.
            h.StandingLava.MergeWithSameType = false;

            // GridMap.CostToEnter already surcharges Lava terrain by 8. This adds
            // enough on top to register the cell as dangerous for the AI's
            // step-out-of-fire check without double-counting the avoidance.
            h.StandingLava.PathingPenalty = 4f;
            Save(h.StandingLava);

            h.FirePool = Asset<HazardDefinition>("Status/HZ_FirePool");
            h.FirePool.DisplayName = "Fire Pool";
            h.FirePool.Tint = new Color(1f, 0.55f, 0.15f, 0.6f);
            h.FirePool.TickInterval = 0.5f;
            h.FirePool.PercentDamagePerTick = 0.025f;
            h.FirePool.FlatDamagePerTick = 2f;
            h.FirePool.DamageType = DamageType.Fire;
            h.FirePool.AppliesStatus = statuses.Burning;
            h.FirePool.StatusDuration = 4f;
            h.FirePool.FriendlyFire = true;
            h.FirePool.LavaWalkersImmune = true;
            h.FirePool.PathingPenalty = 8f;
            Save(h.FirePool);

            h.MeteorScar = Asset<HazardDefinition>("Status/HZ_MeteorScar");
            h.MeteorScar.DisplayName = "Meteor Scar";
            h.MeteorScar.Tint = new Color(1f, 0.32f, 0.06f, 0.7f);
            h.MeteorScar.TickInterval = 0.5f;
            h.MeteorScar.PercentDamagePerTick = 0.04f;
            h.MeteorScar.FlatDamagePerTick = 4f;
            h.MeteorScar.DamageType = DamageType.Fire;
            h.MeteorScar.AppliesStatus = statuses.Molten;
            h.MeteorScar.StatusDuration = 6f;
            h.MeteorScar.FriendlyFire = true;
            h.MeteorScar.PathingPenalty = 14f;
            Save(h.MeteorScar);

            return h;
        }

        // ==========================================================================
        // Abilities
        // ==========================================================================

        private static AbilityDefinition Ability(
            string path, string display, string description,
            TargetKind targetKind, AffectsFaction affects, TargetShape shape,
            int range, float castTime, float cooldown, float telegraph,
            params AbilityEffect[] effects)
        {
            var a = Asset<AbilityDefinition>($"Abilities/{path}");
            a.DisplayName = display;
            a.Description = description;
            a.TargetKind = targetKind;
            a.AffectsFaction = affects;
            a.Shape = shape;
            a.Range = Mathf.Max(1, range);
            a.CastTime = castTime;
            a.Cooldown = cooldown;
            a.TelegraphLeadTime = telegraph;
            a.Effects = new List<AbilityEffect>(effects);
            Save(a);
            return a;
        }

        // ==========================================================================
        // Enemies
        // ==========================================================================

        private sealed class Enemies
        {
            public EnemyDefinition Imp;
            public EnemyDefinition Zealot;
            public EnemyDefinition Hound;
            public EnemyDefinition Brute;
            public EnemyDefinition Boss;
        }

        private static Enemies BuildEnemies(Statuses st, Hazards hz)
        {
            var e = new Enemies();

            // --- Ember Imp: cheap ranged chip damage, dies to a sneeze ------------
            AbilityDefinition impBolt = Ability(
                "EN_AB_EmberBolt", "Ember Bolt", "A spat gobbet of fire.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 5, castTime: 0.6f, cooldown: 2.2f, telegraph: 0f,
                new DamageEffect { Coefficient = 1.1f, DamageType = DamageType.Fire },
                new ApplyStatusEffect { Status = st.Burning, Duration = 4f, Stacks = 1 });

            e.Imp = Enemy("EN_EmberImp", "Ember Imp", EnemyRank.Trash, 64f, 6f,
                EnemyAiProfile.Caster, impBolt, null, ActorFlags.LavaWalker);
            e.Imp.Ai.PreferredRange = 4;
            e.Imp.BaseStats.MoveSpeed = 4.2f;
            e.Imp.BaseStats.FireResist = 0.9f;
            Save(e.Imp);

            // --- Ashborne Zealot: telegraphed line, punishes lazy positioning -----
            AbilityDefinition zealotBrand = Ability(
                "EN_AB_PyreBrand", "Pyre Brand", "Scorches the ground in a line.",
                TargetKind.Ground, AffectsFaction.Enemies, TargetShape.Line(5, 1),
                range: 6, castTime: 0.8f, cooldown: 8f, telegraph: 1.4f,
                new DamageEffect { Coefficient = 2.2f, DamageType = DamageType.Fire },
                new SpawnHazardEffect { Hazard = hz.FirePool, Radius = 0, Duration = 6f });

            AbilityDefinition zealotBolt = Ability(
                "EN_AB_CinderLash", "Cinder Lash", "A whip of live coals.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 6, castTime: 0.9f, cooldown: 3f, telegraph: 0f,
                new DamageEffect { Coefficient = 1.5f, DamageType = DamageType.Fire });

            e.Zealot = Enemy("EN_AshborneZealot", "Ashborne Zealot", EnemyRank.Trash, 90f, 8f,
                EnemyAiProfile.Caster, zealotBolt, new[] { zealotBrand }, ActorFlags.None);
            e.Zealot.BaseStats.FireResist = 0.75f;
            Save(e.Zealot);

            // --- Cinder Hound: fast melee, forces the party to hold formation -----
            AbilityDefinition houndBite = Ability(
                "EN_AB_Bite", "Bite", "Snapping jaws.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 1, castTime: 0f, cooldown: 1.4f, telegraph: 0f,
                new DamageEffect { Coefficient = 1.4f, DamageType = DamageType.Physical });

            AbilityDefinition houndPounce = Ability(
                "EN_AB_Pounce", "Pounce", "Leaps onto its target.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 6, castTime: 0.4f, cooldown: 10f, telegraph: 0.6f,
                new DashEffect(),
                new DamageEffect { Coefficient = 1.6f, DamageType = DamageType.Physical });

            e.Hound = Enemy("EN_CinderHound", "Cinder Hound", EnemyRank.Trash, 75f, 7f,
                EnemyAiProfile.Melee, houndBite, new[] { houndPounce }, ActorFlags.LavaWalker);
            e.Hound.BaseStats.MoveSpeed = 5.4f;
            e.Hound.BaseStats.FireResist = 0.8f;
            e.Hound.Ai.AggroRadius = 9;
            Save(e.Hound);

            // --- Magma Brute: an elite that must be tanked and interrupted --------
            AbilityDefinition bruteSlam = Ability(
                "EN_AB_Slam", "Slam", "A crushing overhead blow.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 1, castTime: 0f, cooldown: 2f, telegraph: 0f,
                new DamageEffect { Coefficient = 2.4f, DamageType = DamageType.Physical });

            AbilityDefinition bruteQuake = Ability(
                "EN_AB_MoltenQuake", "Molten Quake", "Shatters the floor in a wide ring.",
                TargetKind.Self, AffectsFaction.Enemies, TargetShape.Circle(3),
                range: 1, castTime: 0.5f, cooldown: 12f, telegraph: 1.8f,
                new DamageEffect { Coefficient = 3f, DamageType = DamageType.Fire },
                new KnockbackEffect { Tiles = 2, FromCaster = true },
                new ApplyStatusEffect { Status = st.Molten, Duration = 8f, Stacks = 1 });
            bruteQuake.AiMinTargets = 1;
            bruteQuake.AiPriority = 3f;
            Save(bruteQuake);

            e.Brute = Enemy("EN_MagmaBrute", "Magma Brute", EnemyRank.Elite, 130f, 12f,
                EnemyAiProfile.Melee, bruteSlam, new[] { bruteQuake }, ActorFlags.LavaWalker);
            e.Brute.VisualScale = 1.1f;
            e.Brute.BaseStats.Armor = 45f;
            e.Brute.BaseStats.FireResist = 0.9f;
            e.Brute.BaseStats.MoveSpeed = 2.6f;
            Save(e.Brute);

            e.Boss = BuildBoss(st, hz, e.Imp);
            return e;
        }

        private static EnemyDefinition Enemy(
            string assetName, string display, EnemyRank rank,
            float health, float power, EnemyAiProfile ai,
            AbilityDefinition basicAttack, AbilityDefinition[] abilities, ActorFlags flags)
        {
            var def = Asset<EnemyDefinition>($"Enemies/{assetName}");
            def.DisplayName = display;
            def.Rank = rank;
            def.Flags = flags;
            def.Ai = ai;
            def.BasicAttack = basicAttack;

            StatBlock stats = StatBlock.Default;
            stats.MaxHealth = health;
            stats.Power = power;
            stats.CritChance = 0.05f;
            stats.ThreatModifier = 1f;
            def.BaseStats = stats;

            // Abilities is a private serialised list; write it through the
            // SerializedObject so the asset stays the single source of truth.
            var so = new SerializedObject(def);
            SerializedProperty list = so.FindProperty("abilities");
            list.ClearArray();
            if (abilities != null)
            {
                for (int i = 0; i < abilities.Length; i++)
                {
                    list.InsertArrayElementAtIndex(i);
                    list.GetArrayElementAtIndex(i).objectReferenceValue = abilities[i];
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            def.Visuals = FindVisualSet(assetName.Replace("EN_", ""));
            Save(def);
            return def;
        }

        // ==========================================================================
        // Boss
        // ==========================================================================

        private static EnemyDefinition BuildBoss(Statuses st, Hazards hz, EnemyDefinition addDefinition)
        {
            AbilityDefinition swing = Ability(
                "BOSS_AB_Greatsword", "Greatsword", "A slow, heavy cleave.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 1, castTime: 0f, cooldown: 2.2f, telegraph: 0f,
                new DamageEffect { Coefficient = 2.6f, DamageType = DamageType.Physical });

            // Long telegraph, huge shape: the mechanic that teaches the party to
            // stand behind the boss rather than in front of it.
            AbilityDefinition cleave = Ability(
                "BOSS_AB_TyrantCleave", "Tyrant's Cleave", "A sweeping arc of molten steel.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Cone(4, 110f),
                range: 4, castTime: 0.6f, cooldown: 9f, telegraph: 1.6f,
                new DamageEffect { Coefficient = 3.2f, DamageType = DamageType.Physical },
                new ApplyStatusEffect { Status = st.Burning, Duration = 6f, Stacks = 2 });
            cleave.AiPriority = 2f;
            Save(cleave);

            AbilityDefinition meteor = Ability(
                "BOSS_AB_MeteorFall", "Meteor Fall", "Calls a burning rock down onto the arena.",
                TargetKind.Ground, AffectsFaction.Everyone, TargetShape.Circle(2),
                range: 12, castTime: 0.8f, cooldown: 14f, telegraph: 2.4f,
                new DamageEffect { Coefficient = 4f, DamageType = DamageType.Fire, EdgeFalloff = 0.6f },
                new SpawnHazardEffect { Hazard = hz.MeteorScar, Radius = 1, Duration = 12f });
            meteor.RequiresLineOfSight = false;
            meteor.AiPriority = 3f;
            Save(meteor);

            // Donut: the inverse mechanic. Safety is *close* to the boss, which
            // punishes ranged players who have parked at max distance.
            AbilityDefinition eruption = Ability(
                "BOSS_AB_EruptionRing", "Eruption Ring", "The floor splits in a ring. Close is safe.",
                TargetKind.Self, AffectsFaction.Enemies, TargetShape.Donut(2, 6),
                range: 1, castTime: 0.7f, cooldown: 16f, telegraph: 2.2f,
                new DamageEffect { Coefficient = 4.5f, DamageType = DamageType.Fire },
                new KnockbackEffect { Tiles = 1, FromCaster = true });
            eruption.AiPriority = 3.5f;
            Save(eruption);

            AbilityDefinition wave = Ability(
                "BOSS_AB_MagmaWave", "Magma Wave", "A rolling wall of lava.",
                TargetKind.Ground, AffectsFaction.Enemies, TargetShape.Line(9, 3),
                range: 9, castTime: 0.9f, cooldown: 13f, telegraph: 2f,
                new DamageEffect { Coefficient = 3.8f, DamageType = DamageType.Fire },
                new SpawnHazardEffect { Hazard = hz.FirePool, Radius = 1, Duration = 8f });
            wave.RequiresLineOfSight = false;
            wave.AiPriority = 3f;
            Save(wave);

            AbilityDefinition summon = Ability(
                "BOSS_AB_CallTheBrood", "Call the Brood", "Tears imps out of the fissures.",
                TargetKind.Self, AffectsFaction.Enemies, TargetShape.Single,
                range: 1, castTime: 1.2f, cooldown: 25f, telegraph: 0f,
                new SummonEffect { Enemy = addDefinition, Count = 3 });
            summon.AiPriority = 4f;
            Save(summon);

            var boss = Asset<EnemyDefinition>("Enemies/EN_Vulcanor");
            boss.DisplayName = "Vulcanor, the Ember Tyrant";
            boss.Description = "The thing the depths were dug to contain.";
            boss.Rank = EnemyRank.Boss;
            boss.Flags = ActorFlags.Unmovable | ActorFlags.LavaWalker;
            boss.Ai = EnemyAiProfile.BossProfile;
            boss.BasicAttack = swing;
            boss.VisualScale = 1f;

            StatBlock stats = StatBlock.Default;
            stats.MaxHealth = 260f;
            stats.Power = 16f;
            stats.Armor = 60f;
            stats.FireResist = 0.9f;
            stats.MoveSpeed = 2.4f;
            stats.CritChance = 0.08f;
            boss.BaseStats = stats;

            var so = new SerializedObject(boss);
            SerializedProperty list = so.FindProperty("abilities");
            list.ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();

            boss.Visuals = FindVisualSet("Vulcanor");
            boss.BossProfile = BuildBossPhases(st, hz, cleave, meteor, eruption, wave, summon);
            Save(boss);
            return boss;
        }

        private static BossDefinition BuildBossPhases(
            Statuses st, Hazards hz,
            AbilityDefinition cleave, AbilityDefinition meteor,
            AbilityDefinition eruption, AbilityDefinition wave, AbilityDefinition summon)
        {
            var profile = Asset<BossDefinition>("Enemies/BS_Vulcanor");
            profile.Title = "the Ember Tyrant";
            profile.IntroLine = "The heat has a shape, and it has noticed you.";
            profile.EnrageAfterSeconds = 300f;
            profile.EnrageStatus = st.Enrage;

            profile.Phases = new List<BossPhase>
            {
                // Phase 1 teaches the two basic rules: do not stand in front,
                // do not stand in the meteor.
                new BossPhase
                {
                    Name = "Ember Wrath",
                    EnterAtHealthFraction = 1f,
                    BannerText = "Vulcanor stirs",
                    AbilityInterval = 7f,
                    Abilities = new List<AbilityDefinition> { cleave, meteor }
                },

                // Phase 2 adds the inverted mechanic and adds. The party now has
                // to move together and handle a second target set at once.
                new BossPhase
                {
                    Name = "Molten Fury",
                    EnterAtHealthFraction = 0.65f,
                    BannerText = "The floor begins to split",
                    AbilityInterval = 6f,
                    HasteMultiplier = 1.15f,
                    Aura = st.PhaseAura,
                    Abilities = new List<AbilityDefinition> { cleave, meteor, eruption, summon },
                    OnEnter = new List<AbilityEffect>
                    {
                        new SpawnHazardEffect { Hazard = hz.FirePool, Radius = 2, Duration = 20f, AtCaster = true }
                    }
                },

                // Phase 3 is the damage check the enrage timer is measured against.
                new BossPhase
                {
                    Name = "Cataclysm",
                    EnterAtHealthFraction = 0.3f,
                    BannerText = "CATACLYSM",
                    AbilityInterval = 4.5f,
                    DamageMultiplier = 1.3f,
                    HasteMultiplier = 1.35f,
                    Aura = st.PhaseAura,
                    Abilities = new List<AbilityDefinition> { cleave, meteor, eruption, wave, summon },
                    OnEnter = new List<AbilityEffect>
                    {
                        new KnockbackEffect { Tiles = 3, FromCaster = true },
                        new SpawnHazardEffect { Hazard = hz.MeteorScar, Radius = 2, Duration = 25f, AtCaster = true }
                    }
                }
            };

            Save(profile);
            return profile;
        }

        // ==========================================================================
        // Rewards
        // ==========================================================================

        private static void AssignLoot(Enemies enemies, ItemContentBuilder.Catalogue items)
        {
            enemies.Imp.Loot = items.TrashLoot;
            enemies.Zealot.Loot = items.TrashLoot;
            enemies.Hound.Loot = items.TrashLoot;
            enemies.Brute.Loot = items.EliteLoot;
            enemies.Boss.Loot = items.BossLoot;

            Save(enemies.Imp);
            Save(enemies.Zealot);
            Save(enemies.Hound);
            Save(enemies.Brute);
            Save(enemies.Boss);
        }

        /// <summary>
        /// Two baseline pieces each, chosen to suit the role.
        ///
        /// Deliberately thin: the party should start visibly under-equipped so
        /// that the first set piece is a real event. Four empty slots also give
        /// early drops somewhere to land without a swap decision.
        /// </summary>
        private static void AssignStartingGear(Classes classes, ItemContentBuilder.Catalogue items)
        {
            void Give(ClassDefinition target, params string[] itemNames)
            {
                if (target == null) return;

                target.StartingGear = new List<ItemDefinition>();
                foreach (string itemName in itemNames)
                {
                    ItemDefinition item = items.Find(itemName);
                    if (item != null) target.StartingGear.Add(item);
                }

                Save(target);
            }

            Give(classes.Warden, "Scorched Vest", "Chipped Basalt Blade");
            Give(classes.Cleric, "Soot-Stained Hood", "Ember Shard");
            Give(classes.Ranger, "Cinder-Tread Boots", "Ash-Worn Gloves");
            Give(classes.Mage, "Soot-Stained Hood", "Ember Shard");
            Give(classes.Rogue, "Ash-Worn Gloves", "Cinder-Tread Boots");
        }

        // ==========================================================================
        // Classes
        // ==========================================================================

        private sealed class Classes
        {
            public ClassDefinition Warden, Cleric, Ranger, Mage, Rogue;
        }

        private static Classes BuildClasses(Statuses st)
        {
            var c = new Classes();

            // --- Warden (tank) ------------------------------------------------------
            AbilityDefinition shieldBash = Ability(
                "PC_AB_ShieldBash", "Shield Bash", "A punishing shove with the tower shield.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 1, castTime: 0f, cooldown: 1.2f, telegraph: 0f,
                new DamageEffect { Coefficient = 1.3f, DamageType = DamageType.Physical });
            shieldBash.ThreatMultiplier = 2.5f;
            Save(shieldBash);

            AbilityDefinition taunt = Ability(
                "PC_AB_Challenge", "Challenge", "Forces an enemy to attack you.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 6, castTime: 0f, cooldown: 8f, telegraph: 0f,
                new TauntEffect { Duration = 5f, BonusThreat = 400f });
            taunt.AiPriority = 5f;
            Save(taunt);

            AbilityDefinition emberGuard = Ability(
                "PC_AB_EmberGuard", "Ember Guard", "Braces against the heat.",
                TargetKind.Self, AffectsFaction.Allies, TargetShape.Single,
                range: 1, castTime: 0f, cooldown: 20f, telegraph: 0f,
                new ShieldEffect { Coefficient = 12f, Duration = 8f },
                new ApplyStatusEffect { Status = st.EmberWard, Duration = 8f, OnSelf = true });
            emberGuard.AiUseBelowSelfHealth = 0.6f;
            emberGuard.AiPriority = 6f;
            Save(emberGuard);

            AbilityDefinition shockwave = Ability(
                "PC_AB_Shockwave", "Shockwave", "Slams the ground, scattering everything in front.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Cone(3, 100f),
                range: 3, castTime: 0f, cooldown: 14f, telegraph: 0f,
                new DamageEffect { Coefficient = 1.6f, DamageType = DamageType.Physical },
                new KnockbackEffect { Tiles = 2, FromCaster = true },
                new ThreatEffect { Amount = 250f });
            shockwave.AiMinTargets = 2;
            shockwave.ThreatMultiplier = 3f;
            Save(shockwave);

            c.Warden = Class("CL_Warden", "Warden", ActorRole.Tank, new Color(0.30f, 0.62f, 0.68f),
                health: 320f, power: 7f, armor: 70f, fireResist: 0.25f, moveSpeed: 3.2f,
                preferredRange: 1, resource: ResourceKind.Rage,
                basicAttack: shieldBash, abilities: new[] { taunt, shockwave, emberGuard });

            // --- Cleric (healer) -----------------------------------------------------
            AbilityDefinition censer = Ability(
                "PC_AB_CenserStrike", "Censer Strike", "Swings the burning censer.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 5, castTime: 0.5f, cooldown: 1.6f, telegraph: 0f,
                new DamageEffect { Coefficient = 1.1f, DamageType = DamageType.Arcane });

            AbilityDefinition mend = Ability(
                "PC_AB_Mend", "Mend", "A focused, efficient heal.",
                TargetKind.Ally, AffectsFaction.Allies, TargetShape.Single,
                range: 8, castTime: 1.1f, cooldown: 0f, telegraph: 0f,
                new HealEffect { Coefficient = 4.5f });
            mend.AiPriority = 4f;
            Save(mend);

            AbilityDefinition benediction = Ability(
                "PC_AB_Benediction", "Benediction", "Heals everyone standing close together.",
                TargetKind.Ally, AffectsFaction.Allies, TargetShape.Circle(3),
                range: 8, castTime: 1.8f, cooldown: 12f, telegraph: 0f,
                new HealEffect { Coefficient = 3.2f });
            benediction.AiMinTargets = 2;
            benediction.AiPriority = 5f;
            Save(benediction);

            AbilityDefinition cleanse = Ability(
                "PC_AB_Cleanse", "Cleanse", "Strips burning and other afflictions.",
                TargetKind.Ally, AffectsFaction.Allies, TargetShape.Single,
                range: 8, castTime: 0f, cooldown: 6f, telegraph: 0f,
                new DispelEffect { Count = 2, RemoveDebuffs = true });
            Save(cleanse);

            AbilityDefinition ward = Ability(
                "PC_AB_EmberwardBlessing", "Emberward", "Grants fire resistance to the party.",
                TargetKind.Ally, AffectsFaction.Allies, TargetShape.Circle(4),
                range: 8, castTime: 0f, cooldown: 30f, telegraph: 0f,
                new ApplyStatusEffect { Status = st.EmberWard, Duration = 20f });
            Save(ward);

            c.Cleric = Class("CL_Cleric", "Emberward Cleric", ActorRole.Healer, new Color(0.85f, 0.88f, 0.72f),
                health: 180f, power: 9f, armor: 15f, fireResist: 0.2f, moveSpeed: 3.4f,
                preferredRange: 7, resource: ResourceKind.Mana,
                basicAttack: censer, abilities: new[] { mend, benediction, cleanse, ward });

            // --- Ranger (ranged dps) ---------------------------------------------------
            AbilityDefinition shot = Ability(
                "PC_AB_Shot", "Shot", "A quick arrow.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 8, castTime: 0.4f, cooldown: 1.1f, telegraph: 0f,
                new DamageEffect { Coefficient = 1.5f, DamageType = DamageType.Physical });

            AbilityDefinition volley = Ability(
                "PC_AB_Volley", "Volley", "Rains arrows over an area.",
                TargetKind.Ground, AffectsFaction.Enemies, TargetShape.Circle(2),
                range: 9, castTime: 1f, cooldown: 10f, telegraph: 0f,
                new DamageEffect { Coefficient = 1.4f, DamageType = DamageType.Physical },
                new DamageEffect { Coefficient = 1.4f, DamageType = DamageType.Physical, Delay = 0.5f },
                new DamageEffect { Coefficient = 1.4f, DamageType = DamageType.Physical, Delay = 1f });
            volley.AiMinTargets = 2;
            Save(volley);

            AbilityDefinition hamstring = Ability(
                "PC_AB_Hamstring", "Hamstring", "A crippling shot.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 8, castTime: 0f, cooldown: 9f, telegraph: 0f,
                new DamageEffect { Coefficient = 1.2f, DamageType = DamageType.Physical },
                new ApplyStatusEffect { Status = st.Chilled, Duration = 6f });
            Save(hamstring);

            c.Ranger = Class("CL_Ranger", "Cinder Ranger", ActorRole.RangedDps, new Color(0.45f, 0.72f, 0.62f),
                health: 195f, power: 11f, armor: 25f, fireResist: 0.1f, moveSpeed: 4f,
                preferredRange: 7, resource: ResourceKind.Stamina,
                basicAttack: shot, abilities: new[] { volley, hamstring });

            // --- Mage (caster dps) ------------------------------------------------------
            AbilityDefinition frostbolt = Ability(
                "PC_AB_Frostbolt", "Frostbolt", "A lance of cold.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 8, castTime: 1.2f, cooldown: 0f, telegraph: 0f,
                new DamageEffect { Coefficient = 2.4f, DamageType = DamageType.Frost },
                new ApplyStatusEffect { Status = st.Chilled, Duration = 4f });

            AbilityDefinition glacialBurst = Ability(
                "PC_AB_GlacialBurst", "Glacial Burst", "Shatters cold outward from a point.",
                TargetKind.Ground, AffectsFaction.Enemies, TargetShape.Circle(2),
                range: 8, castTime: 1.6f, cooldown: 12f, telegraph: 0f,
                new DamageEffect { Coefficient = 3f, DamageType = DamageType.Frost, EdgeFalloff = 0.7f },
                new ApplyStatusEffect { Status = st.Chilled, Duration = 6f, Stacks = 2 });
            glacialBurst.AiMinTargets = 2;
            Save(glacialBurst);

            AbilityDefinition blink = Ability(
                "PC_AB_Blink", "Blink", "Steps sideways through the heat haze.",
                TargetKind.Ground, AffectsFaction.Allies, TargetShape.Single,
                range: 6, castTime: 0f, cooldown: 14f, telegraph: 0f,
                new DashEffect());
            Save(blink);

            c.Mage = Class("CL_Mage", "Tidecaller Mage", ActorRole.CasterDps, new Color(0.32f, 0.60f, 0.85f),
                health: 165f, power: 13f, armor: 10f, fireResist: 0.15f, moveSpeed: 3.3f,
                preferredRange: 7, resource: ResourceKind.Mana,
                basicAttack: frostbolt, abilities: new[] { glacialBurst, blink });

            // --- Rogue (melee dps) --------------------------------------------------------
            AbilityDefinition stab = Ability(
                "PC_AB_Stab", "Stab", "A fast thrust between the plates.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 1, castTime: 0f, cooldown: 0.9f, telegraph: 0f,
                new DamageEffect { Coefficient = 1.4f, DamageType = DamageType.Physical });

            AbilityDefinition eviscerate = Ability(
                "PC_AB_Eviscerate", "Eviscerate", "Finishes a wounded target.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 1, castTime: 0f, cooldown: 8f, telegraph: 0f,
                new DamageEffect { Coefficient = 4.2f, DamageType = DamageType.Physical });
            eviscerate.AiUseBelowTargetHealth = 0.45f;
            eviscerate.AiPriority = 4f;
            Save(eviscerate);

            AbilityDefinition shadowstep = Ability(
                "PC_AB_Shadowstep", "Shadowstep", "Closes the gap instantly.",
                TargetKind.Enemy, AffectsFaction.Enemies, TargetShape.Single,
                range: 7, castTime: 0f, cooldown: 12f, telegraph: 0f,
                new DashEffect(),
                new DamageEffect { Coefficient = 1.8f, DamageType = DamageType.Physical },
                new ThreatEffect { Amount = -150f });
            Save(shadowstep);

            c.Rogue = Class("CL_Rogue", "Ashblade Rogue", ActorRole.MeleeDps, new Color(0.55f, 0.60f, 0.68f),
                health: 190f, power: 12f, armor: 30f, fireResist: 0.1f, moveSpeed: 4.3f,
                preferredRange: 1, resource: ResourceKind.Stamina,
                basicAttack: stab, abilities: new[] { eviscerate, shadowstep });

            return c;
        }

        private static ClassDefinition Class(
            string assetName, string display, ActorRole role, Color accent,
            float health, float power, float armor, float fireResist, float moveSpeed,
            int preferredRange, ResourceKind resource,
            AbilityDefinition basicAttack, AbilityDefinition[] abilities)
        {
            var def = Asset<ClassDefinition>($"Classes/{assetName}");
            def.DisplayName = display;
            def.Role = role;
            def.AccentColor = accent;
            def.PreferredRange = preferredRange;
            def.BasicAttack = basicAttack;
            def.Abilities = new List<AbilityDefinition>(abilities);

            StatBlock stats = StatBlock.Default;
            stats.MaxHealth = health;
            stats.Power = power;
            stats.Armor = armor;
            stats.FireResist = fireResist;
            stats.MoveSpeed = moveSpeed;
            stats.CritChance = 0.1f;
            stats.ResourceKind = resource;
            stats.MaxResource = resource == ResourceKind.None ? 0f : 100f;
            stats.ResourceRegen = resource == ResourceKind.None ? 0f : 8f;
            stats.ThreatModifier = role switch
            {
                ActorRole.Tank => 4f,
                ActorRole.Healer => 0.5f,
                _ => 1f
            };
            def.BaseStats = stats;

            def.Visuals = FindVisualSet(assetName.Replace("CL_", ""));
            Save(def);
            return def;
        }

        // ==========================================================================
        // Encounters, biome, dungeon
        // ==========================================================================

        private static List<EncounterDefinition> BuildEncounters(Enemies e, Hazards hz)
        {
            var list = new List<EncounterDefinition>
            {
                Encounter("EC_ImpNest", "Imp Nest", EncounterCategory.Trash, 14, hz, null,
                    (e.Imp, 3, 4)),

                Encounter("EC_HoundPack", "Hound Pack", EncounterCategory.Trash, 18, hz, null,
                    (e.Hound, 3, 4)),

                Encounter("EC_ZealotCircle", "Zealot Circle", EncounterCategory.Trash, 24, hz, hz.FirePool,
                    (e.Zealot, 2, 2), (e.Imp, 2, 3)),

                Encounter("EC_MixedWarband", "Warband", EncounterCategory.Trash, 30, hz, null,
                    (e.Hound, 2, 3), (e.Zealot, 1, 2), (e.Imp, 2, 2)),

                Encounter("EC_BruteGuard", "Brute Guard", EncounterCategory.Elite, 45, hz, hz.FirePool,
                    (e.Brute, 1, 1), (e.Imp, 2, 3)),

                Encounter("EC_TwinBrutes", "Twin Brutes", EncounterCategory.Elite, 60, hz, null,
                    (e.Brute, 2, 2))
            };

            return list;
        }

        private static EncounterDefinition Encounter(
            string assetName, string display, EncounterCategory category, int threat,
            Hazards hz, HazardDefinition placedHazard,
            params (EnemyDefinition enemy, int min, int max)[] slots)
        {
            var def = Asset<EncounterDefinition>($"Encounters/{assetName}");
            def.DisplayName = display;
            def.Category = category;
            def.Threat = threat;
            def.SpawnRadius = 3;
            def.LocksDoorsUntilCleared = true;

            def.Slots = new List<EncounterSlot>();
            foreach ((EnemyDefinition enemy, int min, int max) in slots)
            {
                def.Slots.Add(new EncounterSlot
                {
                    Enemy = enemy,
                    MinCount = min,
                    MaxCount = max,
                    Weight = 1f
                });
            }

            def.Hazards = new List<PlacedHazard>();
            if (placedHazard != null)
            {
                // Offset, never centred. A permanent pool on the room's centre
                // sits exactly where the party walks in and where the pack
                // spawns — it cooks both sides before the fight starts, and it
                // denies the one tile a melee group most needs. Off to one side
                // it does its actual job: making half the room worse to stand in.
                def.Hazards.Add(new PlacedHazard
                {
                    Hazard = placedHazard,
                    Offset = new Vector2Int(3, 2),
                    Radius = 1,
                    Duration = 0f
                });
            }

            Save(def);
            return def;
        }

        private static BiomeDefinition BuildBiome(
            Statuses st, Hazards hz, List<EncounterDefinition> encounters, Enemies enemies)
        {
            var biome = Asset<BiomeDefinition>("Biomes/BI_Volcano");
            biome.DisplayName = "The Ember Depths";
            biome.Flavour = "Basalt corridors above a lake of slow fire.";
            biome.AmbientTint = new Color(1f, 0.84f, 0.74f);
            biome.LiquidGlow = Ember;
            biome.LiquidHazard = hz.StandingLava;
            biome.LiquidCoverage = 0.09f;
            biome.SignatureDebuff = st.Burning;
            biome.Boss = enemies.Boss;

            biome.FloorTiles = new List<WeightedTile>
            {
                WeightedTileFor("basalt_cracked", 5f),
                WeightedTileFor("scorched_brick", 3f),
                WeightedTileFor("ash_ground", 2f)
            };

            biome.WallTiles = new List<WeightedTile> { WeightedTileFor("basalt_wall", 1f) };
            biome.LiquidTile = TileFor("lava_pool");
            biome.BossFloorTile = TileFor("obsidian_floor");

            biome.TrashEncounters = new List<EncounterDefinition>();
            biome.EliteEncounters = new List<EncounterDefinition>();
            foreach (EncounterDefinition e in encounters)
            {
                if (e.Category == EncounterCategory.Elite) biome.EliteEncounters.Add(e);
                else biome.TrashEncounters.Add(e);
            }

            Save(biome);
            return biome;
        }

        private static DungeonDefinition BuildDungeon(BiomeDefinition biome)
        {
            var d = Asset<DungeonDefinition>("Dungeons/DG_EmberDepths");
            d.DisplayName = "The Ember Depths";
            d.Description = "Nine chambers cut into a living volcano. Something at the bottom is awake.";
            d.Biome = biome;
            d.PartySize = 5;
            d.MapSize = new Vector2Int(84, 84);
            d.MinRooms = 8;
            d.MaxRooms = 10;
            d.RoomSizeMin = new Vector2Int(9, 9);
            d.RoomSizeMax = new Vector2Int(15, 15);
            d.RoomSpacing = 4;
            d.CorridorWidth = 2;
            d.ExtraConnectionRatio = 0.25f;
            d.EliteRooms = 2;
            d.HasTreasureRoom = true;
            d.BaseRoomBudget = 20;
            d.DifficultyByDepth = AnimationCurve.EaseInOut(0f, 1f, 1f, 2.2f);
            d.UseRandomSeed = true;
            d.FixedSeed = 1337;
            Save(d);
            return d;
        }

        private static PartyRoster BuildRoster(Classes c)
        {
            var roster = Asset<PartyRoster>("Classes/PR_Default");
            roster.Slots = new List<ClassDefinition> { c.Warden, c.Cleric, c.Ranger, c.Mage, c.Rogue };
            roster.MemberNames = new List<string> { "Bastion", "Vesper", "Rook", "Nerin", "Quill" };
            // Slot 0 is the tank: the most forgiving slot to hand a new player,
            // and the one whose job the companion AI does least well.
            roster.LocalPlayerSlot = 0;
            Save(roster);
            return roster;
        }

        // ==========================================================================
        // Asset helpers
        // ==========================================================================

        /// <summary>Loads the asset at <paramref name="relativePath"/> or creates it.</summary>
        private static T Asset<T>(string relativePath) where T : ScriptableObject
        {
            string path = $"{Root}/{relativePath}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;

            var created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void Save(Object asset) => EditorUtility.SetDirty(asset);

        /// <summary>
        /// Links art if it has been imported, and quietly leaves the field empty
        /// if it has not. Content must build on a fresh clone with no art present.
        /// </summary>
        private static ActorVisualSet FindVisualSet(string actorName) =>
            FindExact<ActorVisualSet>($"VS_{actorName}");

        private static TileBase TileFor(string slug) => FindExact<TileBase>($"T_{slug}");

        /// <summary>
        /// Loads the single asset whose file name is exactly
        /// <paramref name="assetName"/>.
        ///
        /// <see cref="AssetDatabase.FindAssets"/> matches loosely — a search for
        /// "T_basalt_cracked" also returns "T_basalt_wall", because they share
        /// the token "basalt". Taking guids[0] from that would silently wire the
        /// wall tile into the floor slot, and the result looks like a generator
        /// bug rather than a lookup bug.
        /// </summary>
        private static T FindExact<T>(string assetName) where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"{assetName} t:{typeof(T).Name}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) != assetName) continue;
                return AssetDatabase.LoadAssetAtPath<T>(path);
            }

            return null;
        }

        private static WeightedTile WeightedTileFor(string slug, float weight) =>
            new WeightedTile { Tile = TileFor(slug), Weight = weight };
    }
}
