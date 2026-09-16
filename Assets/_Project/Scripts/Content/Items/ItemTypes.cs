using System;
using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>
    /// Where an item is worn. Six slots, chosen so that set bonuses can sit at
    /// 2 / 4 / 6 pieces and the last tier is a real commitment — wearing a full
    /// set means giving up every other item in the game.
    /// </summary>
    public enum EquipmentSlot
    {
        Weapon = 0,
        Head = 1,
        Chest = 2,
        Hands = 3,
        Feet = 4,
        Trinket = 5
    }

    public enum ItemRarity
    {
        Common = 0,
        Uncommon = 1,
        Rare = 2,
        Epic = 3,
        Legendary = 4
    }

    /// <summary>
    /// A stat an item or set bonus can touch. These map one-to-one onto fields
    /// on <c>RuntimeStats</c>; adding one here means adding the corresponding
    /// case in the accumulator, and the content validator will say so.
    /// </summary>
    public enum StatKind
    {
        MaxHealth = 0,
        Armor = 1,
        Power = 2,
        MoveSpeed = 3,
        Haste = 4,
        CritChance = 5,
        CritMultiplier = 6,
        FireResist = 7,
        FrostResist = 8,
        ArcaneResist = 9,
        ThreatModifier = 10,
        MaxResource = 11,
        ResourceRegen = 12,
        DamageDealt = 13,
        DamageTaken = 14
    }

    public enum ModifierMode
    {
        /// <summary>Added to the stat. "+40 armour".</summary>
        Flat = 0,

        /// <summary>Fraction of the base stat, added. 0.15 is "+15% power".</summary>
        Percent = 1
    }

    /// <summary>
    /// One stat change contributed by an item or a set bonus.
    ///
    /// <see cref="PerUpgrade"/> is what makes upgrading an item meaningful
    /// rather than cosmetic: it is the slope of the item's power curve, and it
    /// is authored per-modifier so a weapon can gain damage sharply while its
    /// crit chance barely moves.
    /// </summary>
    [Serializable]
    public struct StatModifier
    {
        public StatKind Stat;
        public ModifierMode Mode;

        [Tooltip("Value at upgrade level 0.")]
        public float Value;

        [Tooltip("Added to Value for each upgrade level. This is the item's growth curve.")]
        public float PerUpgrade;

        public float ValueAt(int upgradeLevel) => Value + PerUpgrade * Mathf.Max(0, upgradeLevel);

        public static StatModifier Flat(StatKind stat, float value, float perUpgrade = 0f) =>
            new StatModifier { Stat = stat, Mode = ModifierMode.Flat, Value = value, PerUpgrade = perUpgrade };

        public static StatModifier Percent(StatKind stat, float fraction, float perUpgrade = 0f) =>
            new StatModifier { Stat = stat, Mode = ModifierMode.Percent, Value = fraction, PerUpgrade = perUpgrade };

        /// <summary>Tooltip line, e.g. "+45 Armor" or "+18% Power".</summary>
        public string Describe(int upgradeLevel = 0)
        {
            float value = ValueAt(upgradeLevel);
            string sign = value >= 0f ? "+" : "";

            return Mode == ModifierMode.Percent
                ? $"{sign}{value:P0} {Label(Stat)}"
                : $"{sign}{value:0.##} {Label(Stat)}";
        }

        public static string Label(StatKind stat) => stat switch
        {
            StatKind.MaxHealth => "Health",
            StatKind.Armor => "Armor",
            StatKind.Power => "Power",
            StatKind.MoveSpeed => "Move Speed",
            StatKind.Haste => "Haste",
            StatKind.CritChance => "Crit Chance",
            StatKind.CritMultiplier => "Crit Damage",
            StatKind.FireResist => "Fire Resist",
            StatKind.FrostResist => "Frost Resist",
            StatKind.ArcaneResist => "Arcane Resist",
            StatKind.ThreatModifier => "Threat",
            StatKind.MaxResource => "Max Resource",
            StatKind.ResourceRegen => "Resource Regen",
            StatKind.DamageDealt => "Damage Dealt",
            StatKind.DamageTaken => "Damage Taken",
            _ => stat.ToString()
        };
    }

    public static class ItemRarityExtensions
    {
        /// <summary>
        /// Rarity colours. Warm-neutral deliberately: the party owns the cool
        /// half of the palette, so loot must not read as another teal character.
        /// </summary>
        public static Color Colour(this ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => new Color(0.78f, 0.76f, 0.72f),
            ItemRarity.Uncommon => new Color(0.55f, 0.80f, 0.45f),
            ItemRarity.Rare => new Color(0.38f, 0.62f, 0.92f),
            ItemRarity.Epic => new Color(0.72f, 0.42f, 0.90f),
            ItemRarity.Legendary => new Color(1f, 0.62f, 0.18f),
            _ => Color.white
        };

        /// <summary>How long a drop of this rarity is worth announcing for.</summary>
        public static float ToastSeconds(this ItemRarity rarity) =>
            rarity >= ItemRarity.Epic ? 5f : 3f;
    }
}
