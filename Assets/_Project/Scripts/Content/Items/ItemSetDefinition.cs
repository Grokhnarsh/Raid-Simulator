using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>
    /// One threshold of a set bonus, e.g. "(4) pieces: +20% fire resistance".
    /// </summary>
    [Serializable]
    public sealed class SetBonusTier
    {
        [Tooltip("Pieces of the set that must be worn for this tier to apply.")]
        [Range(2, 6)] public int RequiredPieces = 2;

        [TextArea(1, 3)]
        [Tooltip("Shown in the character panel. Write what the player gets, not how it works.")]
        public string Description;

        public List<StatModifier> Modifiers = new List<StatModifier>();

        [Tooltip("Applied to the wearer while this tier is active, and removed when it is not. " +
                 "The way to express a bonus that is more than numbers.")]
        public StatusEffectDefinition Aura;

        [Tooltip("Added to the wearer's kit while this tier is active. The payoff tier of a " +
                 "six-piece set should usually be this rather than another stat line.")]
        public AbilityDefinition GrantedAbility;
    }

    /// <summary>
    /// A set of items that reward wearing several at once.
    ///
    /// Sets are the reason a loot system is interesting rather than a list of
    /// numbers going up: they make a strictly worse item worth equipping,
    /// because the second and fourth piece are worth more than the difference.
    /// That tension is the whole mechanic, so the tiers should be large enough
    /// to actually change a decision.
    ///
    /// Tiers are cumulative — wearing four pieces of a set with a (2) and a (4)
    /// tier grants both.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Item Set", fileName = "SET_NewSet", order = 52)]
    public sealed class ItemSetDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "New Set";

        [TextArea(2, 4)]
        public string Flavour;

        [Tooltip("Accent used for the set's name in the character panel.")]
        public Color Tint = new Color(1f, 0.62f, 0.18f);

        [Header("Members")]
        [Tooltip("Purely for display and validation — an item belongs to a set by pointing " +
                 "at it, not by appearing here.")]
        public List<ItemDefinition> Members = new List<ItemDefinition>();

        [Header("Bonuses")]
        [Tooltip("Ordered by RequiredPieces ascending. Cumulative: at four pieces both " +
                 "the (2) and the (4) tier are active.")]
        public List<SetBonusTier> Tiers = new List<SetBonusTier>();

        /// <summary>Tiers active at the given number of equipped pieces.</summary>
        public IEnumerable<SetBonusTier> ActiveTiers(int equippedPieces)
        {
            for (int i = 0; i < Tiers.Count; i++)
            {
                SetBonusTier tier = Tiers[i];
                if (tier != null && equippedPieces >= tier.RequiredPieces) yield return tier;
            }
        }

        /// <summary>Pieces needed for the next unearned tier, or 0 if all are active.</summary>
        public int NextThreshold(int equippedPieces)
        {
            int best = 0;
            for (int i = 0; i < Tiers.Count; i++)
            {
                SetBonusTier tier = Tiers[i];
                if (tier == null || equippedPieces >= tier.RequiredPieces) continue;
                if (best == 0 || tier.RequiredPieces < best) best = tier.RequiredPieces;
            }
            return best;
        }

        public string BuildTooltip(int equippedPieces)
        {
            var sb = new StringBuilder();
            sb.Append($"{DisplayName} ({equippedPieces}/{Members.Count})");

            for (int i = 0; i < Tiers.Count; i++)
            {
                SetBonusTier tier = Tiers[i];
                if (tier == null) continue;

                bool active = equippedPieces >= tier.RequiredPieces;
                sb.AppendLine();
                sb.Append(active ? "  ● " : "  ○ ");
                sb.Append($"({tier.RequiredPieces}) ");
                sb.Append(string.IsNullOrWhiteSpace(tier.Description)
                    ? DescribeModifiers(tier)
                    : tier.Description);
            }

            return sb.ToString();
        }

        private static string DescribeModifiers(SetBonusTier tier)
        {
            if (tier.Modifiers.Count == 0) return tier.Aura != null ? tier.Aura.DisplayName : "—";

            var parts = new List<string>(tier.Modifiers.Count);
            for (int i = 0; i < tier.Modifiers.Count; i++) parts.Add(tier.Modifiers[i].Describe());
            return string.Join(", ", parts);
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = name;

            // Out-of-order tiers make ActiveTiers correct but the tooltip
            // nonsense, and hide authoring mistakes like two (2) tiers.
            Tiers.Sort((a, b) =>
            {
                if (a == null) return b == null ? 0 : 1;
                if (b == null) return -1;
                return a.RequiredPieces.CompareTo(b.RequiredPieces);
            });
        }
    }
}
