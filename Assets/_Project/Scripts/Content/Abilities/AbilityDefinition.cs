using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>Which side of the fight an ability's shape collects.</summary>
    public enum AffectsFaction
    {
        Enemies = 0,
        Allies = 1,
        /// <summary>Friendly fire. Boss meteors use this so the party can bait adds into them.</summary>
        Everyone = 2
    }

    /// <summary>
    /// One castable action. Shared by players, trash and the boss — a "boss
    /// mechanic" is just an ability with a long telegraph and a big shape, which
    /// keeps one resolution path in the simulation instead of two.
    /// </summary>
    [CreateAssetMenu(menuName = "EmberDepths/Ability", fileName = "AB_NewAbility", order = 20)]
    public sealed class AbilityDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string DisplayName = "New Ability";

        [TextArea(2, 5)]
        public string Description;

        public Sprite Icon;

        [Header("Targeting")]
        public TargetKind TargetKind = TargetKind.Enemy;
        public AffectsFaction AffectsFaction = AffectsFaction.Enemies;
        public TargetShape Shape = TargetShape.Single;

        [Tooltip("Maximum distance to the target, in tiles (Chebyshev). 1 means melee.")]
        [Min(1)] public int Range = 1;

        [Tooltip("Walls block the cast. Turn off for ground-targeted artillery.")]
        public bool RequiresLineOfSight = true;

        [Tooltip("Cells inside the shape that are behind a wall are skipped.")]
        public bool ShapeRespectsLineOfSight = true;

        [Header("Timing")]
        [Tooltip("Seconds of cast time. 0 is instant. Movement cancels a cast unless CastableWhileMoving.")]
        [Min(0f)] public float CastTime;

        public bool CastableWhileMoving;

        [Min(0f)] public float Cooldown = 3f;

        [Tooltip("Also puts the actor on the shared global cooldown.")]
        public bool TriggersGlobalCooldown = true;

        [Tooltip("Seconds the danger zone is shown before the effect lands. Enemy abilities " +
                 "should always set this above zero — an untelegraphed AoE reads as unfair.")]
        [Min(0f)] public float TelegraphLeadTime;

        [Header("Cost")]
        public float ResourceCost;

        [Header("Effects")]
        [Tooltip("Resolved in order when the cast completes.")]
        [SerializeReference]
        public List<AbilityEffect> Effects = new List<AbilityEffect>();

        [Header("Threat")]
        [Tooltip("Multiplies threat from this ability's damage and healing. " +
                 "The tank's threat builders sit well above 1.")]
        [Min(0f)] public float ThreatMultiplier = 1f;

        [Header("AI hints")]
        [Tooltip("Higher wins when several abilities are off cooldown. Ties break on longest cooldown.")]
        public float AiPriority = 1f;

        [Tooltip("AI will not cast an AoE that would hit fewer than this many targets.")]
        [Min(1)] public int AiMinTargets = 1;

        [Range(0f, 1f)]
        [Tooltip("AI only casts this when the caster's health is at or below this fraction. 1 = no restriction.")]
        public float AiUseBelowSelfHealth = 1f;

        [Range(0f, 1f)]
        [Tooltip("AI only casts this when the intended target is at or below this fraction. 1 = no restriction.")]
        public float AiUseBelowTargetHealth = 1f;

        [Header("Presentation")]
        public string CastVfxKey;
        public string ImpactVfxKey;
        public AudioClip CastSound;

        /// <summary>Total time from cast start to effects landing.</summary>
        public float TimeToImpact => CastTime + TelegraphLeadTime;

        public bool IsAreaEffect => Shape.Kind != ShapeKind.Single;

        /// <summary>Generated tooltip text, also used by the content validator's report.</summary>
        public string BuildTooltip()
        {
            var sb = new StringBuilder();
            sb.Append(DisplayName);

            if (CastTime > 0f) sb.Append($"  ·  {CastTime:0.#}s cast");
            else sb.Append("  ·  instant");

            if (Cooldown > 0f) sb.Append($"  ·  {Cooldown:0.#}s cd");
            if (Range > 1) sb.Append($"  ·  {Range} tiles");

            if (!string.IsNullOrWhiteSpace(Description))
            {
                sb.AppendLine();
                sb.Append(Description);
            }

            for (int i = 0; i < Effects.Count; i++)
            {
                if (Effects[i] == null) continue;
                sb.AppendLine();
                sb.Append("• ");
                sb.Append(Effects[i].Describe());
            }

            return sb.ToString();
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(DisplayName)) DisplayName = name;

            // A ground-targeted ability that insists on an actor target can never
            // be cast; catch it at author time rather than at runtime.
            if (TargetKind == TargetKind.Ground && Shape.Kind == ShapeKind.Single && Range <= 1)
                Range = 2;
        }
    }
}
