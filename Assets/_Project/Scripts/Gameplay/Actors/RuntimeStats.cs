using EmberDepths.Content;
using EmberDepths.Gameplay.Items;
using UnityEngine;

namespace EmberDepths.Gameplay.Actors
{
    /// <summary>
    /// An actor's live numbers: the authored <see cref="StatBlock"/> with every
    /// active status folded in.
    ///
    /// Recomputed from scratch whenever statuses change rather than incrementally
    /// adjusted. That is slightly more work per change and dramatically less
    /// error-prone — incremental modifier bookkeeping is where buff systems
    /// classically drift and leave permanent +20% damage behind.
    /// </summary>
    public sealed class RuntimeStats
    {
        public StatBlock Base { get; private set; }

        public float MaxHealth { get; private set; }
        public float Armor { get; private set; }
        public float Power { get; private set; }
        public float MoveSpeed { get; private set; }
        public float Haste { get; private set; }
        public float CritChance { get; private set; }
        public float CritMultiplier { get; private set; }
        public float ThreatModifier { get; private set; }
        public float MaxResource { get; private set; }
        public float ResourceRegen { get; private set; }

        public float DamageDealtMultiplier { get; private set; }
        public float DamageTakenMultiplier { get; private set; }

        private float _fireResist, _frostResist, _arcaneResist;

        public bool Stunned { get; private set; }
        public bool Rooted { get; private set; }
        public bool Silenced { get; private set; }
        public bool Untargetable { get; private set; }

        public RuntimeStats(StatBlock baseStats)
        {
            Base = baseStats;
            ResetToBase();
        }

        public void SetBase(StatBlock baseStats)
        {
            Base = baseStats;
            ResetToBase();
        }

        public float ResistanceFor(DamageType type) => type switch
        {
            DamageType.Fire => _fireResist,
            DamageType.Frost => _frostResist,
            DamageType.Arcane => _arcaneResist,
            _ => 0f
        };

        public bool CanAct => !Stunned;
        public bool CanMove => !Stunned && !Rooted;
        public bool CanCast => !Stunned && !Silenced;

        private void ResetToBase()
        {
            MaxHealth = Base.MaxHealth;
            Armor = Base.Armor;
            Power = Base.Power;
            MoveSpeed = Base.MoveSpeed;
            Haste = Base.Haste;
            CritChance = Base.CritChance;
            CritMultiplier = Base.CritMultiplier;
            ThreatModifier = Base.ThreatModifier;
            MaxResource = Base.MaxResource;
            ResourceRegen = Base.ResourceRegen;

            DamageDealtMultiplier = 1f;
            DamageTakenMultiplier = 1f;

            _fireResist = Base.FireResist;
            _frostResist = Base.FrostResist;
            _arcaneResist = Base.ArcaneResist;

            Stunned = Rooted = Silenced = Untargetable = false;
        }

        /// <summary>
        /// Rebuilds every derived value from the base block, the worn equipment
        /// and the active statuses.
        ///
        /// Order is deliberate and load-bearing: <b>base → equipment → statuses</b>.
        /// Gear is treated as part of who the character is, so a temporary +20%
        /// damage buff multiplies the geared number rather than the naked one.
        /// The reverse order would make every buff weaker the better your gear,
        /// which is exactly backwards from what a player expects.
        /// </summary>
        public void Recompute(StatusController statuses, Equipment equipment = null)
        {
            ResetToBase();
            ApplyEquipment(equipment);
            ApplyStatuses(statuses);
            ClampDerived();
        }

