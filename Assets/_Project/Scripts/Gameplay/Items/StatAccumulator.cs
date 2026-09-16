using System;
using EmberDepths.Content;

namespace EmberDepths.Gameplay.Items
{
    /// <summary>
    /// Collects every <see cref="StatModifier"/> from equipment and set bonuses
    /// into one flat and one percentage total per stat.
    ///
    /// Gathering first and applying once is deliberate. Applying modifiers as
    /// they are found would make the result depend on the order items happen to
    /// be equipped in, which is the kind of bug that only shows up as "my armour
    /// is different after I re-equip my boots".
    /// </summary>
    public struct StatAccumulator
    {
        private static readonly int StatCount = Enum.GetValues(typeof(StatKind)).Length;

        private float[] _flat;
        private float[] _percent;

        public static StatAccumulator Create() => new StatAccumulator
        {
            _flat = new float[StatCount],
            _percent = new float[StatCount]
        };

        public bool IsValid => _flat != null;

        public void Clear()
        {
            if (_flat == null) return;
            Array.Clear(_flat, 0, _flat.Length);
            Array.Clear(_percent, 0, _percent.Length);
        }

        public void Add(StatModifier modifier, int upgradeLevel)
        {
            if (_flat == null) return;

            int index = (int)modifier.Stat;
            if (index < 0 || index >= StatCount) return;

            float value = modifier.ValueAt(upgradeLevel);

            if (modifier.Mode == ModifierMode.Percent) _percent[index] += value;
            else _flat[index] += value;
        }

        public float Flat(StatKind stat) => _flat == null ? 0f : _flat[(int)stat];

        public float Percent(StatKind stat) => _percent == null ? 0f : _percent[(int)stat];

        /// <summary>
        /// Applies this accumulator's contribution for one stat:
        /// <c>(baseValue + flat) * (1 + percent)</c>.
        ///
        /// Percent is a fraction of the *already-flat-boosted* value, so a +50
        /// armour plate and a +20% armour trinket combine the way a player
        /// expects rather than each reading off the naked base.
        /// </summary>
        public float Apply(StatKind stat, float baseValue) =>
            (baseValue + Flat(stat)) * (1f + Percent(stat));

        /// <summary>Multiplier-style stats have no meaningful flat part; this adds both as fractions.</summary>
        public float ApplyMultiplier(StatKind stat, float baseValue) =>
            baseValue * (1f + Flat(stat) + Percent(stat));
    }
}
