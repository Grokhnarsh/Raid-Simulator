using System.Collections;
using System.Collections.Generic;

namespace RaidSim.Core.Stats
{
    /// <summary>
    /// A plain bag of stat values. Used for authored base stats, class stats per level, item stats
    /// and snapshots.
    /// </summary>
    /// <remarks>
    /// Deliberately not a class with one field per stat: adding a stat must not mean touching every
    /// data container in the project.
    /// </remarks>
    public sealed class StatSet : IEnumerable<KeyValuePair<StatType, float>>
    {
        private readonly Dictionary<StatType, float> _values;

        public StatSet()
        {
            _values = new Dictionary<StatType, float>();
        }

        public StatSet(IEnumerable<KeyValuePair<StatType, float>> values)
        {
            _values = new Dictionary<StatType, float>();
            if (values == null)
            {
                return;
            }

            foreach (KeyValuePair<StatType, float> pair in values)
            {
                _values[pair.Key] = pair.Value;
            }
        }

        public int Count => _values.Count;

        public float this[StatType stat]
        {
            get => _values.TryGetValue(stat, out float value) ? value : StatRules.DefaultBase(stat);
            set => _values[stat] = value;
        }

        public bool Contains(StatType stat) => _values.ContainsKey(stat);

        public bool TryGet(StatType stat, out float value) => _values.TryGetValue(stat, out value);

        public StatSet With(StatType stat, float value)
        {
            _values[stat] = value;
            return this;
        }

        /// <summary>Component-wise sum. Used to fold class, level and equipment contributions together.</summary>
        public static StatSet Add(StatSet a, StatSet b)
        {
            var result = new StatSet(a);
            if (b == null)
            {
                return result;
            }

            foreach (KeyValuePair<StatType, float> pair in b)
            {
                result._values.TryGetValue(pair.Key, out float existing);
                result._values[pair.Key] = existing + pair.Value;
            }

            return result;
        }

        public IEnumerator<KeyValuePair<StatType, float>> GetEnumerator() => _values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
