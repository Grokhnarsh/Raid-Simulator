using System;
using System.Collections.Generic;
using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>
    /// One stage of a boss fight. Phases are entered in order as the boss loses
    /// health and never re-entered, so the fight always escalates.
    /// </summary>
    [Serializable]
    public sealed class BossPhase
    {
        public string Name = "Phase";

        [Range(0f, 1f)]
        [Tooltip("Entered when the boss drops to or below this health fraction. " +
                 "The first phase should be 1.")]
        public float EnterAtHealthFraction = 1f;

        [Tooltip("Shown across the screen when the phase begins. Leave empty for no banner.")]
        public string BannerText;

        [Header("Rotation")]
        [Tooltip("Cast in weighted-random order while in this phase. Leave empty to keep the " +
                 "previous phase's rotation and only change the modifiers below.")]
        public List<AbilityDefinition> Abilities = new List<AbilityDefinition>();

        [Tooltip("Seconds between special casts. The basic attack continues independently.")]
        [Min(0.5f)] public float AbilityInterval = 6f;

        [Header("Modifiers while in this phase")]
        [Min(0.1f)] public float DamageMultiplier = 1f;
        [Min(0.1f)] public float HasteMultiplier = 1f;
        [Range(0f, 0.95f)] public float DamageReduction;

        [Tooltip("Applied to the boss on entering, removed on leaving. Use for visual tinting too.")]
        public StatusEffectDefinition Aura;

        [Header("On enter")]
        [Tooltip("Fires once, the moment the phase begins — summon the adds, erupt the floor, " +
                 "knock the party back. The boss is the caster and its own cell is the target.")]
        [SerializeReference]
        public List<AbilityEffect> OnEnter = new List<AbilityEffect>();
    }

    /// <summary>
    /// The phase machine and enrage timer for a boss. Referenced from an
    /// <see cref="EnemyDefinition"/> whose rank is Boss.
    ///
    /// Kept separate from the enemy asset because a boss's *statistics* and a
    /// boss's *fight design* change at different times and usually by different
    /// people.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Boss Profile", fileName = "BS_NewBoss", order = 12)]
    public sealed class BossDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string Title = "the Unnamed";

        [TextArea(2, 4)]
        public string IntroLine;

        [Header("Phases")]
        [Tooltip("Ordered from full health downwards. The validator warns if they are out of order.")]
        public List<BossPhase> Phases = new List<BossPhase>();

        [Header("Enrage")]
        [Tooltip("Seconds of combat before the boss enrages. 0 disables the timer. " +
                 "An enrage is a soft DPS check: it should kill a party that stalls, not one that " +
                 "plays well.")]
        [Min(0f)] public float EnrageAfterSeconds = 300f;

        [Tooltip("Stacking buff applied once per second after the enrage timer expires.")]
        public StatusEffectDefinition EnrageStatus;

        [Header("Presentation")]
        public AudioClip Music;

        /// <summary>The phase that should be active at a given health fraction.</summary>
        public int PhaseIndexFor(float healthFraction)
        {
            int index = 0;
            for (int i = 0; i < Phases.Count; i++)
            {
                if (Phases[i] == null) continue;
                if (healthFraction <= Phases[i].EnterAtHealthFraction) index = i;
            }
            return index;
        }

        public BossPhase PhaseAt(int index) =>
            index >= 0 && index < Phases.Count ? Phases[index] : null;

        private void OnValidate()
        {
            // Phases must descend, or PhaseIndexFor picks a later phase too early
            // and the fight starts in its final stage.
            for (int i = 1; i < Phases.Count; i++)
            {
                if (Phases[i] == null || Phases[i - 1] == null) continue;
                if (Phases[i].EnterAtHealthFraction > Phases[i - 1].EnterAtHealthFraction)
                    Phases[i].EnterAtHealthFraction = Phases[i - 1].EnterAtHealthFraction;
            }
        }
    }
}
