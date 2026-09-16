using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>Tuning for how an enemy decides where to stand and whom to hit.</summary>
    [Serializable]
    public struct EnemyAiProfile
    {
        [Tooltip("Tiles at which the enemy notices the party and enters combat.")]
        [Min(1)] public int AggroRadius;

        [Tooltip("Tiles from its spawn point before the enemy gives up and resets. " +
                 "0 disables leashing — bosses use that.")]
        [Min(0)] public int LeashRadius;

        [Tooltip("Distance the enemy tries to hold. Melee sits at 1, artillery at 7+.")]
        [Min(1)] public int PreferredRange;

        [Tooltip("Backs away when the party closes inside PreferredRange. Casters and archers do.")]
        public bool Kites;

        [Tooltip("Simulation ticks between path recalculations. Higher is cheaper and looks dumber.")]
        [Min(1)] public int RepathIntervalTicks;

        [Tooltip("Wanders slowly around its spawn while idle, instead of standing frozen.")]
        public bool WandersWhileIdle;

        [Range(0f, 1f)]
        [Tooltip("Chance per decision to step out of a hazard it is standing in. " +
                 "Below 1 so enemies look reckless rather than perfectly played.")]
        public float HazardAvoidance;

        public static EnemyAiProfile Melee => new EnemyAiProfile
        {
            AggroRadius = 7,
            LeashRadius = 18,
            PreferredRange = 1,
            Kites = false,
            RepathIntervalTicks = 4,
            WandersWhileIdle = true,
            HazardAvoidance = 0.5f
        };

        public static EnemyAiProfile Caster => new EnemyAiProfile
        {
            AggroRadius = 9,
            LeashRadius = 18,
            PreferredRange = 6,
            Kites = true,
            RepathIntervalTicks = 6,
            WandersWhileIdle = false,
            HazardAvoidance = 0.8f
        };

        public static EnemyAiProfile BossProfile => new EnemyAiProfile
        {
            AggroRadius = 14,
            LeashRadius = 0,
            PreferredRange = 1,
            Kites = false,
            RepathIntervalTicks = 3,
            WandersWhileIdle = false,
            HazardAvoidance = 0f
        };
    }

    /// <summary>
    /// A hostile actor archetype. Trash, elites and the boss all use this asset;
    /// <see cref="Rank"/> is what changes health scaling, whether a name plate and
    /// boss bar appear, and whether clearing it opens the room's doors.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Enemy", fileName = "EN_NewEnemy", order = 11)]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "New Enemy";

        [TextArea(2, 4)]
        public string Description;

        public EnemyRank Rank = EnemyRank.Trash;

        public ActorFlags Flags = ActorFlags.None;

        [Header("Art")]
        public ActorVisualSet Visuals;

        [Tooltip("Extra scale on the sprite. Elites read better at 1.15, bosses at 1.0 with bigger art.")]
        [Min(0.25f)] public float VisualScale = 1f;

        [Header("Numbers")]
        public StatBlock BaseStats = StatBlock.Default;

        [Tooltip("Multiplies MaxHealth on top of the base block. Lets one tuning pass cover a whole rank.")]
        [Min(0.1f)] public float HealthMultiplier = 1f;

        [Header("Abilities")]
        public AbilityDefinition BasicAttack;

        [SerializeField]
        [Tooltip("Considered every decision tick, highest AiPriority first.")]
        private List<AbilityDefinition> abilities = new List<AbilityDefinition>();

        public IReadOnlyList<AbilityDefinition> Abilities => abilities;

        [Header("Behaviour")]
        public EnemyAiProfile Ai = EnemyAiProfile.Melee;

        [Header("Boss")]
        [Tooltip("Only read when Rank is Boss. Drives the phase machine.")]
        public BossDefinition BossProfile;

        [Header("Rewards")]
        public LootTable Loot;

        [Min(0)] public int ScoreValue = 10;

        /// <summary>Stats after the rank multiplier, as spawned.</summary>
        public StatBlock ResolvedStats
        {
            get
            {
                StatBlock s = BaseStats;
                s.MaxHealth *= HealthMultiplier * RankHealthScale(Rank);
                return s;
            }
        }

        public static float RankHealthScale(EnemyRank rank) => rank switch
        {
            EnemyRank.Trash => 1f,
            EnemyRank.Elite => 4f,
            EnemyRank.Boss => 20f,
            _ => 1f
        };

        public IEnumerable<AbilityDefinition> AllAbilities()
        {
            if (BasicAttack != null) yield return BasicAttack;
            for (int i = 0; i < abilities.Count; i++)
                if (abilities[i] != null) yield return abilities[i];
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = name;

            // A leashing boss walks home mid-fight the first time the party kites it.
            if (Rank == EnemyRank.Boss && Ai.LeashRadius != 0) Ai.LeashRadius = 0;

            if (Ai.RepathIntervalTicks < 1) Ai.RepathIntervalTicks = 4;
            if (Ai.PreferredRange < 1) Ai.PreferredRange = 1;
        }
    }
}
