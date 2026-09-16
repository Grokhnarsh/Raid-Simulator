using System;
using System.Collections.Generic;
using RaidSim.Core.Common;
using RaidSim.Core.Mathematics;

namespace RaidSim.Core.Stats
{
    /// <summary>
    /// Runtime stat state of one entity: authored base values plus every modifier currently applied.
    /// </summary>
    /// <remarks>
    /// <para>This is <b>runtime state</b> and never lives in a ScriptableObject. Authoring assets
    /// hand over a <see cref="StatSet"/> of base values; the block owns everything that changes
    /// during a fight. See <c>DATA_ARCHITECTURE.md</c>.</para>
    /// <para>Final values are cached per stat and invalidated when modifiers change, so the common
    /// case — many reads per frame, few writes per fight — costs a dictionary lookup.</para>
    /// <para>Order of application: <c>(base + flat) * (1 + sum(additive)) * product(1 + multiplicative)</c>.
    /// Clamps from <see cref="StatRules"/> are applied last.</para>
    /// </remarks>
    public sealed class StatBlock
    {
        private readonly Dictionary<StatType, float> _baseValues = new Dictionary<StatType, float>();
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        private readonly Dictionary<StatType, float> _cache = new Dictionary<StatType, float>();

        public StatBlock()
        {
        }

        public StatBlock(StatSet baseValues)
        {
            SetBaseValues(baseValues);
        }

        /// <summary>Raised whenever a final stat value may have changed.</summary>
        public event Action<StatType> StatChanged;

        public int ModifierCount => _modifiers.Count;

        /// <summary>Replaces all base values. Existing modifiers are kept.</summary>
        public void SetBaseValues(StatSet baseValues)
        {
            _baseValues.Clear();
            if (baseValues != null)
            {
                foreach (KeyValuePair<StatType, float> pair in baseValues)
                {
                    _baseValues[pair.Key] = pair.Value;
                }
            }

            InvalidateAll();
        }

        public void SetBase(StatType stat, float value)
        {
            _baseValues[stat] = value;
            Invalidate(stat);
        }

        public float GetBase(StatType stat) =>
            _baseValues.TryGetValue(stat, out float value) ? value : StatRules.DefaultBase(stat);

        /// <summary>Final value after every modifier and clamp.</summary>
        public float Get(StatType stat)
        {
            if (_cache.TryGetValue(stat, out float cached))
            {
                return cached;
            }

            float value = Recalculate(stat);
            _cache[stat] = value;
            return value;
        }

        /// <summary>Final value rounded to the nearest whole number. For health, resource and level.</summary>
        public int GetInt(StatType stat) => (int)Math.Round(Get(stat), MidpointRounding.AwayFromZero);

        public void AddModifier(in StatModifier modifier)
        {
            if (modifier.Stat == StatType.None)
            {
                return;
            }

            _modifiers.Add(modifier);
            Invalidate(modifier.Stat);
        }

        public void AddModifiers(IReadOnlyList<StatModifier> modifiers)
        {
            if (modifiers == null)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                AddModifier(modifiers[i]);
            }
        }

        /// <summary>Removes the first modifier equal to <paramref name="modifier"/>.</summary>
        public bool RemoveModifier(in StatModifier modifier)
        {
            int index = _modifiers.IndexOf(modifier);
            if (index < 0)
            {
                return false;
            }

            _modifiers.RemoveAt(index);
            Invalidate(modifier.Stat);
            return true;
        }

        /// <summary>
        /// Removes every modifier tagged with <paramref name="sourceKey"/>. This is how an expiring
        /// effect or an unequipped item withdraws exactly its own contributions.
        /// </summary>
        public int RemoveModifiersFromSource(int sourceKey)
        {
            if (sourceKey == 0)
            {
                return 0;
            }

            int removed = 0;
            for (int i = _modifiers.Count - 1; i >= 0; i--)
            {
                if (_modifiers[i].SourceKey != sourceKey)
                {
                    continue;
                }

                StatType stat = _modifiers[i].Stat;
                _modifiers.RemoveAt(i);
                Invalidate(stat);
                removed++;
            }

            return removed;
        }

        public void ClearModifiers()
        {
            if (_modifiers.Count == 0)
            {
                return;
            }

            _modifiers.Clear();
            InvalidateAll();
        }

        /// <summary>Snapshot of the current final values. Used for save games and simulation reports.</summary>
        public StatSet ToStatSet()
        {
            var set = new StatSet();
            foreach (StatType stat in StatRules.AllStats)
            {
                if (_baseValues.ContainsKey(stat) || HasModifierFor(stat))
                {
                    set[stat] = Get(stat);
                }
            }

            return set;
        }

        private bool HasModifierFor(StatType stat)
        {
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i].Stat == stat)
                {
                    return true;
                }
            }

            return false;
        }

        private float Recalculate(StatType stat)
        {
            float flat = 0f;
            float additive = 0f;
            float multiplicative = 1f;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                StatModifier modifier = _modifiers[i];
                if (modifier.Stat != stat)
                {
                    continue;
                }

                switch (modifier.Mode)
                {
                    case StatModifierMode.Flat:
                        flat += modifier.Value;
                        break;
                    case StatModifierMode.PercentAdditive:
                        additive += modifier.Value;
                        break;
                    case StatModifierMode.PercentMultiplicative:
                        multiplicative *= 1f + modifier.Value;
                        break;
                }
            }

            float value = (GetBase(stat) + flat) * (1f + additive) * multiplicative;
            return StatRules.Clamp(stat, value);
        }

        private void Invalidate(StatType stat)
        {
            _cache.Remove(stat);
            StatChanged?.Invoke(stat);
        }

        private void InvalidateAll()
        {
            _cache.Clear();
            StatChanged?.Invoke(StatType.None);
        }
    }
}
