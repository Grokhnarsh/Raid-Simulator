using System;
using EmberDepths.Core.Grid;
using UnityEngine;

namespace EmberDepths.Content
{
    /// <summary>
    /// One thing an ability does when it resolves. Abilities hold a list of these,
    /// serialised with <c>[SerializeReference]</c>, so a designer composes
    /// "damage + burn + knockback" in the inspector without new code.
    ///
    /// To add a verb: subclass this, mark it <c>[Serializable]</c>, and it appears
    /// in the ability inspector's add-effect menu. Keep <see cref="Apply"/> free of
    /// side effects other than the ones exposed on <see cref="IAbilityContext"/> —
    /// that is what keeps effects replayable and, later, networkable.
    /// </summary>
    [Serializable]
    public abstract class AbilityEffect
    {
        [Tooltip("Only fires for this fraction of casts. 1 = always.")]
        [Range(0f, 1f)]
        public float Probability = 1f;

        [Tooltip("Seconds after the cast resolves before this effect fires. Lets one ability stagger its hits.")]
        [Min(0f)]
        public float Delay;

        /// <summary>Human-readable summary shown in the tooltip and the content validator.</summary>
        public abstract string Describe();

        /// <summary>Runs the effect. Called once per cast, not once per target.</summary>
        public abstract void Apply(IAbilityContext ctx);
    }

    // -------------------------------------------------------------------------

    [Serializable]
    public sealed class DamageEffect : AbilityEffect
    {
        [Tooltip("Multiplied by the caster's Power to get the pre-mitigation number.")]
        public float Coefficient = 1f;

        public DamageType DamageType = DamageType.Physical;

        public bool CanCrit = true;

        [Tooltip("Damage falls off linearly to this fraction at the edge of the shape.")]
        [Range(0f, 1f)]
        public float EdgeFalloff = 1f;

        public override string Describe() =>
            $"{Coefficient:0.##}x {DamageType} damage" + (EdgeFalloff < 1f ? $" (to {EdgeFalloff:P0} at edge)" : "");

