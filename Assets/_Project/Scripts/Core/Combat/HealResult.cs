using RaidSim.Core.Common;

namespace RaidSim.Core.Combat
{
    /// <summary>What actually happened when a <see cref="HealRequest"/> was resolved.</summary>
    public readonly struct HealResult
    {
        public readonly EntityId Source;
        public readonly EntityId Target;

        /// <summary>Healing after every rule, before the health pool clamped it.</summary>
        public readonly float Amount;

        /// <summary>Healing actually added to health.</summary>
        public readonly float Applied;

        /// <summary>
        /// The part that landed on a full pool. Healers are judged on effective healing, so this has
        /// to be reported separately rather than inferred.
        /// </summary>
        public readonly float Overhealing;

        public readonly bool WasCritical;

        public readonly string SourceLabel;

        public HealResult(
            EntityId source,
            EntityId target,
            float amount,
            float applied,
            float overhealing,
            bool wasCritical,
            string sourceLabel)
        {
            Source = source;
            Target = target;
            Amount = amount;
            Applied = applied;
            Overhealing = overhealing;
            WasCritical = wasCritical;
            SourceLabel = sourceLabel;
        }

        public static HealResult None => default;

        public bool DidAnything => Applied > 0f;
    }
}
