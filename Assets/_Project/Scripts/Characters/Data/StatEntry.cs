using System;
using RaidSim.Core.Stats;
using UnityEngine;

namespace RaidSim.Characters.Data
{
    /// <summary>
    /// One authored stat value: a key and a number.
    /// </summary>
    /// <remarks>
    /// Authoring stats as a list of these rather than as a class with a field per stat is what keeps
    /// adding a new stat a one-line change to <see cref="StatType"/>. No data asset, inspector or
    /// migration is needed, and an asset that does not mention a stat simply leaves it at its
    /// neutral value.
    /// </remarks>
    [Serializable]
    public struct StatEntry
    {
        [Tooltip("Which stat this value applies to.")]
        public StatType Stat;

        [Tooltip("The authored value. Meaning depends on the stat: a pool size, a rating, or a multiplier.")]
        public float Value;

        public StatEntry(StatType stat, float value)
        {
            Stat = stat;
            Value = value;
        }
    }

    public static class StatEntryExtensions
    {
        /// <summary>
        /// Folds authored entries into a <see cref="StatSet"/>. Later duplicates of the same stat
        /// overwrite earlier ones, which makes an override list behave the way an author expects.
        /// </summary>
        public static StatSet ToStatSet(this StatEntry[] entries)
        {
            var set = new StatSet();
            if (entries == null)
            {
                return set;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Stat != StatType.None)
                {
                    set[entries[i].Stat] = entries[i].Value;
                }
            }

            return set;
        }
    }
}