        private void ApplyEquipment(Equipment equipment)
        {
            if (equipment == null) return;

            var accumulator = StatAccumulator.Create();
            equipment.CollectModifiers(ref accumulator);

            MaxHealth = accumulator.Apply(StatKind.MaxHealth, MaxHealth);
            Armor = accumulator.Apply(StatKind.Armor, Armor);
            Power = accumulator.Apply(StatKind.Power, Power);
            MoveSpeed = accumulator.Apply(StatKind.MoveSpeed, MoveSpeed);
            Haste = accumulator.Apply(StatKind.Haste, Haste);
            MaxResource = accumulator.Apply(StatKind.MaxResource, MaxResource);
            ResourceRegen = accumulator.Apply(StatKind.ResourceRegen, ResourceRegen);
            ThreatModifier = accumulator.Apply(StatKind.ThreatModifier, ThreatModifier);

            // Crit chance and resistances are already fractions, so a "percent"
            // modifier on them would compound confusingly. Both parts add.
            CritChance += accumulator.Flat(StatKind.CritChance) + accumulator.Percent(StatKind.CritChance);
            CritMultiplier += accumulator.Flat(StatKind.CritMultiplier) + accumulator.Percent(StatKind.CritMultiplier);

            _fireResist += accumulator.Flat(StatKind.FireResist) + accumulator.Percent(StatKind.FireResist);
            _frostResist += accumulator.Flat(StatKind.FrostResist) + accumulator.Percent(StatKind.FrostResist);
            _arcaneResist += accumulator.Flat(StatKind.ArcaneResist) + accumulator.Percent(StatKind.ArcaneResist);

            DamageDealtMultiplier = accumulator.ApplyMultiplier(StatKind.DamageDealt, DamageDealtMultiplier);
            DamageTakenMultiplier = accumulator.ApplyMultiplier(StatKind.DamageTaken, DamageTakenMultiplier);
        }

        private void ApplyStatuses(StatusController statuses)
        {
            if (statuses == null) return;

            for (int i = 0; i < statuses.Active.Count; i++)
            {
                ActiveStatus s = statuses.Active[i];
                StatusEffectDefinition d = s.Definition;
                if (d == null) continue;

                int stacks = Mathf.Max(1, s.Stacks);

                // Multiplicative modifiers compound: two stacks of a 0.7x slow give
                // 0.49x, not 0.4x. Compounding keeps stacked slows from reaching zero.
                MoveSpeed *= Mathf.Pow(d.MoveSpeedMultiplier, stacks);
                Haste *= Mathf.Pow(d.HasteMultiplier, stacks);
                DamageDealtMultiplier *= Mathf.Pow(d.DamageDealtMultiplier, stacks);
                DamageTakenMultiplier *= Mathf.Pow(d.DamageTakenMultiplier, stacks);

                Armor += d.ArmorBonus * stacks;
                _fireResist += d.FireResistBonus * stacks;

                if (d.Stuns) Stunned = true;
                if (d.Roots) Rooted = true;
                if (d.Silences) Silenced = true;
                if (d.Untargetable) Untargetable = true;
            }
        }

        private void ClampDerived()
        {
            // Resistance is a fraction of damage ignored; at 1.0 an actor would be
            // immortal against a school, which no amount of gear or buffs should
            // be able to reach. The cap is the reason fire resistance can be a
            // headline stat in this biome without trivialising it.
            _fireResist = Mathf.Clamp(_fireResist, -1f, 0.9f);
            _frostResist = Mathf.Clamp(_frostResist, -1f, 0.9f);
            _arcaneResist = Mathf.Clamp(_arcaneResist, -1f, 0.9f);

            MaxHealth = Mathf.Max(1f, MaxHealth);
            Armor = Mathf.Max(0f, Armor);
            Power = Mathf.Max(0f, Power);
            MoveSpeed = Mathf.Max(0f, MoveSpeed);
            Haste = Mathf.Max(0.1f, Haste);
            CritChance = Mathf.Clamp01(CritChance);
            CritMultiplier = Mathf.Max(1f, CritMultiplier);
            ThreatModifier = Mathf.Max(0f, ThreatModifier);
            DamageDealtMultiplier = Mathf.Max(0f, DamageDealtMultiplier);
            DamageTakenMultiplier = Mathf.Max(0f, DamageTakenMultiplier);
        }
    }
}
