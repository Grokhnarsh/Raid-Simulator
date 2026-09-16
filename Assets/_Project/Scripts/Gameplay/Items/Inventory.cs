using System;
using System.Collections.Generic;
using EmberDepths.Content;
using UnityEngine;

namespace EmberDepths.Gameplay.Items
{
    /// <summary>
    /// The party's shared stash and ember purse.
    ///
    /// Shared rather than per-character on purpose: five separate purses turn
    /// every upgrade into a bookkeeping question ("who should hold this?")
    /// without adding a decision worth making. One pool keeps the actual
    /// decision — which item to pour embers into — in front of the player.
    /// </summary>
    public sealed class Inventory
    {
        private readonly List<ItemInstance> _stash = new List<ItemInstance>(32);

        /// <summary>Upgrade currency. Most of a run's power growth comes from spending these.</summary>
        public int Embers { get; private set; }

        public IReadOnlyList<ItemInstance> Stash => _stash;

        public event Action Changed;

        /// <summary>Raised when an upgrade succeeds, for the HUD to react to.</summary>
        public event Action<ItemInstance> ItemUpgraded;

        public int TotalEmbersEarned { get; private set; }
        public int TotalEmbersSpent { get; private set; }

        public void AddEmbers(int amount)
        {
            if (amount <= 0) return;
            Embers += amount;
            TotalEmbersEarned += amount;
            Changed?.Invoke();
        }

        public bool TrySpendEmbers(int amount)
        {
            if (amount < 0 || Embers < amount) return false;
            Embers -= amount;
            TotalEmbersSpent += amount;
            Changed?.Invoke();
            return true;
        }

        public void Stow(ItemInstance item)
        {
            if (item?.Definition == null) return;
            _stash.Add(item);
            Changed?.Invoke();
        }

        public bool Remove(ItemInstance item)
        {
            if (!_stash.Remove(item)) return false;
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Spends embers to raise an item one level.
        ///
        /// Returns false and changes nothing when the item is maxed or the purse
        /// is short, so callers can offer the action without pre-checking.
        /// </summary>
        public bool TryUpgrade(ItemInstance item)
        {
            if (item == null || !item.CanUpgrade) return false;

            int cost = item.NextUpgradeCost;
            if (!TrySpendEmbers(cost)) return false;

            item.Upgrade();
            ItemUpgraded?.Invoke(item);
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// The cheapest worthwhile upgrade across a set of equipment — the one a
        /// player pressing "upgrade" almost always means.
        ///
        /// Lowest level first, because the upgrade curve compounds: taking a +0
        /// item to +1 is both cheaper and a larger relative gain than pushing a
        /// +4 to +5.
        /// </summary>
        public ItemInstance FindBestUpgradeCandidate(IEnumerable<Equipment> equipmentSets)
        {
            ItemInstance best = null;

            foreach (Equipment equipment in equipmentSets)
            {
                if (equipment == null) continue;

                for (int i = 0; i < Equipment.SlotCount; i++)
                {
                    ItemInstance item = equipment.Get((EquipmentSlot)i);
                    if (item == null || !item.CanUpgrade) continue;
                    if (item.NextUpgradeCost > Embers) continue;

                    if (best == null ||
                        item.UpgradeLevel < best.UpgradeLevel ||
                        (item.UpgradeLevel == best.UpgradeLevel && item.NextUpgradeCost < best.NextUpgradeCost))
                    {
                        best = item;
                    }
                }
            }

            return best;
        }

        public void Clear()
        {
            _stash.Clear();
            Embers = 0;
            TotalEmbersEarned = 0;
            TotalEmbersSpent = 0;
            Changed?.Invoke();
        }
    }
}
