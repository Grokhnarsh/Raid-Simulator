using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>
    /// A piece of equipment.
    ///
    /// Items carry no behaviour of their own — only a slot, a rarity and a list
    /// of <see cref="StatModifier"/>. Everything a piece of gear "does" is
    /// therefore expressible in the inspector, and the simulation never has to
    /// know an item exists: equipment feeds the same <c>RuntimeStats</c>
    /// recompute that statuses already feed.
    ///
    /// The exception is <see cref="GrantedAbility"/>, which is how a legendary
    /// earns the word.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Item", fileName = "IT_NewItem", order = 51)]
    public sealed class ItemDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "New Item";

        [TextArea(2, 4)]
        public string Flavour;

        public Sprite Icon;

        public EquipmentSlot Slot = EquipmentSlot.Trinket;
        public ItemRarity Rarity = ItemRarity.Common;

        [Tooltip("Rough power band. Used to decide whether a drop is an upgrade, " +
                 "and to weight loot tables by dungeon depth.")]
        [Min(1)] public int ItemLevel = 1;

        [Header("Stats")]
        public List<StatModifier> Modifiers = new List<StatModifier>();

        [Header("Set")]
        [Tooltip("Leave empty for a standalone item.")]
        public ItemSetDefinition Set;

        [Header("Upgrading")]
        [Tooltip("How far this item can be improved. 0 makes it fixed.")]
        [Range(0, 10)] public int MaxUpgradeLevel = 5;

        [Tooltip("Embers for the first upgrade. Later levels cost progressively more.")]
        [Min(1)] public int BaseUpgradeCost = 40;

        [Tooltip("Multiplies the cost at each level, compounding. 1.6 means +1 -> +2 " +
                 "costs 60% more than +0 -> +1.")]
        [Range(1f, 3f)] public float UpgradeCostGrowth = 1.6f;

        [Header("Special")]
        [Tooltip("Added to the wearer's action bar while equipped. Reserve for legendaries.")]
        public AbilityDefinition GrantedAbility;

        public Color TintColour => Rarity.Colour();

        public bool IsPartOfSet => Set != null;

        /// <summary>Embers to go from <paramref name="currentLevel"/> to the next level.</summary>
        public int UpgradeCost(int currentLevel)
        {
            if (currentLevel >= MaxUpgradeLevel) return 0;
            return Mathf.RoundToInt(BaseUpgradeCost * Mathf.Pow(UpgradeCostGrowth, currentLevel));
        }

        /// <summary>Total embers to take this item from zero to maximum.</summary>
        public int TotalUpgradeCost()
        {
            int total = 0;
            for (int level = 0; level < MaxUpgradeLevel; level++) total += UpgradeCost(level);
            return total;
        }

        /// <summary>
        /// A single number for "how good is this", used to decide whether a drop
        /// beats what a party member is already wearing. Crude on purpose — a
        /// proper comparison needs the wearer's role, which is a later problem.
        /// </summary>
        public float PowerScore(int upgradeLevel = 0)
        {
            float score = ItemLevel;

            for (int i = 0; i < Modifiers.Count; i++)
            {
                StatModifier mod = Modifiers[i];
                float value = Mathf.Abs(mod.ValueAt(upgradeLevel));
                // Percentages are small numbers with large effects; scale them up
                // so a +15% power ring is not rated below a +2 armour boot.
                score += mod.Mode == ModifierMode.Percent ? value * 120f : value;
            }

            // A set piece is worth more than its stats suggest, because it is a
            // step towards a bonus.
            if (IsPartOfSet) score *= 1.15f;

            return score;
        }

        public string BuildTooltip(int upgradeLevel = 0)
        {
            var sb = new StringBuilder();
            sb.Append(DisplayName);
            if (upgradeLevel > 0) sb.Append($" +{upgradeLevel}");
            sb.AppendLine();
            sb.Append($"{Rarity} {Slot}  ·  ilvl {ItemLevel}");

            for (int i = 0; i < Modifiers.Count; i++)
            {
                sb.AppendLine();
                sb.Append("  ");
                sb.Append(Modifiers[i].Describe(upgradeLevel));
            }

            if (GrantedAbility != null)
            {
                sb.AppendLine();
                sb.Append($"  Grants: {GrantedAbility.DisplayName}");
            }

            if (Set != null)
            {
                sb.AppendLine();
                sb.Append($"  Set: {Set.DisplayName}");
            }

            if (!string.IsNullOrWhiteSpace(Flavour))
            {
                sb.AppendLine();
                sb.Append(Flavour);
            }

            return sb.ToString();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = name;
            if (MaxUpgradeLevel < 0) MaxUpgradeLevel = 0;
        }
    }
}
