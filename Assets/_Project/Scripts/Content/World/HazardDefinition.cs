using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>
    /// A persistent patch of dangerous ground: a fire pool the boss leaves behind,
    /// the standing lava of the biome, a meteor scar.
    ///
    /// Hazards are the volcano's main pressure on party positioning, so they are
    /// first-class simulation objects rather than particle effects with a trigger
    /// collider — they occupy grid cells, they raise pathing cost, and the AI
    /// knows to walk out of them.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Hazard", fileName = "HZ_NewHazard", order = 31)]
    public sealed class HazardDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "Fire Pool";

        [Tooltip("Tint of the decal drawn on the floor.")]
        public Color Tint = new Color(1f, 0.48f, 0.11f, 0.65f);

        public Sprite Decal;

        [Header("Damage")]
        [Tooltip("Seconds between damage ticks for an actor standing inside.")]
        [Min(0.1f)] public float TickInterval = 0.5f;

        [Tooltip("Damage per tick as a fraction of the victim's MAX health. " +
                 "Percentage-based so a hazard stays threatening as the party gears up.")]
        [Range(0f, 0.5f)] public float PercentDamagePerTick = 0.02f;

        [Tooltip("Flat damage per tick, added on top of the percentage.")]
        public float FlatDamagePerTick = 2f;

        public DamageType DamageType = DamageType.Fire;

        [Header("Application")]
        [Tooltip("Applied to anyone standing inside. Usually the biome's Burn.")]
        public StatusEffectDefinition AppliesStatus;

        [Min(0.5f)] public float StatusDuration = 4f;

        [Header("Rules")]
        [Tooltip("Hurts the faction that spawned it too. Boss fire pools should say true.")]
        public bool FriendlyFire = true;

        [Tooltip("Actors with the LavaWalker flag ignore this hazard entirely.")]
        public bool LavaWalkersImmune = true;

        [Tooltip("Extra pathing cost per cell, so companion and enemy AI route around it.")]
        [Min(0f)] public float PathingPenalty = 8f;

        [Tooltip("Merges with an overlapping hazard of the same type instead of stacking damage. " +
                 "Without this, two overlapping pools double-dip and delete the party.")]
        public bool MergeWithSameType = true;

        [Header("Presentation")]
        public string SpawnVfxKey = "hazard_fire_spawn";
        public AudioClip AmbientLoop;

        /// <summary>Damage a specific actor takes from one tick.</summary>
        public float DamageFor(float victimMaxHealth) =>
            FlatDamagePerTick + victimMaxHealth * PercentDamagePerTick;
    }
}
