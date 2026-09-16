using System;
using RaidSim.Core.Mathematics;
using RaidSim.Core.Stats;

namespace RaidSim.Core.Vitals
{
    /// <summary>
    /// Runtime health pool of one entity.
    /// </summary>
    /// <remarks>
    /// <para>The pool only owns the number. It deliberately raises no events and knows nothing about
    /// damage types, mitigation, overkill attribution or death handling — the combat system owns
    /// that pipeline and publishes the events (Phase 2). Keeping the pool this dumb is what makes
    /// the damage formula testable in isolation.</para>
    /// <para>Maximum health tracks <see cref="StatType.MaxHealth"/> so a buff that raises the pool
    /// raises it here too. When the maximum changes the current value keeps its <i>fraction</i>,
    /// which is the behaviour players expect from a temporary health buff.</para>
    /// </remarks>
    public sealed class Health
    {
        private readonly StatBlock _stats;
        private float _current;

        public Health(StatBlock stats)
        {
            _stats = stats ?? throw new ArgumentNullException(nameof(stats));
            _current = Max;
            _stats.StatChanged += OnStatChanged;
        }

        /// <summary>Current health. Always within <c>0..Max</c>.</summary>
        public float Current => _current;

        public float Max => _stats.Get(StatType.MaxHealth);

        /// <summary>Current health as a 0..1 fraction. Returns 0 when the pool has no size.</summary>
        public float Fraction => SimMath.SafeRatio(_current, Max);

        public bool IsAlive => _current > 0f;

        public bool IsFull => _current >= Max - SimMath.Epsilon;

        /// <summary>
        /// Removes health. Returns how much was actually removed, which is less than
        /// <paramref name="amount"/> on a killing blow — callers use the difference to report overkill.
        /// </summary>
        public float Remove(float amount)
        {
            if (amount <= 0f || !IsAlive)
            {
                return 0f;
            }

            float applied = Math.Min(amount, _current);
            _current -= applied;
            return applied;
        }

        /// <summary>
        /// Restores health. Returns how much was actually restored, so the healing pipeline can
        /// report overhealing. The dead are not healed; resurrection is an explicit operation.
        /// </summary>
        public float Add(float amount)
        {
            if (amount <= 0f || !IsAlive)
            {
                return 0f;
            }

            float applied = Math.Min(amount, Max - _current);
            _current += applied;
            return applied;
        }

        /// <summary>Sets health directly, clamped. For debug tooling, resets and save loading.</summary>
        public void Set(float value) => _current = SimMath.Clamp(value, 0f, Max);

        public void Fill() => _current = Max;

        /// <summary>Drops the pool to zero. The combat system decides what death means.</summary>
        public void Empty() => _current = 0f;

        /// <summary>Detaches from the stat block. Call when the entity leaves the simulation.</summary>
        public void Dispose() => _stats.StatChanged -= OnStatChanged;

        private void OnStatChanged(StatType stat)
        {
            if (stat != StatType.MaxHealth && stat != StatType.None)
            {
                return;
            }

            // Preserve the fraction so a max-health buff does not read as a sudden heal or hit.
            float max = Max;
            _current = SimMath.Clamp(_current, 0f, max);
        }
    }
}
