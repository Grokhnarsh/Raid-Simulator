using RaidSim.Core.Common;
using RaidSim.Core.Stats;
using RaidSim.Core.Vitals;
using UnityEngine;

namespace RaidSim.Characters.Data
{
    /// <summary>
    /// Static definition of a playable class.
    /// </summary>
    /// <remarks>
    /// <para>This asset is <b>immutable authored data</b>. Nothing writes to it at runtime; a
    /// character built from it owns its own <see cref="StatBlock"/>. See
    /// <c>DATA_ARCHITECTURE.md</c>, "Static data versus runtime state".</para>
    /// <para>Adding a class is adding an asset. No code changes, no enum entries, no switch
    /// statements — which is the whole point of the class system being data.</para>
    /// <para>Abilities and the AI profile are referenced here from Phase 3 and Phase 5 onward; the
    /// fields are intentionally absent until those systems exist, so that the asset never carries a
    /// field nothing reads.</para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "Class_",
        menuName = "Raid Simulator/Characters/Class Definition",
        order = 10)]
    public sealed class ClassDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Shown in the UI. Presentation only: gameplay must never branch on this string.")]
        [SerializeField]
        private string _displayName = "Unnamed Class";

        [Tooltip("Short description of the class fantasy, for menus and tooltips.")]
        [TextArea(2, 4)]
        [SerializeField]
        private string _description = string.Empty;

        [Header("Role")]
        [Tooltip("Role this class fills in a group. Drives AI profile choice and raid-frame sorting.")]
        [SerializeField]
        private CombatRole _role = CombatRole.MeleeDamage;

        [Tooltip("What this class spends to use abilities. Mana starts full, rage starts empty.")]
        [SerializeField]
        private ResourceKind _resourceKind = ResourceKind.Mana;

        [Header("Base stats at level 1")]
        [Tooltip("Stats not listed here stay at their neutral value.")]
        [SerializeField]
        private StatEntry[] _baseStats = new StatEntry[0];

        [Header("Per-level growth")]
        [Tooltip("Added once per level beyond the first. Stats not listed here do not grow.")]
        [SerializeField]
        private StatEntry[] _statsPerLevel = new StatEntry[0];

        [Header("Positioning")]
        [Tooltip("Distance in metres at which this class prefers to fight. Melee classes use a small value.")]
        [Min(0f)]
        [SerializeField]
        private float _preferredCombatRange = 3f;

        [Tooltip("Degrees per second the character can turn.")]
        [Min(0f)]
        [SerializeField]
        private float _turnRateDegrees = 720f;

        [Tooltip("Horizontal radius of the character's footprint, in metres. Range checks measure between footprints.")]
        [Min(0.01f)]
        [SerializeField]
        private float _radius = 0.5f;

        public string DisplayName => _displayName;

        public string Description => _description;

        public CombatRole Role => _role;

        public ResourceKind ResourceKind => _resourceKind;

        public float PreferredCombatRange => _preferredCombatRange;

        public float TurnRateDegrees => _turnRateDegrees;

        public float Radius => _radius;

        /// <summary>
        /// Builds the base stats for <paramref name="level"/>: level-one stats plus growth for each
        /// level above the first.
        /// </summary>
        /// <remarks>
        /// Returns a fresh <see cref="StatSet"/> every call. The caller owns it, so a character can
        /// never mutate the asset it was created from.
        /// </remarks>
        public StatSet BuildBaseStats(int level)
        {
            StatSet stats = _baseStats.ToStatSet();
            int levelsAboveFirst = Mathf.Max(0, level - 1);
            if (levelsAboveFirst == 0)
            {
                return stats;
            }

            foreach (StatEntry growth in _statsPerLevel)
            {
                if (growth.Stat == StatType.None)
                {
                    continue;
                }

                stats[growth.Stat] = stats[growth.Stat] + (growth.Value * levelsAboveFirst);
            }

            return stats;
        }
    }
}
