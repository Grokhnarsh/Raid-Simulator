using RaidSim.Core.Combat;
using UnityEngine;

namespace RaidSim.Combat.Data
{
    /// <summary>
    /// Authored balance numbers for the damage and healing pipelines.
    /// </summary>
    /// <remarks>
    /// <para>Mitigation follows <c>rating / (rating + K)</c>. The shape of that curve is a system
    /// rule and lives in code; the constants that position it are tuning and live here — see
    /// <c>CLAUDE.md</c>, architecture rule 5.</para>
    /// <para>Read the constants as "how much rating is needed for 50% reduction against an attacker
    /// of a given level". At <c>ArmorConstantPerLevel = 55</c>, a level-60 attacker needs the target
    /// to have 3300 armour to halve the hit.</para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "CombatTuning",
        menuName = "Raid Simulator/Combat/Combat Tuning",
        order = 20)]
    public sealed class CombatTuningAsset : ScriptableObject
    {
        [Header("Mitigation")]
        [Tooltip("Armour needed for 50% physical reduction, per level of the attacker. Higher means armour is worth less.")]
        [Min(0.01f)]
        [SerializeField]
        private float _armorConstantPerLevel = 55f;

        [Tooltip("Resistance needed for 50% magical reduction, per level of the attacker.")]
        [Min(0.01f)]
        [SerializeField]
        private float _resistanceConstantPerLevel = 55f;

        [Tooltip("Hard ceiling on the fraction of damage that rating alone may remove. Immunity is an effect, not a gear threshold.")]
        [Range(0f, 0.99f)]
        [SerializeField]
        private float _maximumMitigation = 0.75f;

        [Header("Randomness")]
        [Tooltip("Seed for every roll the simulation makes. A fixed seed makes an encounter replayable.")]
        [SerializeField]
        private int _randomSeed = 1337;

        [Tooltip("When off, the seed above is ignored and a fresh one is taken from the clock on each run.")]
        [SerializeField]
        private bool _useFixedSeed = true;

        public int RandomSeed => _randomSeed;

        public bool UseFixedSeed => _useFixedSeed;

        /// <summary>
        /// Builds the immutable snapshot the pipelines use. Returns a fresh instance every call, so
        /// nothing at runtime can write back into this asset.
        /// </summary>
        public CombatTuning ToRuntime() => new CombatTuning(
            _armorConstantPerLevel,
            _resistanceConstantPerLevel,
            _maximumMitigation);

        /// <summary>
        /// Reports why this asset cannot be used, or null when it is complete.
        /// </summary>
        public string Validate()
        {
            if (_armorConstantPerLevel <= 0f || _resistanceConstantPerLevel <= 0f)
            {
                return $"'{name}' has a non-positive mitigation constant; it is the denominator of the mitigation curve.";
            }

            return null;
        }
    }
}
