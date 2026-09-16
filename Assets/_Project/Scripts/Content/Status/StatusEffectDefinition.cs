using System.Collections.Generic;
using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>
    /// A buff or debuff. Everything a status can do is expressed as data here so
    /// that Burning, Chilled, Molten Armour and Enraged are all the same code path.
    ///
    /// The volcano biome leans on this heavily: almost every fire enemy applies a
    /// stacking Burn, and the party's answer is resist gear plus the cleric's
    /// dispel — both of which are just fields on this asset.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Status Effect", fileName = "ST_NewStatus", order = 30)]
    public sealed class StatusEffectDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "New Status";

        [TextArea(2, 4)]
        public string Description;

        public Sprite Icon;

        [Tooltip("Tints the icon and the actor's flash. Burn uses the lava ramp's mid orange.")]
        public Color Tint = Color.white;

        public bool IsDebuff = true;

        [Tooltip("Can be removed by a dispel. Boss mechanics usually set this false.")]
        public bool Dispellable = true;

        [Header("Stacking")]
        [Min(1)] public int MaxStacks = 1;

        [Tooltip("Re-applying refreshes the remaining duration instead of running two timers.")]
        public bool ReapplyRefreshesDuration = true;

        [Header("Periodic")]
        [Tooltip("Seconds between ticks. 0 disables the periodic part entirely.")]
        [Min(0f)] public float TickInterval = 1f;

        [Tooltip("Damage coefficient per tick, per stack. Scaled by the applier's Power at apply time.")]
        public float DamagePerTick;

        public DamageType TickDamageType = DamageType.Fire;

        [Tooltip("Healing coefficient per tick, per stack.")]
        public float HealPerTick;

        [Header("Stat modifiers (multiplicative, per stack)")]
        [Min(0f)] public float MoveSpeedMultiplier = 1f;
        [Min(0f)] public float HasteMultiplier = 1f;
        [Min(0f)] public float DamageDealtMultiplier = 1f;
        [Min(0f)] public float DamageTakenMultiplier = 1f;

        [Header("Stat modifiers (flat, per stack)")]
        public float ArmorBonus;
        public float FireResistBonus;

        [Header("Control")]
        [Tooltip("Cannot act at all.")]
        public bool Stuns;

        [Tooltip("Cannot move, but can still cast.")]
        public bool Roots;

        [Tooltip("Cannot cast, but can still move.")]
        public bool Silences;

        [Tooltip("Enemies cannot select this actor as a target.")]
        public bool Untargetable;

        [Header("On expire")]
        [Tooltip("Fires when the status runs out — the delayed-bomb pattern. " +
                 "The actor that applied the status counts as the caster.")]
        [SerializeReference]
        public List<AbilityEffect> OnExpire = new List<AbilityEffect>();

        /// <summary>True if this status has any per-tick behaviour worth scheduling.</summary>
        public bool IsPeriodic => TickInterval > 0f && (DamagePerTick != 0f || HealPerTick != 0f);

        public bool PreventsAction => Stuns;
        public bool PreventsMovement => Stuns || Roots;
        public bool PreventsCasting => Stuns || Silences;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = name;
            if (MaxStacks < 1) MaxStacks = 1;
        }
    }
}
