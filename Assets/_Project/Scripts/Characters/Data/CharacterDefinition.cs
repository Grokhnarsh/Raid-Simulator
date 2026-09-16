using RaidSim.Core.Common;
using RaidSim.Core.Stats;
using UnityEngine;

namespace RaidSim.Characters.Data
{
    /// <summary>
    /// Static definition of one concrete character: a class, a level, a name and optional overrides.
    /// </summary>
    /// <remarks>
    /// <para>The split between this and <see cref="ClassDefinition"/> is deliberate. The class holds
    /// everything shared by every character of that class; this holds only what makes one character
    /// different. That is what lets the same five slots later be filled by a player-built roster,
    /// a saved group, or a randomly generated one, without duplicating class data per character.</para>
    /// <para>The demo group is authored as assets of this type. Their names are labels for the UI
    /// and the combat log and nothing else — no gameplay code may branch on
    /// <see cref="DisplayName"/>.</para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "Character_",
        menuName = "Raid Simulator/Characters/Character Definition",
        order = 11)]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Shown in raid frames and the combat log. Presentation only.")]
        [SerializeField]
        private string _displayName = "Unnamed";

        [Tooltip("The class this character belongs to. Supplies role, resource kind and base stats.")]
        [SerializeField]
        private ClassDefinition _class;

        [Min(1)]
        [Tooltip("Character level. Scales the class's per-level stat growth.")]
        [SerializeField]
        private int _level = 1;

        [Header("Overrides")]
        [Tooltip("Stats listed here replace the value computed from the class. Leave empty for a standard character.")]
        [SerializeField]
        private StatEntry[] _statOverrides = new StatEntry[0];

        [Header("Presentation")]
        [Tooltip("Prefab used to represent this character in the world. Optional: bootstrap falls back to the default body.")]
        [SerializeField]
        private GameObject _viewPrefab;

        [Tooltip("Accent colour used for this character's raid frame, selection ring and nameplate.")]
        [SerializeField]
        private Color _accentColor = Color.white;

        public string DisplayName => _displayName;

        public ClassDefinition Class => _class;

        public int Level => Mathf.Max(1, _level);

        public GameObject ViewPrefab => _viewPrefab;

        public Color AccentColor => _accentColor;

        /// <summary>Role inherited from the class, or <see cref="CombatRole.None"/> if unassigned.</summary>
        public CombatRole Role => _class != null ? _class.Role : CombatRole.None;

        /// <summary>
        /// Final base stats for this character: the class's stats at this level, with any overrides
        /// applied on top.
        /// </summary>
        /// <returns>A fresh set the caller owns, or null when no class is assigned.</returns>
        public StatSet BuildBaseStats()
        {
            if (_class == null)
            {
                return null;
            }

            StatSet stats = _class.BuildBaseStats(Level);
            foreach (StatEntry over in _statOverrides)
            {
                if (over.Stat != StatType.None)
                {
                    stats[over.Stat] = over.Value;
                }
            }

            return stats;
        }

        /// <summary>
        /// Reports why this asset cannot produce a character, or null when it is complete.
        /// Bootstrap calls this so a mis-authored asset fails loudly at load instead of producing a
        /// silently broken raider.
        /// </summary>
        public string Validate()
        {
            if (_class == null)
            {
                return $"'{name}' has no class assigned.";
            }

            return null;
        }
    }
}