        public override void Apply(IAbilityContext ctx)
        {
            var targets = ctx.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                IActorHandle t = targets[i];
                float amount = Coefficient;

                if (EdgeFalloff < 1f)
                {
                    float dist = GridCoord.Euclidean(ctx.TargetCell, t.Cell);
                    float maxDist = Mathf.Max(1f, dist);
                    amount *= Mathf.Lerp(1f, EdgeFalloff, Mathf.Clamp01(dist / maxDist));
                }

                ctx.DealDamage(t, amount, DamageType, CanCrit);
            }
        }
    }

    [Serializable]
    public sealed class HealEffect : AbilityEffect
    {
        public float Coefficient = 1f;
        public bool CanCrit = true;

        [Tooltip("Heals the lowest-health target only, instead of everyone in the shape.")]
        public bool LowestHealthOnly;

        public override string Describe() =>
            $"{Coefficient:0.##}x healing" + (LowestHealthOnly ? " (lowest health)" : "");

        public override void Apply(IAbilityContext ctx)
        {
            var targets = ctx.Targets;
            if (targets.Count == 0) return;

            if (LowestHealthOnly)
            {
                IActorHandle best = null;
                for (int i = 0; i < targets.Count; i++)
                    if (best == null || targets[i].HealthFraction < best.HealthFraction)
                        best = targets[i];

                if (best != null) ctx.Heal(best, Coefficient, CanCrit);
                return;
            }

            for (int i = 0; i < targets.Count; i++)
                ctx.Heal(targets[i], Coefficient, CanCrit);
        }
    }

    [Serializable]
    public sealed class ShieldEffect : AbilityEffect
    {
        public float Coefficient = 1f;
        [Min(0.1f)] public float Duration = 8f;

        public override string Describe() => $"absorbs {Coefficient:0.##}x damage for {Duration:0.#}s";

        public override void Apply(IAbilityContext ctx)
        {
            var targets = ctx.Targets;
            for (int i = 0; i < targets.Count; i++)
                ctx.ApplyShield(targets[i], Coefficient, Duration);
        }
    }

    [Serializable]
    public sealed class ApplyStatusEffect : AbilityEffect
    {
        public StatusEffectDefinition Status;
        [Min(0.1f)] public float Duration = 6f;
        [Min(1)] public int Stacks = 1;

        [Tooltip("Apply to the caster rather than to the targets — for self-buffs on an offensive ability.")]
        public bool OnSelf;

        public override string Describe()
        {
            string who = OnSelf ? "self" : "targets";
            string name = Status != null ? Status.DisplayName : "<missing status>";
            return $"applies {name} x{Stacks} to {who} for {Duration:0.#}s";
        }

        public override void Apply(IAbilityContext ctx)
        {
            if (Status == null) return;

            if (OnSelf)
            {
                ctx.ApplyStatus(ctx.Caster, Status, Duration, Stacks);
                return;
            }

            var targets = ctx.Targets;
            for (int i = 0; i < targets.Count; i++)
                ctx.ApplyStatus(targets[i], Status, Duration, Stacks);
        }
    }

    [Serializable]
    public sealed class DispelEffect : AbilityEffect
    {
        [Min(1)] public int Count = 1;

        [Tooltip("Strip debuffs from allies (true) or buffs from enemies (false).")]
        public bool RemoveDebuffs = true;

        public override string Describe() =>
            $"removes {Count} {(RemoveDebuffs ? "debuff" : "buff")}{(Count > 1 ? "s" : "")}";

        public override void Apply(IAbilityContext ctx)
        {
            var targets = ctx.Targets;
            for (int i = 0; i < targets.Count; i++)
                ctx.Dispel(targets[i], Count, RemoveDebuffs);
        }
    }

    [Serializable]
    public sealed class KnockbackEffect : AbilityEffect
    {
        [Min(1)] public int Tiles = 2;

        [Tooltip("Push away from the caster instead of away from the impact point.")]
        public bool FromCaster;

        public override string Describe() => $"knocks back {Tiles} tiles";

        public override void Apply(IAbilityContext ctx)
        {
            GridCoord source = FromCaster ? ctx.Caster.Cell : ctx.TargetCell;
            var targets = ctx.Targets;
            for (int i = 0; i < targets.Count; i++)
                ctx.Knockback(targets[i], source, Tiles);
        }
    }

    [Serializable]
    public sealed class TauntEffect : AbilityEffect
    {
        [Min(0.5f)] public float Duration = 4f;

        [Tooltip("Threat added on top of the forced target, so aggro sticks after the taunt expires.")]
        public float BonusThreat = 200f;

        public override string Describe() => $"taunts for {Duration:0.#}s (+{BonusThreat:0} threat)";

        public override void Apply(IAbilityContext ctx)
        {
            var targets = ctx.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                ctx.Taunt(targets[i], ctx.Caster, Duration);
                ctx.AddThreat(targets[i], ctx.Caster, BonusThreat);
            }
        }
    }

    [Serializable]
    public sealed class ThreatEffect : AbilityEffect
    {
        [Tooltip("Negative values shed threat — the rogue's vanish, the mage's misdirect.")]
        public float Amount = 100f;

        public override string Describe() => Amount >= 0f ? $"generates {Amount:0} threat" : $"sheds {-Amount:0} threat";

        public override void Apply(IAbilityContext ctx)
        {
            var targets = ctx.Targets;
            for (int i = 0; i < targets.Count; i++)
                ctx.AddThreat(targets[i], ctx.Caster, Amount);
        }
    }

    [Serializable]
    public sealed class SpawnHazardEffect : AbilityEffect
    {
        public HazardDefinition Hazard;
        [Min(0)] public int Radius;
        [Min(0.5f)] public float Duration = 10f;

        [Tooltip("Drop it on the caster instead of the target cell.")]
        public bool AtCaster;

        public override string Describe()
        {
            string name = Hazard != null ? Hazard.DisplayName : "<missing hazard>";
            return $"leaves {name} (r{Radius}) for {Duration:0.#}s";
        }

        public override void Apply(IAbilityContext ctx)
        {
            if (Hazard == null) return;
            ctx.SpawnHazard(Hazard, AtCaster ? ctx.Caster.Cell : ctx.TargetCell, Radius, Duration);
        }
    }

    [Serializable]
    public sealed class SummonEffect : AbilityEffect
    {
        public EnemyDefinition Enemy;
        [Min(1)] public int Count = 2;

        public override string Describe()
        {
            string name = Enemy != null ? Enemy.DisplayName : "<missing enemy>";
            return $"summons {Count}x {name}";
        }

        public override void Apply(IAbilityContext ctx)
        {
            if (Enemy == null) return;
            ctx.Summon(Enemy, ctx.TargetCell, Count);
        }
    }

    [Serializable]
    public sealed class DashEffect : AbilityEffect
    {
        [Tooltip("Move the caster to the targeted cell. Blocked cells snap to the nearest reachable one.")]
        public bool ToTargetCell = true;

        public override string Describe() => "dashes the caster to the target";

        public override void Apply(IAbilityContext ctx)
        {
            if (!ToTargetCell) return;
            ctx.Displace(ctx.Caster, ctx.TargetCell);
        }
    }

    [Serializable]
    public sealed class VfxEffect : AbilityEffect
    {
        [Tooltip("Key looked up in the VFX catalogue. Presentation only — never changes simulation state.")]
        public string VfxKey = "impact_fire";

        public override string Describe() => $"plays vfx '{VfxKey}'";

        public override void Apply(IAbilityContext ctx) => ctx.PlayVfx(VfxKey, ctx.TargetCell);
    }
}
