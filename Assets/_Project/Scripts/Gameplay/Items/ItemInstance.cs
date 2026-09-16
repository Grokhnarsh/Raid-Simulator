using EmberDepths.Content;
using UnityEngine;

namespace EmberDepths.Gameplay.Items
{
    /// <summary>
    /// One actual item in the world: a definition plus the state that makes this
    /// copy of it different from every other copy.
    ///
    /// Today that state is only the upgrade level. It is a separate type anyway,
    /// because the moment items can be rolled with random affixes, re-forged or
    /// socketed, the distinction between "what this item is" and "what this
    /// particular one became" is the difference between a small change and a
    /// rewrite.
    /// </summary>
    public sealed class ItemInstance
    {
        public ItemDefinition Definition { get; }
        public int UpgradeLevel { get; private set; }

        public ItemInstance(ItemDefinition definition, int upgradeLevel = 0)
        {
            Definition = definition;
            UpgradeLevel = definition != null
                ? Mathf.Clamp(upgradeLevel, 0, definition.MaxUpgradeLevel)
                : 0;
        }

        public EquipmentSlot Slot => Definition.Slot;
        public ItemRarity Rarity => Definition.Rarity;
        public ItemSetDefinition Set => Definition.Set;

        public bool CanUpgrade => Definition != null && UpgradeLevel < Definition.MaxUpgradeLevel;

        /// <summary>Embers for the next level, or 0 when fully upgraded.</summary>
        public int NextUpgradeCost => CanUpgrade ? Definition.UpgradeCost(UpgradeLevel) : 0;

        public string DisplayName =>
            UpgradeLevel > 0 ? $"{Definition.DisplayName} +{UpgradeLevel}" : Definition.DisplayName;

        public Color TintColour => Definition.TintColour;

        public float PowerScore => Definition.PowerScore(UpgradeLevel);

        /// <summary>
        /// Raises the upgrade level by one. Does not touch currency — that is
        /// <see cref="Inventory.TryUpgrade"/>'s job, so this stays usable from
        /// tests and from content that grants upgrades for free.
        /// </summary>
        public bool Upgrade()
        {
            if (!CanUpgrade) return false;
            UpgradeLevel++;
            return true;
        }

        public void CollectModifiers(ref StatAccumulator accumulator)
        {
            if (Definition == null) return;

            for (int i = 0; i < Definition.Modifiers.Count; i++)
                accumulator.Add(Definition.Modifiers[i], UpgradeLevel);
        }

        public string BuildTooltip() => Definition.BuildTooltip(UpgradeLevel);

        public override string ToString() => DisplayName;
    }
}
