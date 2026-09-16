using System;
using System.Collections.Generic;
using EmberDepths.Core.Sim;
using UnityEngine;

namespace EmberDepths.Content
{
    [Serializable]
    public struct LootEntry
    {
        public ItemDefinition Item;

        [Tooltip("Relative weight inside the table. Only the ratios matter.")]
        [Min(0f)] public float Weight;

        [Tooltip("Lowest upgrade level this can drop at.")]
        [Min(0)] public int MinUpgrade;

        [Tooltip("Highest upgrade level this can drop at. Pre-upgraded drops are how a " +
                 "deep room feels more rewarding than a shallow one.")]
        [Min(0)] public int MaxUpgrade;
    }

    /// <summary>One item that actually dropped.</summary>
    public readonly struct LootRoll
    {
        public readonly ItemDefinition Item;
        public readonly int UpgradeLevel;

        public LootRoll(ItemDefinition item, int upgradeLevel)
        {
            Item = item;
            UpgradeLevel = upgradeLevel;
        }
    }

    /// <summary>
    /// What an enemy or chest drops.
    ///
    /// Rolls come from the run's seeded stream, so the same seed and the same
    /// kills produce the same loot. That matters more than it sounds: without
    /// it, "this seed is unwinnable" is unfalsifiable, and a balance pass over
    /// a thousand simulated runs measures nothing.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Loot Table", fileName = "LT_NewLoot", order = 50)]
    public sealed class LootTable : ScriptableObject
    {
        [Header("Currency")]
        [Tooltip("Embers are the upgrade currency. Most of a run's power comes from these " +
                 "rather than from drops, so trash should always pay something.")]
        [Min(0)] public int MinEmbers;
        [Min(0)] public int MaxEmbers = 5;

        [Header("Drops")]
        [Range(0f, 1f)]
        [Tooltip("Chance that this table drops an item at all.")]
        public float DropChance = 0.25f;

        [Min(1)]
        [Tooltip("Independent rolls when the table does drop.")]
        public int Rolls = 1;

        [Tooltip("Ignore DropChance and always drop. For bosses and treasure chests.")]
        public bool AlwaysDrops;

        public List<LootEntry> Entries = new List<LootEntry>();

        /// <summary>
        /// Rolls the table into <paramref name="results"/>.
        ///
        /// <paramref name="depth"/> is the room's normalised depth (0 at the
        /// entrance, 1 at the boss door). It nudges the upgrade level of what
        /// drops, so the same table stays useful across a whole floor instead of
        /// needing one table per room.
        /// </summary>
        public void Roll(DeterministicRandom rng, List<LootRoll> results, out int embers, float depth = 0f)
        {
            embers = MaxEmbers > MinEmbers ? rng.Range(MinEmbers, MaxEmbers + 1) : MinEmbers;

            if (Entries.Count == 0) return;
            if (!AlwaysDrops && !rng.Chance(DropChance)) return;

            var weights = new float[Entries.Count];
            for (int i = 0; i < Entries.Count; i++)
                weights[i] = Entries[i].Item != null ? Mathf.Max(0f, Entries[i].Weight) : 0f;

            for (int r = 0; r < Rolls; r++)
            {
                int index = rng.PickWeighted(weights);
                if (index < 0) return;

                LootEntry entry = Entries[index];

                int min = Mathf.Max(0, entry.MinUpgrade);
                int max = Mathf.Clamp(entry.MaxUpgrade, min, entry.Item.MaxUpgradeLevel);

                // Depth raises the floor rather than the ceiling: a deep room
                // cannot roll a worse item than a shallow one, but its best roll
                // is unchanged. Keeps the curve monotonic without inflating it.
                int depthFloor = Mathf.FloorToInt(Mathf.Clamp01(depth) * max);
                min = Mathf.Clamp(Mathf.Max(min, depthFloor), 0, max);

                int level = max > min ? rng.Range(min, max + 1) : min;
                results.Add(new LootRoll(entry.Item, level));
            }
        }

        private void OnValidate()
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                LootEntry entry = Entries[i];
                if (entry.Weight <= 0f) entry.Weight = 1f;
                if (entry.MaxUpgrade < entry.MinUpgrade) entry.MaxUpgrade = entry.MinUpgrade;
                Entries[i] = entry;
            }
        }
    }
}
