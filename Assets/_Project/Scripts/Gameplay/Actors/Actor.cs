using System;
using System.Collections.Generic;
using EmberDepths.Content;
using EmberDepths.Core.Grid;
using EmberDepths.Core.Sim;
using EmberDepths.Gameplay.Combat;
using EmberDepths.Gameplay.Items;
using EmberDepths.Gameplay.World;
using UnityEngine;

namespace EmberDepths.Gameplay.Actors
{
    /// <summary>
    /// One living thing in the dungeon — a party member, a trash mob, the boss.
    ///
    /// There is deliberately no separate Player and Enemy class. A boss is an
    /// Actor with a boss brain and a big <see cref="StatBlock"/>; a party member
    /// is an Actor whose intents come from a controller instead of an AI. Keeping
    /// one type means every mechanic (threat, knockback, burning, dispel) works
    /// uniformly on everyone, including the cases nobody thought to test.
    ///
    /// Simulation state lives here. Everything visual lives on
    /// <see cref="ActorView"/>, which reads this and never writes to it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Actor : MonoBehaviour, IActorHandle
    {
        // --- identity -----------------------------------------------------------

        public int Id { get; private set; }
        public string DisplayName { get; set; } = "Actor";
        public Faction Faction { get; private set; }
        public EnemyRank Rank { get; private set; }
        public ActorRole Role { get; private set; }
        public ActorFlags Flags { get; set; }

        public ClassDefinition ClassDefinition { get; private set; }
        public EnemyDefinition EnemyDefinition { get; private set; }
        public ActorVisualSet Visuals { get; private set; }

        public DungeonWorld World { get; private set; }

        // --- spatial ------------------------------------------------------------

        public GridCoord Cell { get; private set; }
        public GridCoord SpawnCell { get; private set; }
        public IsoDirection Facing { get; private set; } = IsoDirection.South;

        public ActorMotor Motor { get; private set; }

        // --- vitals -------------------------------------------------------------

        public float Health { get; private set; }
        public float Shield { get; private set; }
        public float Resource { get; private set; }

        public RuntimeStats Stats { get; private set; }
        public StatusController Status { get; private set; }
        public AbilityBook Abilities { get; private set; }

        /// <summary>Only enemies keep a threat table; party members read it, never own one.</summary>
        public ThreatTable Threat { get; private set; }

        /// <summary>
        /// What this actor is wearing. Present on everyone, not just the party:
        /// an elite in themed gear is a cheap way to make a pack feel authored,
        /// and it costs nothing while the slots are empty.
        /// </summary>
        public Equipment Equipment { get; private set; }

        public float MaxHealth => Stats?.MaxHealth ?? 0f;
        public bool IsAlive => Health > 0f;
        public float HealthFraction => MaxHealth <= 0f ? 0f : Mathf.Clamp01(Health / MaxHealth);
        public bool IsHostileTo(Actor other) =>
            other != null && Faction != Faction.Neutral && other.Faction != Faction.Neutral && other.Faction != Faction;

        // --- events (presentation and UI subscribe; simulation never does) -------

        public event Action<Actor, DamageResult> Damaged;
        public event Action<Actor, float, Actor> Healed;
        public event Action<Actor, Actor> Died;
        public event Action<Actor> CellChanged;
        public event Action<Actor> VitalsChanged;

        /// <summary>
        /// Duration used for set-bonus auras. They are removed explicitly when
        /// the set comes off, so this only has to outlast any plausible run.
        /// </summary>
        private const float EquipmentAuraSeconds = 86400f;

        private int _shieldExpiresAtTick = int.MinValue;
        private int _lastDamageTick = int.MinValue;
        private bool _initialised;

        private readonly List<StatusEffectDefinition> _activeEquipmentAuras = new List<StatusEffectDefinition>(4);
        private readonly List<StatusEffectDefinition> _auraScratch = new List<StatusEffectDefinition>(4);
        private readonly List<AbilityDefinition> _grantedScratch = new List<AbilityDefinition>(4);

        // -------------------------------------------------------------------------

        public void InitialiseAsPartyMember(DungeonWorld world, int id, ClassDefinition def, string displayName, GridCoord cell)
        {
            ClassDefinition = def;
            Role = def != null ? def.Role : ActorRole.MeleeDps;
            Visuals = def != null ? def.Visuals : null;
            Rank = EnemyRank.Trash;

            Initialise(world, id, Faction.Party, displayName,
                def != null ? def.BaseStats : StatBlock.Default, cell);

            if (def != null)
            {
                Abilities.Load(def.BasicAttack, def.Abilities);
            }
        }

        public void InitialiseAsEnemy(DungeonWorld world, int id, EnemyDefinition def, GridCoord cell)
        {
            EnemyDefinition = def;
            Rank = def != null ? def.Rank : EnemyRank.Trash;
            Visuals = def != null ? def.Visuals : null;
            Flags = def != null ? def.Flags : ActorFlags.None;
            Role = ActorRole.MeleeDps;

            Initialise(world, id, Faction.Hostile,
                def != null ? def.DisplayName : "Enemy",
                def != null ? def.ResolvedStats : StatBlock.Default, cell);

            Threat = new ThreatTable();

            if (def != null)
            {
                Abilities.Load(def.BasicAttack, def.Abilities);
            }
        }

        private void Initialise(DungeonWorld world, int id, Faction faction, string displayName, StatBlock stats, GridCoord cell)
        {
            World = world;
            Id = id;
            Faction = faction;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? name : displayName;

            Stats = new RuntimeStats(stats);
            Status = new StatusController(this);
            Status.Changed += OnStatusChanged;
            Status.Expired += OnStatusExpired;

            Equipment = new Equipment();
            Equipment.Changed += OnEquipmentChanged;

            Abilities = new AbilityBook(this);
            Motor = new ActorMotor(this);

            Cell = SpawnCell = cell;
            Motor.ResetTo(cell);
            world.Map.TryOccupy(cell, Id);

            Health = Stats.MaxHealth;
            Resource = Stats.MaxResource;
            Shield = 0f;

            transform.position = IsoGrid.CellToWorld(cell);
            _initialised = true;
        }

        // --- simulation -----------------------------------------------------------

        /// <summary>One simulation step. Driven by <see cref="DungeonWorld"/>, never by Update.</summary>
        public void Tick(int tick)
        {
            if (!_initialised) return;

            if (!IsAlive)
            {
                Motor.HardStop();
                return;
            }

            Status.Tick(tick);
            if (!IsAlive) return;

            if (Shield > 0f && tick >= _shieldExpiresAtTick)
            {
                Shield = 0f;
                VitalsChanged?.Invoke(this);
            }

            Abilities.Tick(tick);
            Motor.Tick();

            RegenerateResource();
        }

        private void RegenerateResource()
        {
            if (Stats.MaxResource <= 0f || Stats.ResourceRegen <= 0f) return;
            if (Resource >= Stats.MaxResource) return;

            Resource = Mathf.Min(Stats.MaxResource, Resource + Stats.ResourceRegen * SimClock.TickDuration);
            VitalsChanged?.Invoke(this);
        }

        // --- damage and healing ----------------------------------------------------

        /// <summary>
        /// Applies an already-mitigated damage result. Mitigation happens in
        /// <see cref="CombatSystem"/> so that every source — abilities, hazards,
        /// damage-over-time — goes through one set of rules.
        /// </summary>
        public void ApplyDamage(DamageResult result)
        {
            if (!IsAlive || result.Amount <= 0f) return;

            float remaining = result.Amount;

            if (Shield > 0f)
            {
                float absorbed = Mathf.Min(Shield, remaining);
                Shield -= absorbed;
                remaining -= absorbed;
                result.Absorbed = absorbed;
            }

            Health = Mathf.Max(0f, Health - remaining);
            _lastDamageTick = World != null ? World.Clock.Tick : 0;

            Damaged?.Invoke(this, result);
            VitalsChanged?.Invoke(this);

            if (Health <= 0f) Die(result.Source);
        }

        /// <summary>Entry point for status ticks; routes through the same mitigation path.</summary>
        public void TakePeriodicDamage(float rawAmount, DamageType type, Actor source)
        {
            if (!IsAlive || World == null) return;
            World.Combat.DealDamage(source, this, rawAmount, type, canCrit: false, threatMultiplier: 1f);
        }

        public void ReceiveHeal(float amount, Actor source)
        {
            if (!IsAlive || amount <= 0f) return;

            float before = Health;
            Health = Mathf.Min(MaxHealth, Health + amount);
            float actual = Health - before;
            if (actual <= 0f) return;

            Healed?.Invoke(this, actual, source);
            VitalsChanged?.Invoke(this);
        }

        public void ReceivePeriodicHeal(float amount, Actor source)
        {
            if (World == null) return;
            World.Combat.Heal(source, this, amount, canCrit: false);
        }

        public void ApplyShield(float amount, float durationSeconds)
        {
            if (!IsAlive || amount <= 0f) return;

            // Shields refresh rather than stack: the strongest wins. Stacking
            // absorbs is a balance hole that only shows up in long fights.
            Shield = Mathf.Max(Shield, amount);
            _shieldExpiresAtTick = World.Clock.Tick + SimClock.SecondsToTicks(durationSeconds);
            VitalsChanged?.Invoke(this);
        }

        public bool SpendResource(float amount)
        {
            if (amount <= 0f) return true;
            if (Resource < amount) return false;
            Resource -= amount;
            VitalsChanged?.Invoke(this);
            return true;
        }

        private void Die(Actor killer)
        {
            Motor.HardStop();
            Status.Clear();
            Abilities.CancelCast();
            World.Map.Vacate(Cell, Id);

            Died?.Invoke(this, killer);
            World.NotifyActorDied(this, killer);
        }

        // --- movement --------------------------------------------------------------

        /// <summary>Called by the motor when a step completes.</summary>
        public void ArriveAt(GridCoord cell)
        {
            if (cell == Cell) return;

            World.Map.Vacate(Cell, Id);
            Cell = cell;
            // The motor already reserved the destination before stepping, so this
            // only re-asserts ownership after the vacate above.
            World.Map.TryOccupy(cell, Id);

            CellChanged?.Invoke(this);
            World.Hazards.OnActorEnteredCell(this, cell);
        }

        /// <summary>Immediate relocation. Used by dashes, knockback and spawning.</summary>
        public void TeleportTo(GridCoord cell)
        {
            World.Map.Vacate(Cell, Id);

            if (!World.Map.TryFindFreeCellNear(cell, 4, out GridCoord landing))
                landing = Cell;

            Cell = landing;
            World.Map.TryOccupy(Cell, Id);
            Motor.ResetTo(Cell);
            transform.position = IsoGrid.CellToWorld(Cell);

            CellChanged?.Invoke(this);
            World.Hazards.OnActorEnteredCell(this, Cell);
        }

        public void SetFacing(IsoDirection dir) => Facing = dir;

        public void FaceTowards(GridCoord target)
        {
            if (target == Cell) return;
            Facing = IsoDirectionExtensions.FromGridStep(Cell, target, Facing);
        }

        // --- IActorHandle -----------------------------------------------------------

        public bool HasStatus(StatusEffectDefinition def) => Status != null && Status.Has(def);
        public int StatusStacks(StatusEffectDefinition def) => Status != null ? Status.StacksOf(def) : 0;

        // --- internals ----------------------------------------------------------------

        private void OnStatusChanged() => RefreshStats();

        /// <summary>
        /// Equipment changed: re-sync the auras and abilities it grants, then
        /// recompute.
        ///
        /// Aura sync happens before the recompute because applying or removing a
        /// status fires <see cref="StatusController.Changed"/>, which recomputes
        /// anyway. Doing it in this order means the stats are only ever read
        /// after both halves have settled.
        /// </summary>
        private void OnEquipmentChanged()
        {
            SyncEquipmentAuras();
            SyncGrantedAbilities();
            RefreshStats();
        }

        private void RefreshStats()
        {
            float fraction = MaxHealth > 0f ? Health / MaxHealth : 1f;
            Stats.Recompute(Status, Equipment);

            // Max health can change when a buff expires or a chest piece comes
            // off. Preserving the fraction avoids the classic bug where losing a
            // health bonus instantly kills whoever was relying on it.
            Health = Mathf.Min(Stats.MaxHealth, fraction * Stats.MaxHealth);

            if (!Stats.CanCast) Abilities.CancelCast();
            VitalsChanged?.Invoke(this);
        }

        private void SyncEquipmentAuras()
        {
            Equipment.CollectAuras(_auraScratch);

            // Remove auras that are no longer granted. Tracking what this actor
            // applied, rather than clearing every buff, keeps a set bonus from
            // stripping a cleric's blessing when someone swaps a helmet.
            for (int i = _activeEquipmentAuras.Count - 1; i >= 0; i--)
            {
                StatusEffectDefinition aura = _activeEquipmentAuras[i];
                if (_auraScratch.Contains(aura)) continue;

                Status.Remove(aura);
                _activeEquipmentAuras.RemoveAt(i);
            }

            for (int i = 0; i < _auraScratch.Count; i++)
            {
                StatusEffectDefinition aura = _auraScratch[i];
                if (_activeEquipmentAuras.Contains(aura)) continue;

                // Set auras last as long as the set is worn; the duration is a
                // formality the status system requires.
                Status.Apply(aura, EquipmentAuraSeconds, 1, this, Stats.Power, World.Clock.Tick);
                _activeEquipmentAuras.Add(aura);
            }
        }

        private void SyncGrantedAbilities()
        {
            Equipment.CollectGrantedAbilities(_grantedScratch);
            Abilities.SetGrantedAbilities(_grantedScratch);
        }

        private void OnStatusExpired(ActiveStatus status)
        {
            if (status?.Definition == null) return;
            if (status.Definition.OnExpire == null || status.Definition.OnExpire.Count == 0) return;

            // The applier is the caster of the delayed payload, which is what makes
            // "bomb" debuffs credit threat and damage to the right actor.
            World.AbilityRunner.RunEffects(
                status.Definition.OnExpire,
                status.Source != null && status.Source.IsAlive ? status.Source : this,
                this,
                Cell);
        }

        public int TicksSinceDamaged(int currentTick) =>
            _lastDamageTick == int.MinValue ? int.MaxValue : currentTick - _lastDamageTick;
    }
}
