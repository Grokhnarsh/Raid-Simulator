using System;
using RaidSim.Core.Mathematics;
using RaidSim.Core.Stats;

namespace RaidSim.Core.Vitals
{
    /// <summary>
    /// What a class spends to use abilities.
    /// </summary>
    /// <remarks>
    /// <para>The kind of resource is authored per class (<see cref="ResourceKind"/>) rather than
    /// hard-coded, because mana and rage behave in opposite directions: mana starts full and
    /// regenerates, rage starts empty and is built in combat. Both are the same pool with a
    /// different fill policy.</para>
    /// </remarks>
    public sealed class ResourcePool
    {
        private readonly StatBlock _stats;
        private float _current;

        public ResourcePool(StatBlock stats, ResourceKind kind)
        {
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            Kind = kind;
            _current = kind.StartsFull() ? Max : 0f;
            _stats.StatChanged += OnStatChanged;
        }

        public ResourceKind Kind { get; }

        public float Current => _current;

        public float Max => _stats.Get(StatType.MaxResource);

        public float Fraction => SimMath.SafeRatio(_current, Max);

        /// <summary>Per-second passive regeneration, from <see cref="StatType.ResourceRegen"/>.</summary>
        public float RegenPerSecond => _stats.Get(StatType.ResourceRegen);

        public bool CanAfford(float cost) => cost <= _current + SimMath.Epsilon;

        /// <summary>
        /// Spends <paramref name="cost"/> if the pool can afford it. All-or-nothing: an ability
        /// either pays its full cost or does not fire.
        /// </summary>
        public bool TrySpend(float cost)
        {
            if (cost <= 0f)
            {
                return true;
            }

            if (!CanAfford(cost))
            {
                return false;
            }

            _current = Math.Max(0f, _current - cost);
            return true;
        }

        /// <summary>Grants resource. Returns the amount actually added after the cap.</summary>
        public float Add(float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }

            float applied = Math.Min(amount, Max - _current);
            if (applied <= 0f)
            {
                return 0f;
            }

            _current += applied;
            return applied;
        }

        /// <summary>Applies passive regeneration for <paramref name="deltaSeconds"/>.</summary>
        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            float regen = RegenPerSecond;
            if (regen > 0f)
            {
                Add(regen * deltaSeconds);
            }
        }

        public void Set(float value) => _current = SimMath.Clamp(value, 0f, Max);

        public void Fill() => _current = Max;

        public void Empty() => _current = 0f;

        /// <summary>Returns the pool to the state it has at the start of an encounter.</summary>
        public void ResetToEncounterStart()
        {
            if (Kind.StartsFull())
            {
                Fill();
            }
            else
            {
                Empty();
            }
        }

        public void Dispose() => _stats.StatChanged -= OnStatChanged;

        private void OnStatChanged(StatType stat)
        {
            if (stat != StatType.MaxResource && stat != StatType.None)
            {
                return;
            }

            _current = SimMath.Clamp(_current, 0f, Max);
        }
    }
}
