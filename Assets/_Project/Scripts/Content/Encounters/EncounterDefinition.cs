using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmberDepths.Content
{
    public enum EncounterCategory
    {
        Trash = 0,
        Elite = 1,
        Boss = 2,
        /// <summary>Spawns behind the party after they have entered. Use sparingly.</summary>
        Ambush = 3
    }

    [Serializable]
    public struct EncounterSlot
    {
        public EnemyDefinition Enemy;

        [Min(1)] public int MinCount;
        [Min(1)] public int MaxCount;

        [Tooltip("Relative chance of this slot being picked when the generator fills a budget.")]
        [Min(0f)] public float Weight;
    }

    [Serializable]
    public struct PlacedHazard
    {
        public HazardDefinition Hazard;

        [Tooltip("Offset from the room centre, in tiles.")]
        public Vector2Int Offset;

        [Min(0)] public int Radius;

        [Tooltip("0 means it never expires — permanent room geometry rather than a fight mechanic.")]
        [Min(0f)] public float Duration;
    }

    /// <summary>
    /// A pack of enemies plus the hazards they fight among. One of these fills one
    /// room.
    ///
    /// Encounters are authored rather than assembled purely at random, because a
    /// room reads as designed when the composition is deliberate — two casters
    /// behind three melee, a fire pool between them and the door.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Encounter", fileName = "EC_NewEncounter", order = 13)]
    public sealed class EncounterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "New Encounter";

        public EncounterCategory Category = EncounterCategory.Trash;

        [Tooltip("Rough difficulty. The dungeon generator spends a per-room budget on these, " +
                 "so a value that lies here makes the whole floor mistuned.")]
        [Min(1)] public int Threat = 10;

        [Header("Composition")]
        public List<EncounterSlot> Slots = new List<EncounterSlot>();

        [Tooltip("Tiles from the room centre within which enemies are scattered.")]
        [Min(1)] public int SpawnRadius = 3;

        [Header("Hazards placed with the pack")]
        public List<PlacedHazard> Hazards = new List<PlacedHazard>();

        [Header("Rules")]
        [Tooltip("Doors shut behind the party and stay shut until every enemy is dead. " +
                 "This is what turns a room into a fight rather than a place to run through.")]
        public bool LocksDoorsUntilCleared = true;

        [Tooltip("Tiles from the party at which the pack wakes up. 0 uses each enemy's own aggro radius.")]
        [Min(0)] public int ActivationRadius;

        /// <summary>Total enemies at maximum roll — used by the validator to catch runaway packs.</summary>
        public int MaxEnemyCount
        {
            get
            {
                int total = 0;
                for (int i = 0; i < Slots.Count; i++) total += Mathf.Max(Slots[i].MinCount, Slots[i].MaxCount);
                return total;
            }
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = name;

            for (int i = 0; i < Slots.Count; i++)
            {
                EncounterSlot s = Slots[i];
                if (s.MinCount < 1) s.MinCount = 1;
                if (s.MaxCount < s.MinCount) s.MaxCount = s.MinCount;
                if (s.Weight <= 0f) s.Weight = 1f;
                Slots[i] = s;
            }
        }
    }
}
