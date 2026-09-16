using System;
using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Gameplay.Actors;
using EmberDepths.Gameplay.World;
using UnityEngine;

namespace EmberDepths.Gameplay.Combat
{
    /// <summary>
    /// The outcome of one damage application, after mitigation. A class rather
    /// than a struct because <see cref="Actor.ApplyDamage"/> fills in
    /// <see cref="Absorbed"/> once shields have taken their cut.
    /// </summary>
    public sealed class DamageResult
    {
        public Actor Source;
        public Actor Target;
        public DamageType Type;

        /// <summary>Post-mitigation damage, before shields.</summary>
        public float Amount;

        /// <summary>How much of <see cref="Amount"/> a shield ate.</summary>
        public float Absorbed;

        public bool Crit;

        /// <summary>Pre-mitigation number, kept for the damage meter and for tuning.</summary>
        public float Raw;

        public float ToHealth => Mathf.Max(0f, Amount - Absorbed);
    }

    /// <summary>
    /// The single place damage and healing are computed.
    ///
    /// Every source funnels through here — abilities, burning, standing in lava,
    /// falling rocks. That is what makes fire resistance actually mean "takes less
    /// fire damage" rather than "takes less damage from the four abilities someone
    /// remembered to wire it into".
    ///
    /// Mitigation order: crit, then armour (physical only), then school
    /// resistance, then the target's damage-taken multiplier. Order matters:
    /// applying resistance before armour would make armour scale with resistance.
    /// </summary>
    public sealed class CombatSystem
    {
        /// <summary>Armour value at which physical damage is halved.</summary>
        private const float ArmorHalvingConstant = 100f;

        /// <summary>Fraction of healing converted to threat, split across engaged enemies.</summary>
        private const float HealThreatFactor = 0.5f;

        private readonly DungeonWorld _world;
        private readonly List<Actor> _engagedScratch = new List<Actor>(16);

        public event Action<DamageResult> DamageDealt;
        public event Action<Actor, Actor, float, bool> HealApplied;

        public CombatSystem(DungeonWorld world)
        {
            _world = world;
        }

        /// <summary>
        /// <paramref name="coefficient"/> is a multiplier on the source's Power,
        /// not a final number. Authoring in coefficients means one Power change
        /// rescales an actor's whole kit consistently.
        /// </summary>
        public DamageResult DealDamage(
            Actor source,
            Actor target,
            float coefficient,
            DamageType type,
            bool canCrit = true,
            float threatMultiplier = 1f)
        {
            if (target == null || !target.IsAlive || coefficient <= 0f) return null;

            float power = source != null ? source.Stats.Power : 1f;
            float outgoing = source != null ? source.Stats.DamageDealtMultiplier : 1f;
            float raw = coefficient * power * outgoing;

            bool crit = false;
            if (canCrit && source != null && source.Stats.CritChance > 0f)
            {
                crit = _world.Rng.Chance(source.Stats.CritChance);
                if (crit) raw *= Mathf.Max(1f, source.Stats.CritMultiplier);
            }

            float mitigated = Mitigate(raw, type, target);

            var result = new DamageResult
            {
                Source = source,
                Target = target,
                Type = type,
                Raw = raw,
                Amount = mitigated,
                Crit = crit
            };

            target.ApplyDamage(result);
            GenerateDamageThreat(source, target, mitigated, threatMultiplier);
            DamageDealt?.Invoke(result);

            return result;
        }

        /// <summary>
        /// Damage authored as a final number rather than as a Power coefficient.
        /// Hazards and fall damage use this: "2% of max health per tick" must mean
        /// exactly that, whoever happens to own the pool.
        /// </summary>
        public DamageResult DealFlatDamage(Actor source, Actor target, float amount, DamageType type)
        {
            if (target == null || !target.IsAlive || amount <= 0f) return null;

            float mitigated = Mitigate(amount, type, target);

            var result = new DamageResult
            {
                Source = source,
                Target = target,
                Type = type,
                Raw = amount,
                Amount = mitigated,
                Crit = false
            };

            target.ApplyDamage(result);
            GenerateDamageThreat(source, target, mitigated, 1f);
            DamageDealt?.Invoke(result);
            return result;
        }

        private static float Mitigate(float raw, DamageType type, Actor target)
        {
            if (type == DamageType.True) return raw * target.Stats.DamageTakenMultiplier;

            float amount = raw;

            if (type == DamageType.Physical)
            {
                // Diminishing returns: armour never reaches immunity, and each
                // point is worth less than the last.
                float armor = Mathf.Max(0f, target.Stats.Armor);
                amount *= ArmorHalvingConstant / (ArmorHalvingConstant + armor);
            }

            float resist = target.Stats.ResistanceFor(type);
            amount *= 1f - resist;

            amount *= target.Stats.DamageTakenMultiplier;
            return Mathf.Max(0f, amount);
        }

        private void GenerateDamageThreat(Actor source, Actor target, float amount, float threatMultiplier)
        {
            if (source == null || amount <= 0f) return;
            if (target.Threat == null) return;              // target keeps no table (party member)
            if (!source.IsHostileTo(target)) return;        // friendly fire generates no aggro

            target.Threat.Add(source, amount * source.Stats.ThreatModifier * threatMultiplier);
        }

        public void Heal(Actor source, Actor target, float coefficient, bool canCrit = true, float threatMultiplier = 1f)
        {
            if (target == null || !target.IsAlive || coefficient <= 0f) return;

            float power = source != null ? source.Stats.Power : 1f;
            float amount = coefficient * power * (source != null ? source.Stats.DamageDealtMultiplier : 1f);

            bool crit = false;
            if (canCrit && source != null && source.Stats.CritChance > 0f)
            {
                crit = _world.Rng.Chance(source.Stats.CritChance);
                if (crit) amount *= Mathf.Max(1f, source.Stats.CritMultiplier);
            }

            float missing = target.MaxHealth - target.Health;
            if (missing <= 0f)
            {
                // Overhealing still counts as an action for the UI, but generates
                // no threat — otherwise spamming heals on a full party pulls rooms.
                HealApplied?.Invoke(source, target, 0f, crit);
                return;
            }

            float effective = Mathf.Min(amount, missing);
            target.ReceiveHeal(effective, source);
            GenerateHealThreat(source, effective * threatMultiplier);
            HealApplied?.Invoke(source, target, effective, crit);
        }

        /// <summary>
        /// Healing threat is spread across every enemy already fighting the party.
        /// A healer that never touches an enemy still climbs its threat table,
        /// which is exactly why the tank has to hold aggro for them.
        /// </summary>
        private void GenerateHealThreat(Actor healer, float amount)
        {
            if (healer == null || amount <= 0f) return;

            _engagedScratch.Clear();
            IReadOnlyList<Actor> enemies = _world.Actors.Hostiles;
            for (int i = 0; i < enemies.Count; i++)
            {
                Actor e = enemies[i];
                if (e != null && e.IsAlive && e.Threat != null && e.Threat.HasAnyThreat)
                    _engagedScratch.Add(e);
            }

            if (_engagedScratch.Count == 0) return;

            float share = amount * HealThreatFactor * healer.Stats.ThreatModifier / _engagedScratch.Count;
            for (int i = 0; i < _engagedScratch.Count; i++)
                _engagedScratch[i].Threat.Add(healer, share);
        }
    }
}
