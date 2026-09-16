using System.Collections.Generic;
using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>
    /// One of the five party archetypes.
    ///
    /// The party is five slots, and this asset is what fills a slot. Adding a
    /// sixth class is a new asset plus a line in the roster — no code. Whether a
    /// slot is driven by a human or by the companion AI is a runtime decision, not
    /// a property of the class, which is what keeps single-player and future
    /// multiplayer on one code path.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Class", fileName = "CL_NewClass", order = 10)]
    public sealed class ClassDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "New Class";

        [TextArea(2, 5)]
        public string Description;

        public ActorRole Role = ActorRole.MeleeDps;

        [Tooltip("Used for the party frame accent and the selection ring. Keep these to the cool " +
                 "half of the palette so five party members stay readable on an orange floor.")]
        public Color AccentColor = new Color(0.36f, 0.78f, 0.76f);

        [Header("Art")]
        public ActorVisualSet Visuals;

        [Header("Numbers")]
        public StatBlock BaseStats = StatBlock.Default;

        [Header("Abilities")]
        [Tooltip("Used automatically when in range and nothing better is available.")]
        public AbilityDefinition BasicAttack;

        [Tooltip("Slotted onto the action bar in order. Six is what the UI lays out cleanly.")]
        public List<AbilityDefinition> Abilities = new List<AbilityDefinition>();

        [Header("Starting gear")]
        [Tooltip("Equipped at spawn, one per slot. Keep it modest — the run's power curve " +
                 "should come from what the party finds, not from what it began with.")]
        public List<ItemDefinition> StartingGear = new List<ItemDefinition>();

        [Header("Companion AI")]
        [Tooltip("Tiles the companion tries to keep between itself and its target. " +
                 "1 for melee, 5-7 for ranged and casters.")]
        [Min(1)] public int PreferredRange = 1;

        [Tooltip("How far the companion will stray from the party leader before returning.")]
        [Min(2)] public int FollowRadius = 6;

        [Tooltip("Healers and casters step out of hazards sooner. Higher = more skittish.")]
        [Range(0f, 1f)] public float HazardAvoidance = 0.6f;

        /// <summary>Every ability including the basic attack, for validation and tooltips.</summary>
        public IEnumerable<AbilityDefinition> AllAbilities()
        {
            if (BasicAttack != null) yield return BasicAttack;
            for (int i = 0; i < Abilities.Count; i++)
                if (Abilities[i] != null) yield return Abilities[i];
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = name;

            // Roles carry real mechanical weight in the threat model, so nudge the
            // threat modifier towards something sane when it is still at default.
            if (Mathf.Approximately(BaseStats.ThreatModifier, 1f))
            {
                BaseStats.ThreatModifier = Role switch
                {
                    ActorRole.Tank => 4f,
                    ActorRole.Healer => 0.5f,
                    _ => 1f
                };
            }
        }
    }
}
