using System;
using System.Collections.Generic;
using EmberDepths.Content;
using UnityEngine;

namespace EmberDepths.Gameplay.Items
{
    /// <summary>
    /// What one actor is wearing, plus the set bonuses that follow from it.
    ///
    /// Set counts are maintained incrementally as items go on and off, because
    /// the alternative — rescanning six slots on every stat recompute — is a
    /// surprising amount of work to do several times a second for five party
    /// members. The counts are the only cached state here, and they are rebuilt
    /// from scratch by <see cref="Rebuild"/> if anything ever looks wrong.
    /// </summary>
    public sealed class Equipment
    {
        public static readonly int SlotCount = Enum.GetValues(typeof(EquipmentSlot)).Length;

        private readonly ItemInstance[] _slots = new ItemInstance[SlotCount];
        private readonly Dictionary<ItemSetDefinition, int> _setPieces = new Dictionary<ItemSetDefinition, int>(4);

        /// <summary>Raised whenever the worn set changes, so stats can be recomputed.</summary>
        public event Action Changed;

        public ItemInstance Get(EquipmentSlot slot) => _slots[(int)slot];

        public IReadOnlyList<ItemInstance> Slots => _slots;

        public int EquippedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Length; i++) if (_slots[i] != null) count++;
                return count;
            }
        }

        /// <summary>Average item level of what is worn. Empty slots count as zero.</summary>
        public float AverageItemLevel
        {
            get
            {
                float total = 0f;
                for (int i = 0; i < _slots.Length; i++)
                    if (_slots[i] != null) total += _slots[i].Definition.ItemLevel + _slots[i].UpgradeLevel;
                return total / SlotCount;
            }
        }

        /// <summary>
        /// Wears <paramref name="item"/>, returning whatever it displaced so the
        /// caller can decide where that goes. Returning it rather than silently
        /// destroying it is the difference between a swap and a loss.
        /// </summary>
        public ItemInstance Equip(ItemInstance item)
        {
            if (item?.Definition == null) return null;

            int index = (int)item.Slot;
            ItemInstance displaced = _slots[index];

            if (displaced != null) TrackSet(displaced.Set, -1);
            _slots[index] = item;
            TrackSet(item.Set, +1);

            Changed?.Invoke();
            return displaced;
        }

        public ItemInstance Unequip(EquipmentSlot slot)
        {
            int index = (int)slot;
            ItemInstance removed = _slots[index];
            if (removed == null) return null;

            _slots[index] = null;
            TrackSet(removed.Set, -1);

            Changed?.Invoke();
            return removed;
        }

        private void TrackSet(ItemSetDefinition set, int delta)
        {
            if (set == null) return;

            _setPieces.TryGetValue(set, out int count);
            count += delta;

            if (count <= 0) _setPieces.Remove(set);
            else _setPieces[set] = count;
        }

        /// <summary>Recomputes the set counts from the worn items. Cheap; call it if in doubt.</summary>
        public void Rebuild()
        {
            _setPieces.Clear();
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null) TrackSet(_slots[i].Set, +1);
        }

        public int PiecesOf(ItemSetDefinition set) =>
            set != null && _setPieces.TryGetValue(set, out int count) ? count : 0;

        public IEnumerable<KeyValuePair<ItemSetDefinition, int>> ActiveSets => _setPieces;

        /// <summary>
        /// Everything worn, plus every set tier currently met, folded into one
        /// accumulator. This is the single input equipment gives the stat system.
        /// </summary>
        public void CollectModifiers(ref StatAccumulator accumulator)
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i]?.CollectModifiers(ref accumulator);

            foreach (KeyValuePair<ItemSetDefinition, int> pair in _setPieces)
            {
                foreach (SetBonusTier tier in pair.Key.ActiveTiers(pair.Value))
                {
                    for (int i = 0; i < tier.Modifiers.Count; i++)
                        // Set bonuses do not scale with any single item's upgrade
                        // level — they are a reward for breadth, not for depth.
                        accumulator.Add(tier.Modifiers[i], 0);
                }
            }
        }

        /// <summary>Auras granted by currently active set tiers.</summary>
        public void CollectAuras(List<StatusEffectDefinition> outAuras)
        {
            outAuras.Clear();

            foreach (KeyValuePair<ItemSetDefinition, int> pair in _setPieces)
            {
                foreach (SetBonusTier tier in pair.Key.ActiveTiers(pair.Value))
                {
                    if (tier.Aura == null || outAuras.Contains(tier.Aura)) continue;
                    outAuras.Add(tier.Aura);
                }
            }
        }

        /// <summary>Abilities granted by worn items and by active set tiers.</summary>
        public void CollectGrantedAbilities(List<AbilityDefinition> outAbilities)
        {
            outAbilities.Clear();

            for (int i = 0; i < _slots.Length; i++)
            {
                AbilityDefinition granted = _slots[i]?.Definition.GrantedAbility;
                if (granted != null && !outAbilities.Contains(granted)) outAbilities.Add(granted);
            }

            foreach (KeyValuePair<ItemSetDefinition, int> pair in _setPieces)
            {
                foreach (SetBonusTier tier in pair.Key.ActiveTiers(pair.Value))
                {
                    if (tier.GrantedAbility == null || outAbilities.Contains(tier.GrantedAbility)) continue;
                    outAbilities.Add(tier.GrantedAbility);
                }
            }
        }

        /// <summary>
        /// True if <paramref name="candidate"/> should replace what is in its
        /// slot. Compares power scores, but counts a set piece's contribution
        /// towards an unearned tier — otherwise the AI would never assemble a
        /// set, because the second piece is almost always a downgrade on its own.
        /// </summary>
        public bool IsUpgrade(ItemInstance candidate) => UpgradeDelta(candidate) > 0f;

        /// <summary>
        /// How much better <paramref name="candidate"/> is than what occupies its
        /// slot. Negative means worse. Used to decide which party member a drop
        /// should go to, which needs a magnitude rather than a yes/no.
        /// </summary>
        public float UpgradeDelta(ItemInstance candidate)
        {
            if (candidate?.Definition == null) return float.NegativeInfinity;

            float candidateScore = candidate.PowerScore + SetProgressBonus(candidate);

            ItemInstance current = Get(candidate.Slot);
            if (current == null) return candidateScore;

            return candidateScore - (current.PowerScore + SetProgressBonus(current));
        }

        /// <summary>
        /// Extra weight for an item that moves the wearer towards a set tier
        /// they have not reached. Zero once every tier is active.
        /// </summary>
        private float SetProgressBonus(ItemInstance item)
        {
            if (item.Set == null) return 0f;

            // Counting the item itself answers "what would I have if I wore it",
            // which is the question that matters when deciding to wear it.
            int worn = PiecesOf(item.Set);
            int wouldHave = Get(item.Slot) == item ? worn : worn + 1;

            int next = item.Set.NextThreshold(wouldHave - 1);
            if (next == 0) return 0f;

            // Strongest right before a threshold, so the piece that completes a
            // tier is the one the AI reaches for.
            int missing = Mathf.Max(1, next - wouldHave);
            return 30f / missing;
        }

        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++) _slots[i] = null;
            _setPieces.Clear();
            Changed?.Invoke();
        }
    }
}
